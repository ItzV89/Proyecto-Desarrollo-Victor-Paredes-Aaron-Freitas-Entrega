using Reservations.Api.Domain.Entities;
using Reservations.Api.Domain.Repositories;

namespace Reservations.Api.Infrastructure.Persistence;

public class ReservationRepository : IReservationRepository
{
 private readonly ReservationsDbContext _db;
 public ReservationRepository(ReservationsDbContext db) { _db = db; }
 public async Task<Reservation> AddAsync(Reservation r)
 {
 _db.Reservations.Add(r);
 await _db.SaveChangesAsync();
 return r;
 }
 public Task<Reservation?> GetByIdAsync(Guid id) => _db.Reservations.FindAsync(id).AsTask();
 public async Task UpdateAsync(Reservation r)
 {
 _db.Reservations.Update(r);
 await _db.SaveChangesAsync();
 }
}
