using Reservations.Api.Domain.Entities;

namespace Reservations.Api.Domain.Repositories;

public interface IReservationRepository
{
 Task<Reservation> AddAsync(Reservation r);
 Task<Reservation?> GetByIdAsync(Guid id);
 Task UpdateAsync(Reservation r);
}
