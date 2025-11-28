namespace Reservations.Api.Domain.Entities;

public class Reservation
{
 public Guid Id { get; set; }
 public Guid EventId { get; set; }
 public Guid ScenarioId { get; set; }
 public Guid SeatId { get; set; }
 public string Status { get; set; } = "Pending";
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
