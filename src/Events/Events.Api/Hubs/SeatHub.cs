using Microsoft.AspNetCore.SignalR;

namespace Events.Api.Hubs;

public class SeatHub : Hub
{
    // Join a SignalR group for a specific event so broadcasts target only clients
    public Task JoinEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return Task.CompletedTask;
        try
        {
            Console.WriteLine($"SeatHub: Connection {Context.ConnectionId} joining event group {eventId}");
        }
        catch { }
        return Groups.AddToGroupAsync(Context.ConnectionId, eventId);
    }

    public Task LeaveEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return Task.CompletedTask;
        try
        {
            Console.WriteLine($"SeatHub: Connection {Context.ConnectionId} leaving event group {eventId}");
        }
        catch { }
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, eventId);
    }
    public override Task OnConnectedAsync()
    {
        try { Console.WriteLine($"SeatHub: Connected {Context.ConnectionId} User={Context.UserIdentifier}"); } catch { }
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        try { Console.WriteLine($"SeatHub: Disconnected {Context.ConnectionId}"); } catch { }
        return base.OnDisconnectedAsync(exception);
    }
}
