namespace CommentsApp.Core.DTOs;

public class CommentDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? HomePage { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? TextFileUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ParentCommentId { get; set; }
    public List<CommentDto> Replies { get; set; } = new();
}
