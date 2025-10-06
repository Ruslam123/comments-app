using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Gif;

namespace CommentsApp.API.Controllers;

// ВАЖЛИВО: Цей файл має бути в папці Controllers, а не Services!

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly string _uploadsPath;
    private readonly ILogger<FileController> _logger;
    
    public FileController(IWebHostEnvironment env, ILogger<FileController> logger)
    {
        _uploadsPath = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
        _logger = logger;
        
        _logger.LogInformation($"Uploads path: {_uploadsPath}");
        
        if (!Directory.Exists(_uploadsPath))
        {
            Directory.CreateDirectory(_uploadsPath);
            _logger.LogInformation($"Created uploads directory: {_uploadsPath}");
        }
    }
    
    [HttpPost("image")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile file)
    {
        _logger.LogInformation($"Received file upload request. File: {file?.FileName}, Size: {file?.Length}");
        
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("No file provided");
            return BadRequest(new { error = "Файл не завантажено" });
        }
        
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".gif", ".png" };
        var extension = Path.GetExtension(file.FileName).ToLower();
        
        if (!allowedExtensions.Contains(extension))
        {
            _logger.LogWarning($"Invalid extension: {extension}");
            return BadRequest(new { error = "Дозволені тільки JPG, GIF, PNG" });
        }
        
        try
        {
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(_uploadsPath, fileName);
            
            _logger.LogInformation($"Saving image to: {filePath}");
            
            using (var stream = file.OpenReadStream())
            using (var image = await Image.LoadAsync(stream))
            {
                // Перевіряємо чи потрібно змінювати розмір
                if (image.Width > 320 || image.Height > 240)
                {
                    _logger.LogInformation($"Resizing image from {image.Width}x{image.Height} to fit 320x240");
                    
                    // Зберігаємо пропорції
                    var ratioX = 320.0 / image.Width;
                    var ratioY = 240.0 / image.Height;
                    var ratio = Math.Min(ratioX, ratioY);
                    
                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);
                    
                    image.Mutate(x => x.Resize(newWidth, newHeight));
                }
                
                // Зберігаємо з правильним форматом
                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        await image.SaveAsJpegAsync(filePath, new JpegEncoder { Quality = 85 });
                        break;
                    case ".png":
                        await image.SaveAsPngAsync(filePath);
                        break;
                    case ".gif":
                        await image.SaveAsGifAsync(filePath);
                        break;
                }
                
                _logger.LogInformation($"Image saved successfully: {fileName}");
            }
            
            return Ok(new { url = $"/uploads/{fileName}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            return StatusCode(500, new { error = $"Помилка обробки зображення: {ex.Message}" });
        }
    }
    
    [HttpPost("text")]
    [RequestSizeLimit(100 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadTextFile([FromForm] IFormFile file)
    {
        _logger.LogInformation($"Received text file upload. File: {file?.FileName}, Size: {file?.Length}");
        
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("No file provided");
            return BadRequest(new { error = "Файл не завантажено" });
        }
        
        var extension = Path.GetExtension(file.FileName).ToLower();
        if (extension != ".txt")
        {
            _logger.LogWarning($"Invalid file type: {file.FileName}");
            return BadRequest(new { error = "Дозволені тільки .txt файли" });
        }
        
        if (file.Length > 100 * 1024)
        {
            _logger.LogWarning($"File too large: {file.Length} bytes");
            return BadRequest(new { error = "Файл занадто великий (max 100KB)" });
        }
        
        try
        {
            var fileName = $"{Guid.NewGuid()}.txt";
            var filePath = Path.Combine(_uploadsPath, fileName);
            
            _logger.LogInformation($"Saving text file to: {filePath}");
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            
            _logger.LogInformation($"Text file saved successfully: {fileName}");
            
            return Ok(new { url = $"/uploads/{fileName}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading text file");
            return StatusCode(500, new { error = $"Помилка завантаження файлу: {ex.Message}" });
        }
    }
}