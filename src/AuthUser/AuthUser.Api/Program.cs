using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MediatR;
using AuthUser.Api.Infrastructure.Persistence;
using AuthUser.Api.Domain.Repositories;
using AuthUser.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

services.AddCors(options =>
{
 options.AddPolicy("AllowReactApp",
 b => b.WithOrigins(
     "http://localhost:3000",
     "http://localhost:5173",
     "http://127.0.0.1:5173"
 ).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
});

services.AddMediatR(typeof(Program));

var connection = configuration.GetConnectionString("DefaultConnection") ?? "Host=postgres;Database=plataforma;Username=postgres;Password=postgres";
services.AddDbContext<AuthUserDbContext>(opt => opt.UseNpgsql(connection));
services.AddScoped<IProfileRepository, ProfileRepository>();

// Keycloak admin client for provisioning
services.AddHttpClient<KeycloakAdminService>(client =>
{
    client.BaseAddress = new Uri(configuration["Keycloak:AdminBaseUrl"] ?? "http://localhost:8080");
    client.Timeout = TimeSpan.FromSeconds(10);
});
// Note: do NOT register the scoped KeycloakAdminService as a singleton - this caused DI resolution issues.
// KeycloakAdminService is registered via AddHttpClient above which provides a configured HttpClient.
// Do not register KeycloakAdminService manually here to avoid resolving it without the configured HttpClient.

// HttpClient factory for direct token calls
services.AddHttpClient();

services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
 .AddJwtBearer(options =>
 {
 options.Authority = configuration["Keycloak:Authority"] ?? "http://localhost:8080/realms/plataforma-eventos";
 options.Audience = configuration["Keycloak:Audience"] ?? "account";
 options.RequireHttpsMetadata = false;
 options.TokenValidationParameters = new TokenValidationParameters
 {
 RoleClaimType = "realm_access/roles"
 };
 });

services.AddAuthorization(options =>
{
 options.AddPolicy("UsuarioAutenticado", p => p.RequireAuthenticatedUser());
 options.AddPolicy("SoloAdmin", p => p.RequireRole("Administrador"));
});

var app = builder.Build();

// apply migrations automatically in development
using (var scope = app.Services.CreateScope())
{
 var db = scope.ServiceProvider.GetRequiredService<AuthUserDbContext>();
 db.Database.EnsureCreated();
}

app.UseRouting();
app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
