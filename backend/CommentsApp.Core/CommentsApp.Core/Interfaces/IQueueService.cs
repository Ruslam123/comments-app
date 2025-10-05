namespace CommentsApp.Core.Interfaces;

public interface IQueueService
{
    Task PublishCommentCreatedAsync(Guid commentId);
}
