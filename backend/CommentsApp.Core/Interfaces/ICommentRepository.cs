using CommentsApp.Core.Entities;
using CommentsApp.Core.DTOs;

namespace CommentsApp.Core.Interfaces;

public interface ICommentRepository
{
    Task<PagedResult<Comment>> GetTopLevelCommentsAsync(int page, int pageSize, string sortBy, bool ascending);
    Task<Comment?> GetCommentWithRepliesAsync(Guid id);
    Task<Comment> AddCommentAsync(Comment comment);
    Task<int> GetTotalCountAsync();
}
