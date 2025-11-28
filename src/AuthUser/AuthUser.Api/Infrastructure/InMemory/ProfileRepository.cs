using AuthUser.Api.Domain.Entities;
using AuthUser.Api.Domain.Repositories;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuthUser.Api.Infrastructure.InMemory;

public class ProfileRepository : IProfileRepository
{
    private readonly List<Profile> _store = new();
    public Task<Profile> AddAsync(Profile p)
    {
        _store.Add(p);
        return Task.FromResult(p);
    }
    public Task<Profile?> GetByIdAsync(Guid id) => Task.FromResult(_store.FirstOrDefault(x => x.Id == id));
    public Task<Profile?> GetByKeycloakIdAsync(string keycloakId) => Task.FromResult(_store.FirstOrDefault(x => x.KeycloakId == keycloakId));
    public Task<Profile> UpdateAsync(Profile p)
    {
        var idx = _store.FindIndex(x => x.Id == p.Id);
        if (idx >= 0) _store[idx] = p;
        return Task.FromResult(p);
    }
}
