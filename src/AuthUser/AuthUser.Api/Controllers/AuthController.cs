using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AuthUser.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly HttpClient _client;
    private readonly IConfiguration _config;
    public AuthController(IHttpClientFactory http, IConfiguration config)
    {
        _client = http.CreateClient();
        _config = config;
        _client.BaseAddress = new System.Uri(_config["Keycloak:BaseUrl"] ?? "http://localhost:8080");
    }

    [HttpPost("token")]
    public async Task<IActionResult> Token([FromForm] Dictionary<string, string> form)
    {
        var realm = _config["Keycloak:Realm"] ?? "plataforma-eventos";
        var tokenUrl = $"/realms/{realm}/protocol/openid-connect/token";
        var content = new FormUrlEncodedContent(form);
        var res = await _client.PostAsync(tokenUrl, content);
        var body = await res.Content.ReadAsStringAsync();
        return StatusCode((int)res.StatusCode, body);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromForm] Dictionary<string, string> form)
    {
        return await Token(form);
    }
}
