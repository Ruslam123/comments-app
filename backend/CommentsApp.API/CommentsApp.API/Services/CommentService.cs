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
            ImagePath = dto.ImagePath,
            TextFilePath = dto.TextFilePath,
            CreatedAt = DateTime.UtcNow
        };
        
        comment = await _commentRepository.AddCommentAsync(comment);
        
        // Відправляємо подію через SignalR
        var commentDto = MapToDto(comment);
        await _hubContext.Clients.All.SendAsync("ReceiveComment", commentDto);
        
        await _queueService.PublishCommentCreatedAsync(comment.Id);
        await InvalidateCache();
        
        return commentDto;
    }
    
    private string SanitizeHtml(string input)
    {
        // Спочатку екрануємо всі HTML символи
        var sanitized = System.Web.HttpUtility.HtmlEncode(input);
        
        // Тепер дозволяємо тільки безпечні теги
        var pattern = @"&lt;(/?)(\w+)(.*?)&gt;";
        
        sanitized = Regex.Replace(sanitized, pattern, match =>
        {
            var isClosing = match.Groups[1].Value == "/";
            var tagName = match.Groups[2].Value.ToLower();
            var attributes = match.Groups[3].Value;
            
            if (!AllowedTags.Contains(tagName))
                return string.Empty;
            
            if (tagName == "a" && !isClosing)
            {
                // Обробляємо атрибути посилання
                var hrefMatch = Regex.Match(attributes, @"href\s*=\s*&quot;([^&]+)&quot;");
                var titleMatch = Regex.Match(attributes, @"title\s*=\s*&quot;([^&]+)&quot;");
                
                var href = hrefMatch.Success ? $"href=\"{hrefMatch.Groups[1].Value}\"" : "";
                var title = titleMatch.Success ? $" title=\"{titleMatch.Groups[1].Value}\"" : "";
                
                return $"<a {href}{title}>";
            }
            
            return $"<{match.Groups[1].Value}{tagName}>";
        });
        
        return sanitized;
    }
    
    private CommentDto MapToDto(Comment comment)
    {
        if (comment.User == null)
        {
            throw new InvalidOperationException($"Comment {comment.Id} does not have User loaded");
        }
        
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
            Replies = comment.Replies?.Select(MapToDto).ToList() ?? new List<CommentDto>()
        };
    }
    
    private async Task InvalidateCache()
    {
        // Redis не підтримує wildcard видалення через StackExchange.Redis
        // Тому просто очищуємо кеш через TTL
        await Task.CompletedTask;
    }
}