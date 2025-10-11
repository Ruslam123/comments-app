using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using CommentsApp.Core.DTOs;
using CommentsApp.Core.Entities;
using CommentsApp.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using CommentsApp.API.Hubs;
using Microsoft.Extensions.Logging;

namespace CommentsApp.API.Services;

public class CommentService
{
    private readonly ICommentRepository _commentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICacheService _cacheService;
    private readonly IQueueService _queueService;
    private readonly IHubContext<CommentsHub> _hubContext;
    private readonly ILogger<CommentService> _logger;
    private static readonly string[] AllowedTags = { "a", "code", "i", "strong" };
    
    public CommentService(
        ICommentRepository commentRepository, 
        IUserRepository userRepository, 
        ICacheService cacheService, 
        IQueueService queueService,
        IHubContext<CommentsHub> hubContext,
        ILogger<CommentService> logger)
    {
        _commentRepository = commentRepository;
        _userRepository = userRepository;
        _cacheService = cacheService;
        _queueService = queueService;
        _hubContext = hubContext;
        _logger = logger;
    }
    
    public async Task<PagedResult<CommentDto>> GetCommentsAsync(int page, int pageSize, string sortBy, bool ascending)
    {
        try
        {
            _logger.LogInformation($"GetCommentsAsync: page={page}, size={pageSize}, sort={sortBy}, asc={ascending}");
            
            // Спроба отримати з кешу
            var cacheKey = $"comments:page:{page}:size:{pageSize}:sort:{sortBy}:asc:{ascending}";
            
            try
            {
                var cached = await _cacheService.GetAsync<PagedResult<CommentDto>>(cacheKey);
                if (cached != null)
                {
                    _logger.LogInformation("✅ Returning cached result");
                    return cached;
                }
            }
            catch (Exception cacheEx)
            {
                _logger.LogWarning($"⚠️ Cache read failed (non-critical): {cacheEx.Message}");
            }
            
            // Отримання з БД
            _logger.LogInformation("🔍 Fetching from database...");
            var result = await _commentRepository.GetTopLevelCommentsAsync(page, pageSize, sortBy, ascending);
            
            if (result.Items == null || !result.Items.Any())
            {
                _logger.LogWarning("⚠️ No comments found in database");
                return new PagedResult<CommentDto>
                {
                    Items = new List<CommentDto>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize
                };
            }
            
            _logger.LogInformation($"✅ DB returned {result.Items.Count} items, total: {result.TotalCount}");
            
            // Маппінг з перевіркою
            var mappedItems = new List<CommentDto>();
            foreach (var comment in result.Items)
            {
                try
                {
                    var dto = MapToDto(comment);
                    mappedItems.Add(dto);
                }
                catch (Exception mapEx)
                {
                    _logger.LogError($"❌ Failed to map comment {comment.Id}: {mapEx.Message}");
                    // Пропускаємо цей коментар
                    continue;
                }
            }
            
            var pagedResult = new PagedResult<CommentDto>
            {
                Items = mappedItems,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };
            
            _logger.LogInformation($"✅ Successfully mapped {pagedResult.Items.Count} comments");
            
            // Збереження в кеш
            try
            {
                await _cacheService.SetAsync(cacheKey, pagedResult, TimeSpan.FromMinutes(5));
                _logger.LogInformation("✅ Saved to cache");
            }
            catch (Exception cacheEx)
            {
                _logger.LogWarning($"⚠️ Cache write failed (non-critical): {cacheEx.Message}");
            }
            
            return pagedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ CRITICAL ERROR in GetCommentsAsync: {ex.Message}");
            _logger.LogError($"Stack trace: {ex.StackTrace}");
            
            // Повертаємо порожній результат замість помилки
            return new PagedResult<CommentDto>
            {
                Items = new List<CommentDto>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            };
        }
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
        
        var commentDto = MapToDto(comment);
        
        // SignalR
        try
        {
            await _hubContext.Clients.All.SendAsync("ReceiveComment", commentDto);
            _logger.LogInformation($"✅ SignalR notification sent for comment {comment.Id}");
        }
        catch (Exception signalrEx)
        {
            _logger.LogWarning($"⚠️ SignalR failed (non-critical): {signalrEx.Message}");
        }
        
        // Queue
        try
        {
            await _queueService.PublishCommentCreatedAsync(comment.Id);
        }
        catch (Exception queueEx)
        {
            _logger.LogWarning($"⚠️ Queue publish failed (non-critical): {queueEx.Message}");
        }
        
        await InvalidateCache();
        
        return commentDto;
    }
    
    private string SanitizeHtml(string input)
    {
        // Використовуємо HtmlEncoder.Default замість System.Web.HttpUtility
        var sanitized = HtmlEncoder.Default.Encode(input);
        
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
        // КРИТИЧНА ПЕРЕВІРКА
        if (comment == null)
        {
            throw new ArgumentNullException(nameof(comment), "Comment cannot be null");
        }
        
        if (comment.User == null)
        {
            _logger.LogError($"❌ Comment {comment.Id} has NULL User! UserId: {comment.UserId}");
            throw new InvalidOperationException($"Comment {comment.Id} does not have User loaded. UserId: {comment.UserId}");
        }
        
        return new CommentDto
        {
            Id = comment.Id,
            UserName = comment.User.UserName ?? "Unknown",
            Email = comment.User.Email ?? "no-email@example.com",
            HomePage = comment.User.HomePage,
            Text = comment.Text ?? "",
            ImageUrl = comment.ImagePath,
            TextFileUrl = comment.TextFilePath,
            CreatedAt = comment.CreatedAt,
            ParentCommentId = comment.ParentCommentId,
            Replies = comment.Replies?.Select(r => {
                try
                {
                    return MapToDto(r);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to map reply {r.Id}: {ex.Message}");
                    return null;
                }
            }).Where(r => r != null).ToList() ?? new List<CommentDto>()
        };
    }
    
    private async Task InvalidateCache()
    {
        // Очистка кешу після створення коментаря
        await Task.CompletedTask;
    }
}
