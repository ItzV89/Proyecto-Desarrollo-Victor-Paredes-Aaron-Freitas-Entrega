using AuthUser.Api.Domain.Entities;

namespace AuthUser.Api.Domain.Repositories;

public interface IProfileRepository
{
 Task<Profile> AddAsync(Profile profile);
 Task<Profile?> GetByIdAsync(Guid id);
 Task<Profile?> GetByKeycloakIdAsync(string keycloakId);
}
