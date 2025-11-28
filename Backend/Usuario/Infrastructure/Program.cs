using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

// 1. Configurar Autenticación (Validación del Token contra Keycloak)
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8080/realms/plataforma-eventos"; // Keycloak
        options.Audience = "account";
        options.RequireHttpsMetadata = false; // Solo para desarrollo
        // Mapeo de roles de Keycloak a Claims de .NET
        options.TokenValidationParameters = new TokenValidationParameters
        {
            RoleClaimType = "realm_access/roles"
        };
    });

// 2. Definir Políticas de Autorización
services.AddAuthorization(options =>
{
    options.AddPolicy("UsuarioAutenticado", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("SoloAdmin", policy =>
        policy.RequireRole("Administrador")); // Rol de Keycloak
});

// 3. Configurar CORS para permitir la conexión desde React (3000)
services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        builder => builder.WithOrigins("http://localhost:3000")
                           .AllowAnyMethod()
                           .AllowAnyHeader());
});

// 4. Añadir Yarp
services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors("AllowReactApp");
app.UseRouting();

// 5. Middleware de Seguridad
app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();