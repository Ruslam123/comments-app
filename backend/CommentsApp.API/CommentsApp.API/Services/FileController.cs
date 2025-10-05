using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.Drawing.Imaging;

namespace CommentsApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly string _uploadsPath;
    private readonly ILogger<FileController> _logger;
    
    public FileController(IWebHostEnvironment env, ILogger<FileController> logger)
    {
        _uploadsPath = Path.Combine(env.WebRootPath ?? env.ContentRootPath, "uploads");
        _logger = logger;
        
        // Створити папку якщо не існує
        if (!Directory.Exists(_uploadsPath))
            Directory.CreateDirectory(_uploadsPath);
    }
    
    [HttpPost("image")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5MB max
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Файл не завантажено");
        
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".gif", ".png" };
        var extension = Path.GetExtension(file.FileName).ToLower();
        
        if (!allowedExtensions.Contains(extension))
            return BadRequest("Дозволені тільки JPG, GIF, PNG");
        
        try
        {
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(_uploadsPath, fileName);
            
            using (var stream = file.OpenReadStream())
            using (var image = Image.FromStream(stream))
            {
                // Resize до 320x240 зі збереженням пропорцій
                var resized = ResizeImage(image, 320, 240);
                resized.Save(filePath, GetImageFormat(extension));
            }
            
            return Ok(new { url = $"/uploads/{fileName}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження зображення");
            return StatusCode(500, "Помилка обробки зображення");
        }
    }
    
    [HttpPost("text")]
    [RequestSizeLimit(100 * 1024)] // 100KB max
    public async Task<IActionResult> UploadTextFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Файл не завантажено");
        
        if (Path.GetExtension(file.FileName).ToLower() != ".txt")
            return BadRequest("Дозволені тільки .txt файли");
        
        if (file.Length > 100 * 1024)
            return BadRequest("Файл занадто великий (max 100KB)");
        
        try
        {
            var fileName = $"{Guid.NewGuid()}.txt";
            var filePath = Path.Combine(_uploadsPath, fileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            
            return Ok(new { url = $"/uploads/{fileName}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження текстового файлу");
            return StatusCode(500, "Помилка завантаження файлу");
        }
    }
    
    private Image ResizeImage(Image image, int maxWidth, int maxHeight)
    {
        var ratioX = (double)maxWidth / image.Width;
        var ratioY = (double)maxHeight / image.Height;
        var ratio = Math.Min(ratioX, ratioY);
        
        var newWidth = (int)(image.Width * ratio);
        var newHeight = (int)(image.Height * ratio);
        
        var newImage = new Bitmap(newWidth, newHeight);
        using (var graphics = Graphics.FromImage(newImage))
        {
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.DrawImage(image, 0, 0, newWidth, newHeight);
        }
        
        return newImage;
    }
    
    private ImageFormat GetImageFormat(string extension)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".png" => ImageFormat.Png,
            ".gif" => ImageFormat.Gif,
            _ => ImageFormat.Jpeg
        };
    }
}