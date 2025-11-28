using Microsoft.EntityFrameworkCore;
using Events.Api.Domain.Entities;

namespace Events.Api.Infrastructure.Persistence;

public class EventsDbContext : DbContext
{
 public EventsDbContext(DbContextOptions<EventsDbContext> options) : base(options) { }
 public DbSet<EventEntity> Events { get; set; } = null!;
 public DbSet<Scenario> Scenarios { get; set; } = null!;
 public DbSet<Seat> Seats { get; set; } = null!;
 public DbSet<Events.Api.Domain.Entities.Reservation> Reservations { get; set; } = null!;


 protected override void OnModelCreating(ModelBuilder modelBuilder)
 {
 base.OnModelCreating(modelBuilder);
 modelBuilder.Entity<EventEntity>(b =>
 {
 b.HasKey(x => x.Id);
 b.Property(x => x.Name).IsRequired();
        b.Property(x => x.OrganizerKeycloakId).IsRequired(false);
        b.Property(x => x.Description).HasColumnType("text").IsRequired(false);
        b.Property(x => x.Place).HasColumnType("text").IsRequired(false);
        b.Property(x => x.EventType).HasColumnType("text").IsRequired(false);
 });
    modelBuilder.Entity<Scenario>(b =>
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired();
        b.HasMany(x => x.Seats).WithOne().HasForeignKey(s => s.ScenarioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne< Events.Api.Domain.Entities.EventEntity >().WithMany(e => e.Scenarios).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
    });

    modelBuilder.Entity<Seat>(b =>
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired();
        b.Property(x => x.IsAvailable).IsRequired();
        b.Property(x => x.LockOwner).IsRequired(false);
        b.Property(x => x.LockExpiresAt).IsRequired(false);
        b.Property(x => x.Type).HasColumnType("text").IsRequired(false);
        b.Property(x => x.Price).HasColumnType("numeric(10,2)").IsRequired();
    });

    modelBuilder.Entity<Events.Api.Domain.Entities.Reservation>(b =>
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.UserKeycloakId).HasColumnType("text").IsRequired();
        b.Property(x => x.EventId).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.Status).HasColumnType("text").IsRequired();
        b.HasMany<Events.Api.Domain.Entities.ReservationSeat>().WithOne().HasForeignKey(s => s.ReservationId).OnDelete(DeleteBehavior.Cascade);
    });

    modelBuilder.Entity<Events.Api.Domain.Entities.ReservationSeat>(b =>
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.SeatId).IsRequired();
        b.Property(x => x.Code).HasColumnType("text").IsRequired();
        b.Property(x => x.Type).HasColumnType("text").IsRequired(false);
        b.Property(x => x.Price).HasColumnType("numeric(10,2)").IsRequired();
    });
 }
}
