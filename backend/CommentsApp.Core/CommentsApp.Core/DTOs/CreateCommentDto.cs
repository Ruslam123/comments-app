namespace CommentsApp.Core.DTOs;

public class CreateCommentDto
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? HomePage { get; set; }
    public string CaptchaToken { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public Guid? ParentCommentId { get; set; }
}
