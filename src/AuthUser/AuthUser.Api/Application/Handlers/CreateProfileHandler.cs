using MediatR;
using AuthUser.Api.Application.Commands;
using AuthUser.Api.Domain.Repositories;
using AuthUser.Api.Domain.Entities;

namespace AuthUser.Api.Application.Handlers;

public class CreateProfileHandler : IRequestHandler<CreateProfileCommand, Guid>
{
 private readonly IProfileRepository _repo;
 public CreateProfileHandler(IProfileRepository repo) { _repo = repo; }
 public async Task<Guid> Handle(CreateProfileCommand request, CancellationToken cancellationToken)
 {
	 var profile = new Profile
	 {
		 Id = Guid.NewGuid(),
		 Username = request.Username,
		 Email = request.Email,
		 Roles = request.Roles != null && request.Roles.Length > 0 ? string.Join(',', request.Roles) : null
	 };
	 await _repo.AddAsync(profile);
	 return profile.Id;
 }
}
