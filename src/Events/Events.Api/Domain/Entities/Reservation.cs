using System;
using System.Collections.Generic;

namespace Events.Api.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; }
    public string UserKeycloakId { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Confirmed"; // Confirmed | Cancelled
    public List<ReservationSeat> Seats { get; set; } = new();
}

public class ReservationSeat
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public Guid SeatId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Type { get; set; }
    public decimal Price { get; set; }
}
