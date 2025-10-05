using Microsoft.EntityFrameworkCore;
using CommentsApp.Core.Entities;
using CommentsApp.Core.DTOs;
using CommentsApp.Core.Interfaces;
using CommentsApp.Infrastructure.Data;

namespace CommentsApp.Infrastructure.Repositories;

public class CommentRepository : ICommentRepository
{
    private readonly ApplicationDbContext _context;
    public CommentRepository(ApplicationDbContext context) { _context = context; }
    public async Task<Comment> AddCommentAsync(Comment comment) { _context.Comments.Add(comment); await _context.SaveChangesAsync(); return comment; }
    public async Task<int> GetTotalCountAsync() => await _context.Comments.CountAsync();
    public async Task<Comment?> GetCommentWithRepliesAsync(Guid id) => await _context.Comments.Include(c => c.User).Include(c => c.Replies).ThenInclude(r => r.User).FirstOrDefaultAsync(c => c.Id == id);
    public async Task<PagedResult<Comment>> GetTopLevelCommentsAsync(int page, int pageSize, string sortBy, bool asc) { var q = _context.Comments.Include(c => c.User).Include(c => c.Replies).ThenInclude(r => r.User).Where(c => c.ParentCommentId == null).OrderByDescending(c => c.CreatedAt); var total = await q.CountAsync(); var items = await q.Skip((page-1)*pageSize).Take(pageSize).ToListAsync(); return new PagedResult<Comment> { Items = items, TotalCount = total, Page = page, PageSize = pageSize }; }
}
