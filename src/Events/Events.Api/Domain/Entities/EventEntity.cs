using System;

namespace Events.Api.Domain.Entities;

public class EventEntity
{
 public Guid Id { get; set; }
 public string Name { get; set; } = string.Empty;
 public DateTime Date { get; set; }
    public string? OrganizerKeycloakId { get; set; }
    public List<Scenario> Scenarios { get; set; } = new();
    public string? Description { get; set; }
    public string? Place { get; set; }
    public string? EventType { get; set; }
}

public class Scenario
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Seat> Seats { get; set; } = new();
}

public class Seat
{
    public Guid Id { get; set; }
    public Guid ScenarioId { get; set; }
    public string Code { get; set; } = string.Empty; // e.g., A1, B5
    public bool IsAvailable { get; set; } = true;
    public Guid? LockOwner { get; set; }
    public DateTime? LockExpiresAt { get; set; }
    public string Type { get; set; } = "General"; // e.g., VIP, General
    public decimal Price { get; set; } = 0m;
}
