using Microsoft.AspNetCore.Mvc;
using AuthUser.Api.Application.Commands;
using MediatR;
using AuthUser.Api.Domain.Entities;
using AuthUser.Api.Domain.Repositories;
using AuthUser.Api.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace AuthUser.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
 private readonly IMediator _mediator;
 private readonly IProfileRepository _repo;
 private readonly ILogger<ProfilesController> _logger;

 public ProfilesController(IMediator mediator, IProfileRepository repo, ILogger<ProfilesController> logger)
 {
 _mediator = mediator;
 _repo = repo;
 _logger = logger;
 }
 [HttpPost]
 public async Task<IActionResult> Create([FromBody] CreateProfileCommand command, CancellationToken cancellationToken)
 {
    _logger?.LogInformation("Create: received request for Username={Username}, Email={Email}", command.Username, command.Email);
    // provision in Keycloak first
    // use provided password if present, otherwise generate a random one for initial provisioning
    var pwd = !string.IsNullOrWhiteSpace(command.Password)
        ? command.Password
        : Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));
    _logger?.LogDebug("Create: using password (length)={Len}", pwd?.Length ?? 0);
    // Keycloak admin client configured via DI
    var keycloakClient = HttpContext.RequestServices.GetService<KeycloakAdminService>();
    _logger?.LogInformation("Create: resolved keycloakClient present={Present}", keycloakClient != null);
    string? kcId = null;
    if (keycloakClient != null)
    {
        _logger?.LogInformation("Create: calling KeycloakAdminService.CreateUserAsync for {Username}", command.Username);
        kcId = await keycloakClient.CreateUserAsync(command.Username, command.Email, pwd, cancellationToken);
        _logger?.LogInformation("Create: Keycloak create returned id={KcId}", kcId ?? "(null)");
       // assign roles if provided
       if (kcId != null && command.Roles != null && command.Roles.Length > 0)
       {
           try {
               _logger?.LogInformation("Create: assigning roles {Roles} to kcId={KcId}", string.Join(',', command.Roles), kcId);
               await keycloakClient.AssignRealmRolesAsync(kcId, command.Roles, cancellationToken);
               _logger?.LogInformation("Create: roles assigned to kcId={KcId}", kcId);
           } catch (Exception ex) { _logger?.LogWarning(ex, "Create: exception assigning roles to kcId={KcId}", kcId); }
       }
    }

    _logger?.LogInformation("Create: saving profile to DB via mediator");
    var id = await _mediator.Send(command, cancellationToken);
    _logger?.LogInformation("Create: mediator returned id={Id}", id);
    var profile = await _repo.GetByIdAsync(id);
    if (profile != null && kcId != null)
    {
        profile.KeycloakId = kcId;
        // persist update
        if (_repo is AuthUser.Api.Infrastructure.Persistence.ProfileRepository p)
        {
            _logger?.LogInformation("Create: updating profile in concrete repository");
            await p.UpdateAsync(profile);
        }
        else
        {
            // fallback: save by adding (in-memory)
            _logger?.LogInformation("Create: updating profile via generic repo.AddAsync fallback");
            await _repo.AddAsync(profile);
        }
    }
    // Return created profile and the initially generated password (useful for dev/testing).
    // In production prefer emailing a reset link instead of returning raw passwords.
    var response = new {
        profile,
        password = pwd
    };
    return CreatedAtAction(nameof(Get), new { id }, response);
 }

 [HttpGet("{id}")]
 public async Task<IActionResult> Get(Guid id)
 {
 var p = await _repo.GetByIdAsync(id);
 if (p == null) return NotFound();
 return Ok(p);
 }
}
