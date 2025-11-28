using Microsoft.AspNetCore.Mvc;
using Events.Api.Infrastructure.Persistence;
using Events.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Events.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly EventsDbContext _db;
    private readonly Events.Api.Infrastructure.Services.IEventBus _eventBus;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<Events.Api.Hubs.SeatHub> _hubContext;
    private readonly Microsoft.Extensions.Logging.ILogger<ReservationsController> _logger;

    public ReservationsController(EventsDbContext db, Events.Api.Infrastructure.Services.IEventBus eventBus, Microsoft.AspNetCore.SignalR.IHubContext<Events.Api.Hubs.SeatHub> hubContext, Microsoft.Extensions.Logging.ILogger<ReservationsController> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _hubContext = hubContext;
        _logger = logger;
    }

    public record SeatRef(Guid ScenarioId, Guid SeatId);
    public record ConfirmReservationRequest(Guid ReservationId, Guid EventId, List<SeatRef>? Seats);

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "UsuarioAutenticado")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmReservationRequest? payload)
    {
        try
        {
            _logger.LogInformation("Confirm reservation payload: {payload}", System.Text.Json.JsonSerializer.Serialize(payload));
        }
        catch { }

        if (payload == null) {
            _logger.LogWarning("Confirm called with null payload");
            return BadRequest(new { message = "payload required" });
        }
        if (payload.ReservationId == Guid.Empty) return BadRequest("reservationId required");

        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub)) return Forbid();

        var seatsRefs = payload.Seats ?? new List<SeatRef>();
        if (!seatsRefs.Any()) return BadRequest("seats required");

        var seatIds = seatsRefs.Select(s => s.SeatId).ToList();
        var seats = await _db.Seats.Where(s => seatIds.Contains(s.Id)).ToListAsync();

        // ensure all requested seats exist
        if (seats.Count != seatIds.Count)
        {
            var missing = seatIds.Except(seats.Select(s => s.Id)).ToList();
            return BadRequest(new { message = "Some seats not found", missing });
        }

        // ensure all requested seats are locked by this reservation
        var notLocked = seats.Where(s => s.LockOwner != payload.ReservationId).ToList();
        if (notLocked.Any())
        {
            return Conflict(new { message = "Some seats are not locked by this reservation", seats = notLocked.Select(s => new { s.Id, s.Code, s.IsAvailable, s.LockOwner }) });
        }

        // create reservation
        var reservation = new Reservation { Id = payload.ReservationId, UserKeycloakId = sub, EventId = payload.EventId, CreatedAt = DateTime.UtcNow, Status = "Confirmed" };
        foreach (var s in seats)
        {
            reservation.Seats.Add(new ReservationSeat { Id = Guid.NewGuid(), ReservationId = reservation.Id, SeatId = s.Id, Code = s.Code, Type = s.Type, Price = s.Price });
        }

        _db.Reservations.Add(reservation);

        // mark seats as no longer available and clear locks
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Seats""
            SET ""IsAvailable"" = FALSE, ""LockOwner"" = NULL, ""LockExpiresAt"" = NULL
            WHERE ""Id"" = ANY({seatIds})
        ");

        await _db.SaveChangesAsync();

        // publish eventbus event and broadcast reservation created and seat taken
        try
        {
            var payloadMsg = System.Text.Json.JsonSerializer.Serialize(new { type = "ReservationCreated", reservationId = reservation.Id, eventId = reservation.EventId, userId = reservation.UserKeycloakId });
            try { _eventBus?.Publish("plataforma.events", payloadMsg); } catch { }

            // broadcast seats taken to clients subscribed to this event
            var seatsPayload = reservation.Seats.Select(s => new { s.SeatId, s.Code, s.Type, s.Price }).ToList();
            var broadcast = new { type = "ReservationCreated", reservationId = reservation.Id, eventId = reservation.EventId, seats = seatsPayload };
            try
            {
                await _hubContext.Clients.Group(reservation.EventId.ToString()).SendCoreAsync("ReservationCreated", new object[] { broadcast });
            }
            catch { }
        }
        catch { }

        return CreatedAtAction(nameof(GetMy), new { id = reservation.Id }, new { reservation.Id });
    }

    [HttpGet("my")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "UsuarioAutenticado")]
    public async Task<IActionResult> GetMy()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub)) return Forbid();
        // load reservations and associated seats, but exclude cancelled ones (they should not appear in the UI)
        var reservations = await _db.Reservations.Include(r => r.Seats)
            .Where(r => r.UserKeycloakId == sub && r.Status != "Cancelled")
            .OrderByDescending(r => r.CreatedAt).ToListAsync();
        var eventIds = reservations.Select(r => r.EventId).Distinct().ToList();
        var events = await _db.Events.Where(e => eventIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => new { e.Name, e.Date });

        // group reservations by event and aggregate seats so frontend can show per-event grouped seats
        var grouped = reservations.GroupBy(r => r.EventId).Select(g => new
        {
            eventId = g.Key,
            eventName = events.ContainsKey(g.Key) ? events[g.Key].Name : null,
            eventDate = events.ContainsKey(g.Key) ? events[g.Key].Date : (DateTime?)null,
            // ensure seats are unique by seatId to avoid duplicates in the UI
            seats = g.SelectMany(r => r.Seats)
                     .GroupBy(s => s.SeatId)
                     .Select(gr => gr.First())
                     .Select(s => new { seatId = s.SeatId, code = s.Code, type = s.Type, price = s.Price }).ToList(),
            reservations = g.Select(r => new { reservationId = r.Id, createdAt = r.CreatedAt, status = r.Status }).ToList()

        }).Where(g => (g.seats?.Count ?? 0) > 0).ToList();

        return Ok(grouped);
    }

    [HttpDelete("{reservationId}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "UsuarioAutenticado")]
    public async Task<IActionResult> Cancel(Guid reservationId)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub)) return Forbid();

        var reservation = await _db.Reservations.Include(r => r.Seats).FirstOrDefaultAsync(r => r.Id == reservationId && r.UserKeycloakId == sub);
        if (reservation == null) return NotFound();

        // release seats
        var seatIds = reservation.Seats.Select(s => s.SeatId).ToList();
        if (seatIds.Count > 0)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""Seats""
                SET ""IsAvailable"" = TRUE, ""LockOwner"" = NULL, ""LockExpiresAt"" = NULL
                WHERE ""Id"" = ANY({seatIds})
            ");
        }

        reservation.Status = "Cancelled";
        _db.Reservations.Update(reservation);
        await _db.SaveChangesAsync();

        try
        {
            var payloadMsg = System.Text.Json.JsonSerializer.Serialize(new { type = "ReservationCancelled", reservationId = reservation.Id, eventId = reservation.EventId, userId = reservation.UserKeycloakId });
            try { _eventBus?.Publish("plataforma.events", payloadMsg); } catch { }

            var seatsPayload = reservation.Seats.Select(s => new { s.SeatId, s.Code, s.Type, s.Price }).ToList();
            var broadcast = new { type = "ReservationCancelled", reservationId = reservation.Id, eventId = reservation.EventId, seats = seatsPayload };
            try
            {
                await _hubContext.Clients.Group(reservation.EventId.ToString()).SendCoreAsync("ReservationCancelled", new object[] { broadcast });
            }
            catch { }
            // Also broadcast SeatUnlocked for each released seat so event views update availability
            try
            {
                // fetch seat records to include scenarioId for each unlocked seat so clients can update the correct scenario
                var seatsInDb = await _db.Seats.Where(s => seatIds.Contains(s.Id))
                    .Select(s => new { id = s.Id, code = s.Code, scenarioId = s.ScenarioId })
                    .ToListAsync();

                var unlocked = seatsInDb.Select(s => new { id = s.id, code = s.code, scenarioId = s.scenarioId }).ToList();
                var unlockedPayload = new { type = "SeatUnlocked", eventId = reservation.EventId, seats = unlocked };
                try
                {
                    await _hubContext.Clients.Group(reservation.EventId.ToString()).SendCoreAsync("SeatUnlocked", new object[] { unlockedPayload });
                }
                catch (Exception ex)
                {
                    try { Console.WriteLine("ReservationsController: SeatUnlocked broadcast error: " + ex.Message); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { Console.WriteLine("ReservationsController: SeatUnlocked payload build error: " + ex.Message); } catch { }
            }
        }
        catch { }

        return NoContent();
    }
}
