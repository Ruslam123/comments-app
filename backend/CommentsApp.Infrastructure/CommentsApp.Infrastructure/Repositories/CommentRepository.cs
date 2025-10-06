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
        var query = _context.Comments
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .Where(c => c.ParentCommentId == null);
        
        // Сортування
        query = sortBy.ToLower() switch
        {
            "username" => ascending 
                ? query.OrderBy(c => c.User.UserName) 
                : query.OrderByDescending(c => c.User.UserName),
            "email" => ascending 
                ? query.OrderBy(c => c.User.Email) 
                : query.OrderByDescending(c => c.User.Email),
            _ => ascending 
                ? query.OrderBy(c => c.CreatedAt) 
                : query.OrderByDescending(c => c.CreatedAt)
        };
        
        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return new PagedResult<Comment>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}