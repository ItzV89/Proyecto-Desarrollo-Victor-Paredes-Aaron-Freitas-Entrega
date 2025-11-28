using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

builder.Configuration.AddJsonFile("ocelot.json", optional: true);

services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"));

services.AddCors(options =>
{
	options.AddPolicy("AllowReactApp",
		b => b.WithOrigins("http://localhost:5173", "http://localhost:3000").AllowAnyHeader().AllowAnyMethod());
});

services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
 .AddJwtBearer(options =>
 {
 options.Authority = configuration["Keycloak:Authority"] ?? "http://localhost:8080/realms/plataforma-eventos";
 options.Audience = configuration["Keycloak:Audience"] ?? "account";
 options.RequireHttpsMetadata = false;
 options.TokenValidationParameters = new TokenValidationParameters
 {
 RoleClaimType = "realm_access/roles",
 // Accept both container and localhost issuers for local development/testing
 ValidIssuers = new[] {
	 configuration["Keycloak:Authority"] ?? "http://keycloak:8080/realms/plataforma-eventos",
	 "http://localhost:8080/realms/plataforma-eventos"
 }
 };
 });

services.AddAuthorization(options =>
{
	options.AddPolicy("UsuarioAutenticado", p => p.RequireAuthenticatedUser());
	options.AddPolicy("SoloAdmin", p => p.RequireRole("Administrador"));
});

var app = builder.Build();

app.UseRouting();
app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();
