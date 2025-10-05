using System.Text.RegularExpressions;
using CommentsApp.Core.DTOs;
using CommentsApp.Core.Entities;
using CommentsApp.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using CommentsApp.API.Hubs;

namespace CommentsApp.API.Services;

public class CommentService
{
    private readonly ICommentRepository _commentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICacheService _cacheService;
    private readonly IQueueService _queueService;
    private readonly IHubContext<CommentsHub> _hubContext;
    private static readonly string[] AllowedTags = { "a", "code", "i", "strong" };
    
    public CommentService(
        ICommentRepository commentRepository, 
        IUserRepository userRepository, 
        ICacheService cacheService, 
        IQueueService queueService,
        IHubContext<CommentsHub> hubContext)
    {
        _commentRepository = commentRepository;
        _userRepository = userRepository;
        _cacheService = cacheService;
        _queueService = queueService;
        _hubContext = hubContext;
    }
    
    public async Task<PagedResult<CommentDto>> GetCommentsAsync(int page, int pageSize, string sortBy, bool ascending)
    {
        var cacheKey = $"comments:page:{page}:size:{pageSize}:sort:{sortBy}:asc:{ascending}";
        var cached = await _cacheService.GetAsync<PagedResult<CommentDto>>(cacheKey);
        
        // ВИПРАВЛЕНО: було "= null", має бути "!= null"
        if (cached != null) return cached;
        
        var result = await _commentRepository.GetTopLevelCommentsAsync(page, pageSize, sortBy, ascending);
        
        var dto = new PagedResult<CommentDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
        
        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));
        return dto;
    }
    
    public async Task<CommentDto> CreateCommentAsync(CreateCommentDto dto, string ipAddress, string userAgent)
    {
        var sanitizedText = SanitizeHtml(dto.Text);
        
        var user = await _userRepository.GetByEmailAsync(dto.Email);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                UserName = dto.UserName,
                Email = dto.Email,
                HomePage = dto.HomePage,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            };
            user = await _userRepository.AddUserAsync(user);
        }
        
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ParentCommentId = dto.ParentCommentId,
            Text = sanitizedText,
            CreatedAt = DateTime.UtcNow
        };
        
        comment = await _commentRepository.AddCommentAsync(comment);
        
        // Публікація події в RabbitMQ
        await _queueService.PublishCommentCreatedAsync(comment.Id);
        
        // ДОДАНО: Відправка через SignalR
        var commentDto = MapToDto(comment);
        await _hubContext.Clients.All.SendAsync("ReceiveComment", commentDto);
        
        // Інвалідація кешу
        await InvalidateCache();
        
        return commentDto;
    }
    
    private string SanitizeHtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        // ВИПРАВЛЕНО: правильний regex для HTML тегів
        var pattern = @"<(/?)(\w+)([^>]*)>";
        
        return Regex.Replace(input, pattern, match =>
        {
            var isClosing = match.Groups[1].Value == "/";
            var tagName = match.Groups[2].Value.ToLower();
            var attributes = match.Groups[3].Value;
            
            // Якщо тег не дозволений, видаляємо його
            if (!AllowedTags.Contains(tagName))
                return string.Empty;
            
            // Для закриваючих тегів просто повертаємо їх
            if (isClosing)
                return $"</{tagName}>";
            
            // Для тегу <a> обробляємо атрибути
            if (tagName == "a")
            {
                var hrefMatch = Regex.Match(attributes, @"href\s*=\s*[""']([^""']+)[""']");
                var titleMatch = Regex.Match(attributes, @"title\s*=\s*[""']([^""']+)[""']");
                
                var href = hrefMatch.Success ? $"href=\"{System.Web.HttpUtility.HtmlEncode(hrefMatch.Groups[1].Value)}\"" : "";
                var title = titleMatch.Success ? $" title=\"{System.Web.HttpUtility.HtmlEncode(titleMatch.Groups[1].Value)}\"" : "";
                
                return $"<a {href}{title}>";
            }
            
            // Для інших дозволених тегів
            return $"<{tagName}>";
        });
    }
    
    private CommentDto MapToDto(Comment comment)
    {
        return new CommentDto
        {
            Id = comment.Id,
            UserName = comment.User.UserName,
            Email = comment.User.Email,
            HomePage = comment.User.HomePage,
            Text = comment.Text,
            ImageUrl = comment.ImagePath,
            TextFileUrl = comment.TextFilePath,
            CreatedAt = comment.CreatedAt,
            ParentCommentId = comment.ParentCommentId,
            Replies = comment.Replies.Select(MapToDto).ToList()
        };
    }
    
    private async Task InvalidateCache()
    {
        // Redis не підтримує wildcard delete, тому видаляємо ключі окремо
        // В production краще використовувати Redis key patterns
        for (int page = 1; page <= 10; page++)
        {
            var keys = new[]
            {
                $"comments:page:{page}:size:25:sort:createdAt:asc:false",
                $"comments:page:{page}:size:25:sort:createdAt:asc:true",
                $"comments:page:{page}:size:25:sort:userName:asc:false",
                $"comments:page:{page}:size:25:sort:userName:asc:true",
                $"comments:page:{page}:size:25:sort:email:asc:false",
                $"comments:page:{page}:size:25:sort:email:asc:true"
            };
            
            foreach (var key in keys)
            {
                await _cacheService.RemoveAsync(key);
            }
        }
    }
}