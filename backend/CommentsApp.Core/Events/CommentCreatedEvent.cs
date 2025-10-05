namespace CommentsApp.Core.Events;

public class CommentCreatedEvent
{
    public Guid CommentId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
