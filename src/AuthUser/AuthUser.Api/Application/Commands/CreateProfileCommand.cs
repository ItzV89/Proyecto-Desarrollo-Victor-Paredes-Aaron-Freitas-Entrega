using MediatR;

namespace AuthUser.Api.Application.Commands;

public record CreateProfileCommand(string Username, string Email, string[]? Roles, string? Password = null) : IRequest<Guid>;
