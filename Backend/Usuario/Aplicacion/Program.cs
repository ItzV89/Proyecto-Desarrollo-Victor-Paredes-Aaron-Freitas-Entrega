using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("ocelot.json");

// 1. Configuración de la Validación de Keycloak (Autenticación)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer("KeycloakScheme", options =>
{
    options.Authority = "https://[TU_URL_KEYCLOAK]/realms/[TU_REALM]";
    options.Audience = "account"; // Generalmente 'account' o el Client ID de tu API Gateway
    options.RequireHttpsMetadata = true;
    // Opcional: Personalizar el mapeo de claims si es necesario
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
    };
});

// 2. Configuración de Ocelot
builder.Services.AddOcelot();

var app = builder.Build();

// Middleware de Autenticación (ANTES de Ocelot)
app.UseAuthentication();

// Middleware de Ocelot
await app.UseOcelot();

app.Run();