using System;

namespace AuthUser.Api.Domain.Entities;

public class Profile
{
 public Guid Id { get; set; }
 public string Username { get; set; } = string.Empty;
 public string Email { get; set; } = string.Empty;
 public string? KeycloakId { get; set; }
 public string? Roles { get; set; } // comma-separated list of realm roles
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
