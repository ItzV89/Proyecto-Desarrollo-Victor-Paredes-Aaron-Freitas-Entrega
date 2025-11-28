using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthUser.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
 [HttpGet("me")]
 [Authorize(Policy = "UsuarioAutenticado")]
 public IActionResult Me()
 {
 var name = User?.Identity?.Name ?? "anonymous";
 return Ok(new { user = name, claims = User?.Claims?.Select(c => new { c.Type, c.Value }) });
 }
}
