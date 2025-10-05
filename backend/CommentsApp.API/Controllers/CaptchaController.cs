using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.Drawing.Imaging;

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
        
        var image = GenerateCaptchaImage(code);
        
        return Ok(new { token, image = Convert.ToBase64String(image) });
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
    
    private byte[] GenerateCaptchaImage(string code)
    {
        using var bitmap = new Bitmap(200, 80);
        using var graphics = Graphics.FromImage(bitmap);
        
        graphics.Clear(Color.White);
        
        var font = new Font("Arial", 24, FontStyle.Bold);
        graphics.DrawString(code, font, Brushes.Black, new PointF(10, 20));
        
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}

public class CaptchaValidationDto
{
    public string Token { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}