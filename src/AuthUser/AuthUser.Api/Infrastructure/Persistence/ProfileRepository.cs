using AuthUser.Api.Domain.Entities;
using AuthUser.Api.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AuthUser.Api.Infrastructure.Persistence;

public class ProfileRepository : IProfileRepository
{
 private readonly AuthUserDbContext _db;
 public ProfileRepository(AuthUserDbContext db) { _db = db; }
 public async Task<Profile> AddAsync(Profile profile)
 {
 _db.Profiles.Add(profile);
 await _db.SaveChangesAsync();
 return profile;
 }
 public async Task<Profile> UpdateAsync(Profile profile)
 {
     _db.Profiles.Update(profile);
     await _db.SaveChangesAsync();
     return profile;
 }
 public Task<Profile?> GetByIdAsync(Guid id) => _db.Profiles.FindAsync(id).AsTask();
 public Task<Profile?> GetByKeycloakIdAsync(string keycloakId) => _db.Profiles.FirstOrDefaultAsync(p => p.KeycloakId == keycloakId);
}
