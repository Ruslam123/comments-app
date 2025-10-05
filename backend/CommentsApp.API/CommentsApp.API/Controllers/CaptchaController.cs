using Microsoft.AspNetCore.Mvc;

namespace CommentsApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CaptchaController : ControllerBase
{
    private static readonly Dictionary<string, string> CaptchaCodes = new();
    
    [HttpGet]
    public IActionResult GenerateCaptcha()
    {
        var code = GenerateRandomCode(6);
        var token = Guid.NewGuid().ToString();
        
        CaptchaCodes[token] = code;
        
        // Повертаємо просто текст замість зображення
        return Ok(new { token, code });
    }
    
    [HttpPost("validate")]
    public IActionResult ValidateCaptcha([FromBody] CaptchaValidationDto dto)
    {
        if (CaptchaCodes.TryGetValue(dto.Token, out var code) && code.Equals(dto.Code, StringComparison.OrdinalIgnoreCase))
        {
            CaptchaCodes.Remove(dto.Token);
            return Ok(new { valid = true });
        }
        
        return Ok(new { valid = false });
    }
    
    private string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

public class CaptchaValidationDto
{
    public string Token { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}