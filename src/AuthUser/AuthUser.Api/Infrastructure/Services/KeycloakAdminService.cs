using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AuthUser.Api.Infrastructure.Services;

public class KeycloakAdminService
{
    private readonly HttpClient _client;
    private readonly IConfiguration _config;
    private readonly ILogger<KeycloakAdminService>? _logger;
    private const int DefaultTimeoutMs = 10000;

    public KeycloakAdminService(HttpClient client, IConfiguration config, ILogger<KeycloakAdminService> logger)
    {
        _client = client;
        _config = config;
        _logger = logger;
    }

    private async Task<string?> GetAdminAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("GetAdminAccessTokenAsync: starting token request");
        var realm = _config["Keycloak:Realm"] ?? "plataforma-eventos";
        var tokenUrl = $"/realms/{realm}/protocol/openid-connect/token";
        var adminClientId = _config["Keycloak:AdminClientId"];
        var adminSecret = _config["Keycloak:AdminClientSecret"];
        if (string.IsNullOrEmpty(adminClientId) || string.IsNullOrEmpty(adminSecret)) return null;

        try
        {
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string,string>("grant_type","client_credentials"),
                new KeyValuePair<string,string>("client_id", adminClientId),
                new KeyValuePair<string,string>("client_secret", adminSecret),
            });

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultTimeoutMs);

            var res = await _client.PostAsync(tokenUrl, content, cts.Token);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync(cancellationToken: cts.Token);
                _logger?.LogWarning("GetAdminAccessTokenAsync: token request failed with status {Status}. Content: {Content}", res.StatusCode, err);
                return null;
            }
            var json = await res.Content.ReadAsStringAsync(cancellationToken: cts.Token);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var t)) return t.GetString();
        }
        catch (OperationCanceledException ex)
        {
            _logger?.LogWarning(ex, "GetAdminAccessTokenAsync cancelled or timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "GetAdminAccessTokenAsync exception");
        }
        return null;
    }

    public async Task<string?> CreateUserAsync(string username, string email, string password, CancellationToken cancellationToken = default)
    {
        var realm = _config["Keycloak:Realm"] ?? "plataforma-eventos";
        try
        {
            _logger?.LogInformation("CreateUserAsync: creating user {Username} in realm {Realm}", username, realm);
            var token = await GetAdminAccessTokenAsync(cancellationToken);
            if (token == null) return null;
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                username = username,
                email = email,
                enabled = true,
                credentials = new[] { new { type = "password", value = password, temporary = false } }
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultTimeoutMs);

            var res = await _client.PostAsJsonAsync($"/admin/realms/{realm}/users", payload, cts.Token);
            if (!res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync(cancellationToken: cts.Token);
                _logger?.LogWarning("CreateUserAsync: create user failed {Status}. Content: {Content}", res.StatusCode, content);
                return null;
            }
            if (res.Headers.Location != null)
            {
                var seg = res.Headers.Location.Segments.LastOrDefault();
                return seg?.TrimEnd('/');
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger?.LogWarning(ex, "CreateUserAsync cancelled or timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "CreateUserAsync exception");
        }
        return null;
    }

    public async Task<JsonElement?> GetRealmRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var realm = _config["Keycloak:Realm"] ?? "plataforma-eventos";
        try
        {
            _logger?.LogDebug("GetRealmRoleAsync: getting role {Role} in realm {Realm}", roleName, realm);
            var token = await GetAdminAccessTokenAsync(cancellationToken);
            if (token == null) return null;
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultTimeoutMs);

            var res = await _client.GetAsync($"/admin/realms/{realm}/roles/{roleName}", cts.Token);
            if (!res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync(cancellationToken: cts.Token);
                _logger?.LogWarning("GetRealmRoleAsync: get role {Role} failed {Status}. Content: {Content}", roleName, res.StatusCode, content);
                return null;
            }
            var json = await res.Content.ReadAsStringAsync(cancellationToken: cts.Token);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (OperationCanceledException ex)
        {
            _logger?.LogWarning(ex, "GetRealmRoleAsync cancelled or timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "GetRealmRoleAsync exception");
            return null;
        }
    }

    public async Task<bool> AssignRealmRolesAsync(string userId, IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
    {
        var realm = _config["Keycloak:Realm"] ?? "plataforma-eventos";
        try
        {
            _logger?.LogInformation("AssignRealmRolesAsync: assigning roles {Roles} to user {UserId} in realm {Realm}", string.Join(',', roleNames), userId, realm);
            var token = await GetAdminAccessTokenAsync(cancellationToken);
            if (token == null) return false;
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var rolesToAssign = new List<object>();
            foreach (var rn in roleNames)
            {
                var roleEl = await GetRealmRoleAsync(rn, cancellationToken);
                if (roleEl != null)
                {
                    // build minimal object { id, name }
                    var id = roleEl.Value.GetProperty("id").GetString();
                    var name = roleEl.Value.GetProperty("name").GetString();
                    rolesToAssign.Add(new { id = id, name = name });
                }
            }
            if (!rolesToAssign.Any())
            {
                _logger?.LogInformation("AssignRealmRolesAsync: no roles to assign for user {UserId}", userId);
                return false;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultTimeoutMs);

            var res = await _client.PostAsJsonAsync($"/admin/realms/{realm}/users/{userId}/role-mappings/realm", rolesToAssign, cts.Token);
            if (!res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync(cancellationToken: cts.Token);
                _logger?.LogWarning("AssignRealmRolesAsync: assign roles failed {Status}. Content: {Content}", res.StatusCode, content);
                return false;
            }
            return true;
        }
        catch (OperationCanceledException ex)
        {
            _logger?.LogWarning(ex, "AssignRealmRolesAsync cancelled or timed out");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "AssignRealmRolesAsync exception");
            return false;
        }
    }
}
