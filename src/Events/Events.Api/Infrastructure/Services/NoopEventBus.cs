using System;
namespace Events.Api.Infrastructure.Services;

public class NoopEventBus : IEventBus
{
    public void Publish(string exchange, string message)
    {
        // No-op in local development (avoids RabbitMQ dependency)
        Console.WriteLine($"NoopEventBus: would publish to {exchange}: {message}");
    }
}
