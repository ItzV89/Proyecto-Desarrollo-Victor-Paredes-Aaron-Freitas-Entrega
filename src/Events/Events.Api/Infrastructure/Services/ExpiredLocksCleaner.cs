using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Threading;
using Events.Api.Infrastructure.Persistence;
using System;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using System.Text.Json;
using System.Text;

namespace Events.Api.Infrastructure.Services;

public class ExpiredLocksCleaner : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public ExpiredLocksCleaner(IServiceProvider provider)
    {
        _provider = provider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
                var eventBus = scope.ServiceProvider.GetService<Events.Api.Infrastructure.Services.IEventBus>();
                var now = DateTime.UtcNow;
                // find expired seats first to publish events per seat
                var expiredSeats = await db.Seats.Where(s => s.LockExpiresAt != null && s.LockExpiresAt < now).Select(s => new { s.Id, s.ScenarioId, s.Code, s.LockOwner }).ToListAsync();
                if (expiredSeats.Count > 0)
                {
                    var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE ""Seats""
                        SET ""IsAvailable"" = TRUE, ""LockOwner"" = NULL, ""LockExpiresAt"" = NULL
                        WHERE ""LockExpiresAt"" IS NOT NULL AND ""LockExpiresAt"" < {now}
                    ");

                    Console.WriteLine($"ExpiredLocksCleaner: released {affected} seats at {now}");

                        try
                        {
                            if (eventBus != null)
                            {
                                foreach (var seat in expiredSeats)
                                {
                                    var payload = JsonSerializer.Serialize(new { type = "SeatUnlocked", seatId = seat.Id, scenarioId = seat.ScenarioId, code = seat.Code, reservationId = seat.LockOwner });
                                    eventBus.Publish("plataforma.events", payload);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("ExpiredLocksCleaner publish error: " + ex.Message);
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ExpiredLocksCleaner error: " + ex.Message);
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
