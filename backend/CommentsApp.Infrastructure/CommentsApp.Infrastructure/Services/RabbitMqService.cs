using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using CommentsApp.Core.Interfaces;
using CommentsApp.Core.Events;

namespace CommentsApp.Infrastructure.Services;

public class RabbitMqService : IQueueService, IDisposable
{
    private readonly IModel _channel;
    private const string QueueName = "comment-created-queue";
    public RabbitMqService(string conn) { var f = new ConnectionFactory { Uri = new Uri(conn) }; _channel = f.CreateConnection().CreateModel(); _channel.QueueDeclare(QueueName, true, false, false, null); }
    public Task PublishCommentCreatedAsync(Guid id) { var e = new CommentCreatedEvent { CommentId = id, UserId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }; var b = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(e)); _channel.BasicPublish("", QueueName, null, b); return Task.CompletedTask; }
    public void Dispose() { _channel?.Close(); }
}
