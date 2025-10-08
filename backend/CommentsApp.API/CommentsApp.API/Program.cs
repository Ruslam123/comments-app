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

// Обмеження Kestrel
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
    Console.WriteLine("Using DATABASE_URL from environment");
    
    // Railway/Render використовують postgresql://, змінюємо на postgres://
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
    
    Console.WriteLine($"Database: {uri.Host}:{uri.Port}/{uri.LocalPath.TrimStart('/')}");
}
else
{
    Console.WriteLine("Using DefaultConnection from appsettings.json");
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// === Redis ===
var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
if (!string.IsNullOrEmpty(redisUrl))
{
    try
    {
        Console.WriteLine("Connecting to Redis...");
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisUrl + ",connectTimeout=5000,abortConnect=false"));
        builder.Services.AddScoped<ICacheService, RedisCacheService>();
        Console.WriteLine("Redis connected");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Redis connection failed: {ex.Message}");
        builder.Services.AddScoped<ICacheService, DummyCacheService>();
    }
}
else
{
    Console.WriteLine("Redis not configured, using dummy cache");
    builder.Services.AddScoped<ICacheService, DummyCacheService>();
}

// === RabbitMQ ===
var rabbitMqUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL");
if (!string.IsNullOrEmpty(rabbitMqUrl))
{
    try
    {
        Console.WriteLine("Connecting to RabbitMQ...");
        builder.Services.AddSingleton<IQueueService>(sp => new RabbitMqService(rabbitMqUrl));
        Console.WriteLine("RabbitMQ connected");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"RabbitMQ connection failed: {ex.Message}");
        builder.Services.AddSingleton<IQueueService, DummyQueueService>();
    }
}
else
{
    Console.WriteLine("RabbitMQ not configured, using dummy queue");
    builder.Services.AddSingleton<IQueueService, DummyQueueService>();
}

// === Services ===
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<CommentService>();

// === SignalR ===
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 32 * 1024;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// === Health Check ===
app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));

app.MapGet("/", () => Results.Ok(new
{
    name = "Comments API",
    version = "1.0.0",
    status = "running"
}));

// === Міграції ===
try
{
    Console.WriteLine("Running database migrations...");
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    Console.WriteLine("Migrations completed successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Migration failed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

// === Middleware ===
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
    context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
    context.Response.Headers.Add("Access-Control-Allow-Headers", "*");
    
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        return;
    }
    
    await next();
});

app.UseCors();

app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.MapHub<CommentsHub>("/hubs/comments");

Console.WriteLine($"Starting application on {app.Environment.EnvironmentName}");
app.Run();

// === Dummy Services ===
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