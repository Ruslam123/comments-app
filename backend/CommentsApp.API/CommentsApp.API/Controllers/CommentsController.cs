using Microsoft.AspNetCore.Mvc;
using CommentsApp.API.Services;
using CommentsApp.Core.DTOs;

namespace CommentsApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommentsController : ControllerBase
{
    private readonly CommentService _commentService;
    
    public CommentsController(CommentService commentService)
    {
        _commentService = commentService;
    }
    
    [HttpGet]
    public async Task<ActionResult<PagedResult<CommentDto>>> GetComments(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 25,
    [FromQuery] string sortBy = "createdAt",
    [FromQuery] bool ascending = false)
{
    // Виправлення: більш м'яка валідація
    if (page < 1) page = 1;
    if (pageSize < 1) pageSize = 25;
    if (pageSize > 100) pageSize = 100;
    
    var result = await _commentService.GetCommentsAsync(page, pageSize, sortBy, ascending);
    return Ok(result);
}
    
    [HttpPost]
    public async Task<ActionResult<CommentDto>> CreateComment([FromBody] CreateCommentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers["User-Agent"].ToString();
        
        var result = await _commentService.CreateCommentAsync(dto, ipAddress, userAgent);
        return CreatedAtAction(nameof(GetComments), new { id = result.Id }, result);
    }
    
    [HttpPost("preview")]
    public ActionResult<string> PreviewComment([FromBody] PreviewDto dto)
    {
        return Ok(new { html = dto.Text });
    }
}

public class PreviewDto
{
    public string Text { get; set; } = string.Empty;
}
