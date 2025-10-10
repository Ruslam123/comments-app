using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Npgsql;
using CommentsApp.Infrastructure.Data;
using CommentsApp.Infrastructure.Repositories;
using CommentsApp.Infrastructure.Services;
using CommentsApp.Core.Interfaces;
using CommentsApp.API.Services;
using CommentsApp.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Логування
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 100;
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// === PostgreSQL ===
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;

if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl.Replace("postgresql://", "postgres://"));
    var userInfo = uri.UserInfo.Split(':');
    connectionString = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.LocalPath.TrimStart('/'),
        Username = userInfo[0],
        Password = userInfo.Length > 1 ? userInfo[1] : "",
        SslMode = SslMode.Require,
        TrustServerCertificate = true,
        Timeout = 30,
        CommandTimeout = 30,
        Pooling = true,
        MaxPoolSize = 20,
        MinPoolSize = 5
    }.ToString();
    
    Console.WriteLine($"[DB] Connected to: {uri.Host}:{uri.Port}/{uri.LocalPath.TrimStart('/')}");
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
    Console.WriteLine("[DB] Using default connection string");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
    options.EnableDetailedErrors();
});

// === Redis ===
var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
if (!string.IsNullOrEmpty(redisUrl))
{
    try
    {
        Console.WriteLine("[Redis] Attempting connection...");
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisUrl + ",connectTimeout=5000,abortConnect=false"));
        builder.Services.AddScoped<ICacheService, RedisCacheService>();
        Console.WriteLine("[Redis] ✅ Connected");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Redis] ⚠️ Failed: {ex.Message}, using dummy cache");
        builder.Services.AddScoped<ICacheService, DummyCacheService>();
    }
}
else
{
    Console.WriteLine("[Redis] Not configured, using dummy cache");
    builder.Services.AddScoped<ICacheService, DummyCacheService>();
}

// === RabbitMQ ===
var rabbitMqUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL");
if (!string.IsNullOrEmpty(rabbitMqUrl))
{
    try
    {
        Console.WriteLine("[RabbitMQ] Attempting connection...");
        builder.Services.AddSingleton<IQueueService>(sp => new RabbitMqService(rabbitMqUrl));
        Console.WriteLine("[RabbitMQ] ✅ Connected");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[RabbitMQ] ⚠️ Failed: {ex.Message}, using dummy queue");
        builder.Services.AddSingleton<IQueueService, DummyQueueService>();
    }
}
else
{
    Console.WriteLine("[RabbitMQ] Not configured, using dummy queue");
    builder.Services.AddSingleton<IQueueService, DummyQueueService>();
}

builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<CommentService>();

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 32 * 1024;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// === CORS ===
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

Console.WriteLine("=== APPLICATION STARTING ===");
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");

// Health endpoint
app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));

// Debug endpoint
app.MapGet("/debug/db", async (ApplicationDbContext db) => 
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        var userCount = await db.Users.CountAsync();
        var commentCount = await db.Comments.CountAsync();
        
        return Results.Ok(new
        {
            canConnect,
            userCount,
            commentCount,
            connectionString = db.Database.GetConnectionString()?.Split(';').FirstOrDefault()
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            error = ex.Message,
            stackTrace = ex.StackTrace
        }, statusCode: 500);
    }
});

// === Міграції ===
try
{
    Console.WriteLine("[Migrations] Starting...");
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    var canConnect = await db.Database.CanConnectAsync();
    Console.WriteLine($"[Migrations] Can connect to DB: {canConnect}");
    
    if (canConnect)
    {
        await db.Database.MigrateAsync();
        Console.WriteLine("[Migrations] ✅ Completed successfully");
        
        var userCount = await db.Users.CountAsync();
        var commentCount = await db.Comments.CountAsync();
        Console.WriteLine($"[DB] Users: {userCount}, Comments: {commentCount}");
    }
    else
    {
        Console.WriteLine("[Migrations] ❌ Cannot connect to database");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Migrations] ❌ Failed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// Перевірка wwwroot
var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Console.WriteLine($"[wwwroot] Path: {wwwrootPath}");
Console.WriteLine($"[wwwroot] Exists: {Directory.Exists(wwwrootPath)}");
if (Directory.Exists(wwwrootPath))
{
    var files = Directory.GetFiles(wwwrootPath);
    Console.WriteLine($"[wwwroot] Files: {files.Length}");
    foreach (var file in files.Take(5))
    {
        Console.WriteLine($"  - {Path.GetFileName(file)}");
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.MapHub<CommentsHub>("/hubs/comments");
app.MapFallbackToFile("index.html");

Console.WriteLine("=== APPLICATION STARTED ===");
app.Run();

public class DummyCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) => Task.CompletedTask;
    public Task RemoveAsync(string key) => Task.CompletedTask;
}

public class DummyQueueService : IQueueService
{
    public Task PublishCommentCreatedAsync(Guid commentId) => Task.CompletedTask;
}