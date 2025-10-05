using Microsoft.AspNetCore.SignalR;

namespace CommentsApp.API.Hubs;

public class CommentsHub : Hub
{
    public async Task NotifyNewComment(object comment) => await Clients.All.SendAsync("ReceiveComment", comment);
}