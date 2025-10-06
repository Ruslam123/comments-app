using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using CommentsApp.Infrastructure.Data;
using CommentsApp.Infrastructure.Repositories;
using CommentsApp.Infrastructure.Services;
using CommentsApp.Core.Interfaces;
using CommentsApp.API.Services;
using CommentsApp.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Парсинг DATABASE_URL для Railway/Render
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;

if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Redis
var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") 
    ?? builder.Configuration.GetConnectionString("Redis")!;
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(redisUrl));

// RabbitMQ
var rabbitMqUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL") 
    ?? builder.Configuration.GetConnectionString("RabbitMQ")!;
builder.Services.AddSingleton<IQueueService>(sp =>
    new RabbitMqService(rabbitMqUrl));

builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();
builder.Services.AddScoped<CommentService>();

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Створюємо папку uploads
var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

// Міграції
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
}

app.UseCors("AllowFrontend");
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.MapHub<CommentsHub>("/hubs/comments");

app.Run();