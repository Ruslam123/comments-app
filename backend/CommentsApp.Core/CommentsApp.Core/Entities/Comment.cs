namespace CommentsApp.Core.Entities;

public class Comment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? ParentCommentId { get; set; }
    public Comment? ParentComment { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string? TextFilePath { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
}
