using Microsoft.EntityFrameworkCore;
using CommentsApp.Core.Entities;
using CommentsApp.Core.DTOs;
using CommentsApp.Core.Interfaces;
using CommentsApp.Infrastructure.Data;

namespace CommentsApp.Infrastructure.Repositories;

public class CommentRepository : ICommentRepository
{
    private readonly ApplicationDbContext _context;
    
    public CommentRepository(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<Comment> AddCommentAsync(Comment comment)
    {
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();
        
        // ВАЖЛИВО: Завантажуємо User після збереження
        return await _context.Comments
            .Include(c => c.User)
            .Include(c => c.Replies)
            .FirstAsync(c => c.Id == comment.Id);
    }
    
    public async Task<int> GetTotalCountAsync()
    {
        return await _context.Comments
            .Where(c => c.ParentCommentId == null)
            .CountAsync();
    }
    
    public async Task<Comment?> GetCommentWithRepliesAsync(Guid id)
    {
        return await _context.Comments
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(c => c.Id == id);
    }
    
    public async Task<PagedResult<Comment>> GetTopLevelCommentsAsync(
        int page, 
        int pageSize, 
        string sortBy, 
        bool ascending)
    {
        // Завантажуємо ВСІ коментарі з усіма вкладеними рівнями
        var allComments = await _context.Comments
            .Include(c => c.User)
            .AsNoTracking()
            .ToListAsync();
        
        // Функція для рекурсивного завантаження replies
        void LoadReplies(Comment comment)
        {
            var replies = allComments.Where(c => c.ParentCommentId == comment.Id).ToList();
            foreach (var reply in replies)
            {
                // Завантажуємо User для reply
                reply.User = allComments.First(c => c.Id == reply.Id).User;
                comment.Replies.Add(reply);
                
                // РЕКУРСІЯ: Завантажуємо replies для цього reply
                LoadReplies(reply);
            }
        }
        
        // Отримуємо тільки топ-левел коментарі
        var topLevelComments = allComments
            .Where(c => c.ParentCommentId == null)
            .ToList();
        
        // Завантажуємо всі вкладені replies для кожного топ-левел коментаря
        foreach (var comment in topLevelComments)
        {
            LoadReplies(comment);
        }
        
        // Сортування
        IEnumerable<Comment> sortedComments = sortBy.ToLower() switch
        {
            "username" => ascending 
                ? topLevelComments.OrderBy(c => c.User.UserName) 
                : topLevelComments.OrderByDescending(c => c.User.UserName),
            "email" => ascending 
                ? topLevelComments.OrderBy(c => c.User.Email) 
                : topLevelComments.OrderByDescending(c => c.User.Email),
            _ => ascending 
                ? topLevelComments.OrderBy(c => c.CreatedAt) 
                : topLevelComments.OrderByDescending(c => c.CreatedAt)
        };
        
        var totalCount = topLevelComments.Count;
        var items = sortedComments
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        
        return new PagedResult<Comment>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
