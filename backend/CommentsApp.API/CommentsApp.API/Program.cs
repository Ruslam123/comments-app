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
}
else
{
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
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisUrl + ",connectTimeout=5000,abortConnect=false"));
        builder.Services.AddScoped<ICacheService, RedisCacheService>();
    }
    catch
    {
        builder.Services.AddScoped<ICacheService, DummyCacheService>();
    }
}
else
{
    builder.Services.AddScoped<ICacheService, DummyCacheService>();
}

// === RabbitMQ ===
var rabbitMqUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL");
if (!string.IsNullOrEmpty(rabbitMqUrl))
{
    try
    {
        builder.Services.AddSingleton<IQueueService>(sp => new RabbitMqService(rabbitMqUrl));
    }
    catch
    {
        builder.Services.AddSingleton<IQueueService, DummyQueueService>();
    }
}
else
{
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

// === CORS - МАКСИМАЛЬНО ВІДКРИТИЙ ===
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("*");
    });
});

var app = builder.Build();

// Логування для дебагу
Console.WriteLine("=== APPLICATION STARTING ===");
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"CORS Policy: AllowAll (AllowAnyOrigin)");

app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName,
    cors = "enabled"
}));

app.MapGet("/", () => Results.Ok(new
{
    name = "Comments API",
    version = "1.0.0",
    status = "running",
    cors = "AllowAll policy active"
}));

// Міграції
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
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// КРИТИЧНО: Додатковий middleware для CORS заголовків
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

// ВАЖЛИВО: CORS middleware має бути ПЕРШИМ
app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.MapHub<CommentsHub>("/hubs/comments");

Console.WriteLine("=== APPLICATION STARTED ===");
Console.WriteLine("CORS is configured to allow all origins");
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