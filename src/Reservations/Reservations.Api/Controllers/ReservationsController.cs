using Microsoft.AspNetCore.Mvc;
using Hangfire;
using Reservations.Api.Domain.Repositories;
using Reservations.Api.Domain.Entities;
using System.Net.Http.Json;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Reservations.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
 private readonly IBackgroundJobClient _jobs;
 private readonly IReservationRepository _repo;
 private readonly IHttpClientFactory _http;
 private readonly IServiceProvider _sp;
 public ReservationsController(IBackgroundJobClient jobs, IReservationRepository repo, IHttpClientFactory http, IServiceProvider sp)
 {
 _jobs = jobs;
 _repo = repo;
 _http = http;
 _sp = sp;
 }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "UsuarioAutenticado")]

 [HttpPost]
 public async Task<IActionResult> Create([FromBody] CreateReservationRequest payload)
 {
     // basic validation
     if (payload == null || payload.EventId == Guid.Empty || payload.SeatId == Guid.Empty)
         return BadRequest("eventId and seatId required");
        // generate reservation id and attempt to lock seat first
        var reservationId = Guid.NewGuid();
        try
        {
            var client = _http.CreateClient("events");
            var res = await client.PostAsJsonAsync($"api/events/{payload.EventId}/scenarios/{payload.ScenarioId}/seats/{payload.SeatId}/lock", new { ReservationId = reservationId });
            if (!res.IsSuccessStatusCode)
            {
                return Conflict("Seat could not be locked");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return StatusCode(500, "Error contacting events service");
        }

        // lock succeeded -> create reservation and publish
        var r = new Reservation { Id = reservationId, EventId = payload.EventId, ScenarioId = payload.ScenarioId, SeatId = payload.SeatId, Status = "Pending" };
        await _repo.AddAsync(r);

        // publish RabbitMQ event: ReservationCreated
        try
        {
            // Usamos dynamic para evitar dependencias de tipos en tiempo de compilación
            var factory = _sp.GetRequiredService<object>();
            dynamic conn = ((dynamic)factory).CreateConnection();
            using dynamic channel = conn.CreateModel();
            channel.ExchangeDeclare(exchange: "plataforma.events", type: ExchangeType.Fanout, durable: true);
            var message = JsonSerializer.Serialize(new { type = "ReservationCreated", reservationId = r.Id, eventId = r.EventId, status = r.Status });
            var body = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish(exchange: "plataforma.events", routingKey: "", basicProperties: null, body: body);
        }
        catch (Exception ex)
        {
            Console.WriteLine("RabbitMQ publish error: " + ex.Message);
        }

        // notify Events.Api so it can broadcast to SignalR clients (local dev sync)
        try
        {
            var clientNotify = _http.CreateClient("events");
            var seats = new[] { new { seatId = r.SeatId, scenarioId = r.ScenarioId } };
            await clientNotify.PostAsJsonAsync("api/reservations/notify", new { type = "ReservationCreated", reservationId = r.Id, eventId = r.EventId, seats });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Notify Events ReservationCreated error: " + ex.Message);
        }

        // schedule expiration in 15 minutes
        _jobs.Schedule(() => ExpireReservation(r.Id), TimeSpan.FromMinutes(15));
        return Accepted(new { id = r.Id, status = r.Status });
 }

public record CreateReservationRequest(Guid EventId, Guid ScenarioId, Guid SeatId);

 public async Task ExpireReservation(Guid id)
 {
 var r = await _repo.GetByIdAsync(id);
 if (r == null) return;
 r.Status = "Expired";
 await _repo.UpdateAsync(r);
 Console.WriteLine($"Reserva {id} expirada");
 
 // publish RabbitMQ event: ReservationExpired
     try
 {
     var factory = _sp.GetRequiredService<object>();
     dynamic conn = ((dynamic)factory).CreateConnection();
     using dynamic channel = conn.CreateModel();
     channel.ExchangeDeclare(exchange: "plataforma.events", type: ExchangeType.Fanout, durable: true);
     var message = JsonSerializer.Serialize(new { type = "ReservationExpired", reservationId = r.Id, eventId = r.EventId, status = r.Status });
     var body = Encoding.UTF8.GetBytes(message);
     channel.BasicPublish(exchange: "plataforma.events", routingKey: "", basicProperties: null, body: body);
 }
 catch (Exception ex)
 {
     Console.WriteLine("RabbitMQ publish error in expiration: " + ex.Message);
 }
 
 // call Events service to unlock seats for this reservation
 try
 {
     var client = _http.CreateClient("events");
     var res = await client.PostAsJsonAsync($"api/events/{r.EventId}/scenarios/{r.ScenarioId}/seats/unlock", new { ReservationId = r.Id });
     if (!res.IsSuccessStatusCode)
     {
         Console.WriteLine($"Unlock call returned {res.StatusCode}");
     }

    // notify Events.Api about expiration so it can broadcast cancellation/seat-unlock
    try
    {
        var clientNotify = _http.CreateClient("events");
        var seats = new[] { new { seatId = r.SeatId, scenarioId = r.ScenarioId } };
        await clientNotify.PostAsJsonAsync("api/reservations/notify", new { type = "ReservationExpired", reservationId = r.Id, eventId = r.EventId, seats });
    }
    catch (Exception ex)
    {
        Console.WriteLine("Notify Events ReservationExpired error: " + ex.Message);
    }
 }
 catch (Exception ex)
 {
     Console.WriteLine("Error calling Events unlock: " + ex.Message);
 }
 }

 [HttpDelete("{reservationId}")]
 public async Task<IActionResult> CancelReservation(Guid reservationId)
 {
     var r = await _repo.GetByIdAsync(reservationId);
     if (r == null) return NotFound();
     r.Status = "Cancelled";
     await _repo.UpdateAsync(r);

     // publish RabbitMQ event so other services can react if a bus is available
     try
     {
         var factory = _sp.GetRequiredService<object>();
         dynamic conn = ((dynamic)factory).CreateConnection();
         using dynamic channel = conn.CreateModel();
         channel.ExchangeDeclare(exchange: "plataforma.events", type: ExchangeType.Fanout, durable: true);
         var message = JsonSerializer.Serialize(new { type = "ReservationCancelled", reservationId = r.Id, eventId = r.EventId, status = r.Status });
         var body = Encoding.UTF8.GetBytes(message);
         channel.BasicPublish(exchange: "plataforma.events", routingKey: "", basicProperties: null, body: body);
     }
     catch { }

     // notify Events.Api so it can broadcast cancellation/unlock
     try
     {
         var clientNotify = _http.CreateClient("events");
         var seats = new[] { new { seatId = r.SeatId, scenarioId = r.ScenarioId } };
         await clientNotify.PostAsJsonAsync("api/reservations/notify", new { type = "ReservationCancelled", reservationId = r.Id, eventId = r.EventId, seats });
     }
     catch (Exception ex)
     {
         Console.WriteLine("Notify Events ReservationCancelled error: " + ex.Message);
     }

     return NoContent();
 }
}
