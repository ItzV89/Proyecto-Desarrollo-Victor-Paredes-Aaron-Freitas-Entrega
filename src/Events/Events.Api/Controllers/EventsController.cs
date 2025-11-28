using Microsoft.AspNetCore.Mvc;
using Events.Api.Infrastructure.Persistence;
using Events.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using System.Linq;

namespace Events.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly EventsDbContext _db;
    private readonly Events.Api.Infrastructure.Services.IEventBus _eventBus;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<Events.Api.Hubs.SeatHub> _hubContext;

    public EventsController(EventsDbContext db, Events.Api.Infrastructure.Services.IEventBus eventBus, Microsoft.AspNetCore.SignalR.IHubContext<Events.Api.Hubs.SeatHub> hubContext)
    {
        _db = db;
        _eventBus = eventBus;
        _hubContext = hubContext;
    }

    [HttpGet("protected")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "UsuarioAutenticado")]
    public IActionResult Protected() => Ok(new { message = "Autenticado" });

 [HttpGet]
 public IActionResult GetAll()
 {
 var list = _db.Events.Take(50).Select(e => new { e.Id, e.Name, e.Date }).ToList();
 return Ok(list);
 }

    public record SeatTypeDto(string Name, int Quantity, decimal? Price);
    public record CreateEventDto(string Name, DateTime Date, List<SeatTypeDto>? SeatTypes, string? Description, string? Place, string? EventType);

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "UsuarioAutenticado")]
    public async Task<IActionResult> Create([FromBody] CreateEventDto? payload)
    {
        if (payload == null) return BadRequest();
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var ev = new Events.Api.Domain.Entities.EventEntity
        {
            Id = Guid.NewGuid(),
            Name = payload.Name,
            Date = payload.Date,
            OrganizerKeycloakId = sub
        };
        ev.Description = payload.Description;
        ev.Place = payload.Place;
        ev.EventType = payload.EventType;
        _db.Events.Add(ev);

        // create default scenario and seats based on provided SeatTypes
        var sc = new Scenario { Id = Guid.NewGuid(), EventId = ev.Id, Name = "General" };
        if (payload.SeatTypes == null || payload.SeatTypes.Count == 0)
        {
            // fallback: 5 rows x 10 cols
            for (int r = 1; r <= 5; r++)
            {
                for (int c = 1; c <= 10; c++)
                {
                    sc.Seats.Add(new Seat { Id = Guid.NewGuid(), ScenarioId = sc.Id, Code = $"{(char)('A' + r - 1)}{c}", IsAvailable = true, Type = "General", Price = 0m });
                }
            }
        }
        else
        {
            foreach (var t in payload.SeatTypes)
            {
                var baseName = string.IsNullOrWhiteSpace(t.Name) ? "T" : t.Name.Trim();
                for (int i = 1; i <= Math.Max(0, t.Quantity); i++)
                {
                    sc.Seats.Add(new Seat { Id = Guid.NewGuid(), ScenarioId = sc.Id, Code = $"{baseName}-{i}", IsAvailable = true, Type = baseName, Price = t.Price ?? 0m });
                }
            }
        }

        _db.Scenarios.Add(sc);
        await _db.SaveChangesAsync();
        // broadcast EventCreated to all clients so UIs refresh automatically
        try
        {
            var evtPayload = new { type = "EventCreated", eventInfo = new { ev.Id, ev.Name, ev.Date, ev.Description, ev.Place, ev.EventType, ev.OrganizerKeycloakId } };
            try
            {
                Console.WriteLine("EventsController: broadcasting EventCreated: " + System.Text.Json.JsonSerializer.Serialize(evtPayload));
                await _hubContext.Clients.All.SendCoreAsync("EventCreated", new object[] { evtPayload });
            }
            catch (Exception ex)
            {
                Console.WriteLine("EventsController: EventCreated broadcast error: " + ex.Message);
            }
        }
        catch { }

        return CreatedAtAction(nameof(GetById), new { id = ev.Id }, new { ev.Id, ev.Name, ev.Date, ev.OrganizerKeycloakId });
    }

    [HttpGet("my")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "UsuarioAutenticado")]
    public async Task<IActionResult> GetMy()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub)) return Forbid();
        var list = await _db.Events.Where(e => e.OrganizerKeycloakId == sub).OrderByDescending(e => e.Date).Select(e => new { e.Id, e.Name, e.Date, e.Description, e.Place, e.EventType }).ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var ev = await _db.Events.Where(e => e.Id == id).Select(e => new
        {
            e.Id,
            e.Name,
            e.Date,
            e.OrganizerKeycloakId,
            e.Description,
            e.Place,
            e.EventType,
            Scenarios = e.Scenarios.Select(s => new
            {
                s.Id,
                s.Name,
                Seats = s.Seats.Select(se => new { se.Id, se.Code, se.Type, se.Price, se.IsAvailable, se.LockOwner, se.LockExpiresAt })
            })
        }).FirstOrDefaultAsync();
        if (ev == null) return NotFound();
        return Ok(ev);
    }

    [HttpPut("{id}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "UsuarioAutenticado")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateEventDto? payload)
    {
        if (payload == null) return BadRequest();
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound();
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (ev.OrganizerKeycloakId != sub) return Forbid();
        ev.Name = payload.Name;
        ev.Date = payload.Date;
        ev.Description = payload.Description;
        ev.Place = payload.Place;
        ev.EventType = payload.EventType;
        // if SeatTypes provided, replace scenarios/seats with new ones
        if (payload.SeatTypes != null)
        {
            var existingScenarios = _db.Scenarios.Where(s => s.EventId == ev.Id).Include(s => s.Seats).ToList();
            if (existingScenarios.Any())
            {
                _db.Seats.RemoveRange(existingScenarios.SelectMany(s => s.Seats));
                _db.Scenarios.RemoveRange(existingScenarios);
            }

            var sc = new Scenario { Id = Guid.NewGuid(), EventId = ev.Id, Name = "General" };
            foreach (var t in payload.SeatTypes)
            {
                var baseName = string.IsNullOrWhiteSpace(t.Name) ? "T" : t.Name.Trim();
                for (int i = 1; i <= Math.Max(0, t.Quantity); i++)
                {
                    sc.Seats.Add(new Seat { Id = Guid.NewGuid(), ScenarioId = sc.Id, Code = $"{baseName}-{i}", IsAvailable = true });
                }
            }
            _db.Scenarios.Add(sc);
        }

        await _db.SaveChangesAsync();
        // broadcast EventUpdated so clients update listing
        try
        {
            var evtPayload = new { type = "EventUpdated", eventInfo = new { ev.Id, ev.Name, ev.Date, ev.Description, ev.Place, ev.EventType } };
            try
            {
                Console.WriteLine("EventsController: broadcasting EventUpdated: " + System.Text.Json.JsonSerializer.Serialize(evtPayload));
                await _hubContext.Clients.All.SendCoreAsync("EventUpdated", new object[] { evtPayload });
            }
            catch (Exception ex)
            {
                Console.WriteLine("EventsController: EventUpdated broadcast error: " + ex.Message);
            }
        }
        catch { }
        return Ok(new { ev.Id, ev.Name, ev.Date });
    }

    [HttpDelete("{id}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "UsuarioAutenticado")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound();
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (ev.OrganizerKeycloakId != sub) return Forbid();
        // Before deleting event, cancel reservations and release seats for that event
        try
        {
            var reservations = _db.Reservations.Include(r => r.Seats).Where(r => r.EventId == ev.Id).ToList();
            foreach (var r in reservations)
            {
                var seatIds = r.Seats.Select(s => s.SeatId).ToList();
                if (seatIds.Count > 0)
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE ""Seats""
                        SET ""IsAvailable"" = TRUE, ""LockOwner"" = NULL, ""LockExpiresAt"" = NULL
                        WHERE ""Id"" = ANY({seatIds})
                    ");
                }
                r.Status = "Cancelled";
                _db.Reservations.Update(r);

                // broadcast reservation cancelled per event group
                try
                {
                    var seatsPayload = r.Seats.Select(s => new { s.SeatId, s.Code, s.Type, s.Price }).ToList();
                    var broadcast = new { type = "ReservationCancelled", reservationId = r.Id, eventId = r.EventId, seats = seatsPayload };
                    await _hubContext.Clients.Group(r.EventId.ToString()).SendCoreAsync("ReservationCancelled", new object[] { broadcast });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("EventsController: ReservationCancelled broadcast error: " + ex.Message);
                }

                // broadcast SeatUnlocked
                try
                {
                    var seatsInDb = await _db.Seats.Where(s => seatIds.Contains(s.Id)).Select(s => new { id = s.Id, code = s.Code, scenarioId = s.ScenarioId }).ToListAsync();
                    var unlockedPayload = new { type = "SeatUnlocked", eventId = ev.Id, seats = seatsInDb };
                    await _hubContext.Clients.Group(ev.Id.ToString()).SendCoreAsync("SeatUnlocked", new object[] { unlockedPayload });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("EventsController: SeatUnlocked broadcast error during delete: " + ex.Message);
                }
            }
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("EventsController: error cancelling reservations on delete: " + ex.Message);
        }

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        // broadcast EventDeleted to all clients
        try
        {
            var payload = new { type = "EventDeleted", eventId = ev.Id };
            Console.WriteLine("EventsController: broadcasting EventDeleted: " + System.Text.Json.JsonSerializer.Serialize(payload));
            await _hubContext.Clients.All.SendCoreAsync("EventDeleted", new object[] { payload });
        }
        catch (Exception ex)
        {
            Console.WriteLine("EventsController: EventDeleted broadcast error: " + ex.Message);
        }

        return NoContent();
    }

    [HttpPost("{eventId}/scenarios")]
    public async Task<IActionResult> CreateScenario(Guid eventId, [FromBody] string name)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return NotFound();
        var sc = new Scenario { Id = Guid.NewGuid(), EventId = eventId, Name = name };
        // add default seats for demo
        for (int r = 1; r <= 5; r++)
        {
            for (int c = 1; c <= 10; c++)
            {
                sc.Seats.Add(new Seat { Id = Guid.NewGuid(), ScenarioId = sc.Id, Code = $"{(char)('A' + r - 1)}{c}", IsAvailable = true, LockOwner = null, LockExpiresAt = null });
            }
        }
        _db.Scenarios.Add(sc);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetScenarioSeats), new { eventId, scenarioId = sc.Id }, sc);
    }

    [HttpGet("{eventId}/scenarios/{scenarioId}/seats")]
    public IActionResult GetScenarioSeats(Guid eventId, Guid scenarioId)
    {
        var sc = _db.Scenarios.Where(s => s.Id == scenarioId && s.EventId == eventId).Select(s => new
        {
            s.Id,
            s.Name,
            Seats = s.Seats.Select(se => new { se.Id, se.Code, se.Type, se.Price, se.IsAvailable, se.LockOwner, se.LockExpiresAt })
        }).FirstOrDefault();
        if (sc == null) return NotFound();
        return Ok(sc);
    }

    public record LockRequest(Guid ReservationId);

    [HttpPost("{eventId}/scenarios/{scenarioId}/seats/{seatId}/lock")]
    public async Task<IActionResult> LockSeat(Guid eventId, Guid scenarioId, Guid seatId, [FromBody] LockRequest? payload)
    {
        if (payload == null || payload.ReservationId == Guid.Empty) return BadRequest("reservationId required");

        // attempt atomic conditional lock to avoid race conditions
        var reservationId = payload.ReservationId;
        var ttlMinutes = 15;
        var expiresAt = DateTime.UtcNow.AddMinutes(ttlMinutes);

        // SQL: update seat set IsAvailable=false, LockOwner=@reservationId, LockExpiresAt=@expires where Id=@seatId and ScenarioId=@scenarioId and (IsAvailable = true OR (LockExpiresAt IS NOT NULL AND LockExpiresAt < now()))
        var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Seats""
            SET ""IsAvailable"" = FALSE, ""LockOwner"" = {reservationId}, ""LockExpiresAt"" = {expiresAt}
            WHERE ""Id"" = {seatId} AND ""ScenarioId"" = {scenarioId} AND (""IsAvailable"" = TRUE OR (""LockExpiresAt"" IS NOT NULL AND ""LockExpiresAt"" < NOW()))
        ");

        if (affected == 0)
        {
            // fetch seat to return current state
            var current = _db.Seats.Where(s => s.Id == seatId && s.ScenarioId == scenarioId).Select(s => new { s.Id, s.Code, s.IsAvailable, s.LockOwner, s.LockExpiresAt }).FirstOrDefault();
            if (current == null) return NotFound();
            return Conflict(new { message = "Seat could not be locked", seat = current });
        }

        var seat = _db.Seats.Where(s => s.Id == seatId && s.ScenarioId == scenarioId).Select(s => new { s.Id, s.Code, s.Type, s.Price, s.IsAvailable, s.LockOwner, s.LockExpiresAt }).FirstOrDefault();

        // broadcast to connected clients that a seat was locked
        try
        {
            var payloadMsg = new { type = "SeatLocked", eventId = eventId, scenarioId = scenarioId, seat };
            try
            {
                Console.WriteLine($"EventsController: broadcasting SeatLocked to group {eventId} payload: {System.Text.Json.JsonSerializer.Serialize(payloadMsg)}");
                await _hubContext.Clients.Group(eventId.ToString()).SendCoreAsync("SeatLocked", new object[] { payloadMsg });
            }
            catch (Exception ex)
            {
                Console.WriteLine("EventsController: SeatLocked broadcast error: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("EventsController: SeatLocked outer error: " + ex.Message);
        }

        return Ok(seat);
    }

    [HttpPost("cleanup/locks")]
    public async Task<IActionResult> CleanupExpiredLocks()
    {
        var now = DateTime.UtcNow;
        var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Seats""
            SET ""IsAvailable"" = TRUE, ""LockOwner"" = NULL, ""LockExpiresAt"" = NULL
            WHERE ""LockExpiresAt"" IS NOT NULL AND ""LockExpiresAt"" < {now}
        ");
        return Ok(new { released = affected });
    }

    public record UnlockRequest(Guid ReservationId);
    public record RemoveSeatsDto(int Count);

    [HttpDelete("{eventId}/scenarios/{scenarioId}/seats/{seatId}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "UsuarioAutenticado")]
    public async Task<IActionResult> DeleteSeat(Guid eventId, Guid scenarioId, Guid seatId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return NotFound();
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (ev.OrganizerKeycloakId != sub) return Forbid();

        // ensure scenario belongs to event
        var scenario = await _db.Scenarios.FindAsync(scenarioId);
        if (scenario == null || scenario.EventId != eventId) return NotFound();

        var seat = await _db.Seats.Where(s => s.Id == seatId && s.ScenarioId == scenarioId).FirstOrDefaultAsync();
        if (seat == null) return NotFound();

        // find reservations that reference this seat
        var reservations = _db.Reservations.Include(r => r.Seats).Where(r => r.EventId == eventId && r.Seats.Any(rs => rs.SeatId == seatId)).ToList();

        foreach (var r in reservations)
        {
            try
            {
                var rSeatIds = r.Seats.Select(s => s.SeatId).ToList();
                if (rSeatIds.Any())
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE ""Seats""
                        SET ""IsAvailable"" = TRUE, ""LockOwner"" = NULL, ""LockExpiresAt"" = NULL
                        WHERE ""Id"" = ANY({rSeatIds})
                    ");
                }

                r.Status = "Cancelled";
                _db.Reservations.Update(r);

                // broadcast reservation cancelled
                try
                {
                    var seatsPayload = r.Seats.Select(s => new { s.SeatId, s.Code, s.Type, s.Price }).ToList();
                    var broadcast = new { type = "ReservationCancelled", reservationId = r.Id, eventId = r.EventId, seats = seatsPayload };
                    await _hubContext.Clients.Group(r.EventId.ToString()).SendCoreAsync("ReservationCancelled", new object[] { broadcast });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("EventsController: ReservationCancelled broadcast error in DeleteSeat: " + ex.Message);
                }

                // broadcast SeatUnlocked for seats released by this reservation (if any)
                try
                {
                    var seatsInDb = await _db.Seats.Where(s => rSeatIds.Contains(s.Id)).Select(s => new { id = s.Id, code = s.Code, scenarioId = s.ScenarioId }).ToListAsync();
                    var unlockedPayload = new { type = "SeatUnlocked", eventId = r.EventId, seats = seatsInDb };
                    await _hubContext.Clients.Group(r.EventId.ToString()).SendCoreAsync("SeatUnlocked", new object[] { unlockedPayload });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("EventsController: SeatUnlocked broadcast error in DeleteSeat: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("EventsController: error cancelling reservation in DeleteSeat: " + ex.Message);
            }
        }

        // remove the seat
        try
        {
            _db.Seats.Remove(seat);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("EventsController: error removing seat: " + ex.Message);
            return StatusCode(500, "error removing seat");
        }

        // broadcast SeatRemoved so clients remove seat from UI
        try
        {
            var removedPayload = new { type = "SeatRemoved", eventId = eventId, scenarioId = scenarioId, seats = new[] { new { id = seat.Id, code = seat.Code } } };
            await _hubContext.Clients.Group(eventId.ToString()).SendCoreAsync("SeatRemoved", new object[] { removedPayload });
        }
        catch (Exception ex)
        {
            Console.WriteLine("EventsController: SeatRemoved broadcast error in DeleteSeat: " + ex.Message);
        }

        return Ok(new { removed = 1 });
    }

    [HttpPost("{eventId}/scenarios/{scenarioId}/seats/remove")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "UsuarioAutenticado")]
    public async Task<IActionResult> RemoveSeats(Guid eventId, Guid scenarioId, [FromBody] RemoveSeatsDto? payload)
    {
        if (payload == null || payload.Count <= 0) return BadRequest("count required");

        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return NotFound();
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (ev.OrganizerKeycloakId != sub) return Forbid();

        // ensure scenario belongs to event
        var scenario = await _db.Scenarios.FindAsync(scenarioId);
        if (scenario == null || scenario.EventId != eventId) return NotFound();

        // pick seats to remove (choose last by Code for simplicity)
        var seatsToRemove = _db.Seats.Where(s => s.ScenarioId == scenarioId).OrderByDescending(s => s.Code).Take(payload.Count).ToList();
        if (!seatsToRemove.Any()) return Ok(new { removed = 0 });

        var seatIds = seatsToRemove.Select(s => s.Id).ToList();

        // find reservations that reference any of these seats
        var reservations = _db.Reservations.Include(r => r.Seats).Where(r => r.EventId == eventId && r.Seats.Any(rs => seatIds.Contains(rs.SeatId))).ToList();

        foreach (var r in reservations)
        {
            try
            {
                // release all seats for this reservation
                var rSeatIds = r.Seats.Select(s => s.SeatId).ToList();
                if (rSeatIds.Any())
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE ""Seats""
                        SET ""IsAvailable"" = TRUE, ""LockOwner"" = NULL, ""LockExpiresAt"" = NULL
                        WHERE ""Id"" = ANY({rSeatIds})
                    ");
                }

                r.Status = "Cancelled";
                _db.Reservations.Update(r);

                // broadcast reservation cancelled and unlocked seats
                try
                {
                    var seatsPayload = r.Seats.Select(s => new { s.SeatId, s.Code, s.Type, s.Price }).ToList();
                    var broadcast = new { type = "ReservationCancelled", reservationId = r.Id, eventId = r.EventId, seats = seatsPayload };
                    await _hubContext.Clients.Group(r.EventId.ToString()).SendCoreAsync("ReservationCancelled", new object[] { broadcast });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("EventsController: ReservationCancelled broadcast error in RemoveSeats: " + ex.Message);
                }

                try
                {
                    var seatsInDb = await _db.Seats.Where(s => rSeatIds.Contains(s.Id)).Select(s => new { id = s.Id, code = s.Code, scenarioId = s.ScenarioId }).ToListAsync();
                    var unlockedPayload = new { type = "SeatUnlocked", eventId = r.EventId, seats = seatsInDb };
                    await _hubContext.Clients.Group(r.EventId.ToString()).SendCoreAsync("SeatUnlocked", new object[] { unlockedPayload });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("EventsController: SeatUnlocked broadcast error in RemoveSeats: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("EventsController: error cancelling reservation in RemoveSeats: " + ex.Message);
            }
        }

        // remove seat rows
        try
        {
            _db.Seats.RemoveRange(seatsToRemove);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("EventsController: error removing seats: " + ex.Message);
            return StatusCode(500, "error removing seats");
        }

        // broadcast SeatRemoved so clients remove seats from UI
        try
        {
            var removedPayload = new { type = "SeatRemoved", eventId = eventId, scenarioId = scenarioId, seats = seatsToRemove.Select(s => new { id = s.Id, code = s.Code }) };
            await _hubContext.Clients.Group(eventId.ToString()).SendCoreAsync("SeatRemoved", new object[] { removedPayload });
        }
        catch (Exception ex)
        {
            Console.WriteLine("EventsController: SeatRemoved broadcast error: " + ex.Message);
        }

        return Ok(new { removed = seatIds.Count });
    }

    [HttpPost("{eventId}/scenarios/{scenarioId}/seats/unlock")]
    public async Task<IActionResult> UnlockSeatsByReservation(Guid eventId, Guid scenarioId, [FromBody] UnlockRequest? payload)
    {
        if (payload == null || payload.ReservationId == Guid.Empty) return BadRequest("reservationId required");

        // return list of seats released so we can publish events if needed
        var seats = await _db.Seats.Where(s => s.ScenarioId == scenarioId && s.LockOwner == payload.ReservationId).Select(s => new { s.Id, s.Code }).ToListAsync();
        if (seats.Count == 0) return Ok(new { released = 0 });

        var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Seats""
            SET ""IsAvailable"" = TRUE, ""LockOwner"" = NULL, ""LockExpiresAt"" = NULL
            WHERE ""ScenarioId"" = {scenarioId} AND ""LockOwner"" = {payload.ReservationId}
        ");

        // publish SeatUnlocked events and broadcast via SignalR
        try
        {
            foreach (var seat in seats)
            {
                var payloadMsg = System.Text.Json.JsonSerializer.Serialize(new { type = "SeatUnlocked", seatId = seat.Id, scenarioId = scenarioId, code = seat.Code, reservationId = payload.ReservationId });
                try { _eventBus?.Publish("plataforma.events", payloadMsg); } catch { }
            }

            // broadcast unlocked seats
            var broadcast = new { type = "SeatUnlocked", eventId = eventId, scenarioId = scenarioId, seats };
            try
            {
                Console.WriteLine($"EventsController: broadcasting SeatUnlocked to group {eventId} payload: {System.Text.Json.JsonSerializer.Serialize(broadcast)}");
                await _hubContext.Clients.Group(eventId.ToString()).SendCoreAsync("SeatUnlocked", new object[] { broadcast });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Publish/Hub SeatUnlocked error: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Publish/Hub SeatUnlocked error: " + ex.Message);
        }

        return Ok(new { released = affected });
    }
}
