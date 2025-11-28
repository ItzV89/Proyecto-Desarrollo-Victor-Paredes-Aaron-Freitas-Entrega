namespace Events.Api.Infrastructure.Services;

public interface IEventBus
{
    void Publish(string exchange, string message);
}
