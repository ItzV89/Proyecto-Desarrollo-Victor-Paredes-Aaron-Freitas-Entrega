using Microsoft.EntityFrameworkCore;
using Reservations.Api.Domain.Entities;

namespace Reservations.Api.Infrastructure.Persistence;

public class ReservationsDbContext : DbContext
{
 public ReservationsDbContext(DbContextOptions<ReservationsDbContext> options) : base(options) { }
 public DbSet<Reservation> Reservations { get; set; } = null!;

 protected override void OnModelCreating(ModelBuilder modelBuilder)
 {
 base.OnModelCreating(modelBuilder);
 modelBuilder.Entity<Reservation>(b =>
 {
 b.HasKey(x => x.Id);
 b.Property(x => x.Status).IsRequired();
 });
 }
}
