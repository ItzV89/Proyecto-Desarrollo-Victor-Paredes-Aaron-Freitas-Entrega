using MediatR;
using Microsoft.EntityFrameworkCore;
using Events.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
// RabbitMQ se utilizará en producción a través de un EventBus especializado.
// Para el desarrollo local registramos un NoopEventBus que evita la dependencia de RabbitMQ.

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

services.AddCors(options =>
{
 options.AddPolicy("AllowReactApp",
 b => b.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://localhost:5000").AllowAnyHeader().AllowAnyMethod().AllowCredentials());
});

services.AddMediatR(typeof(Program));

// SignalR para notificaciones en tiempo real sobre bloqueo/estado de butacas
services.AddSignalR();

var connection = configuration.GetConnectionString("DefaultConnection") ?? "Host=postgres;Database=plataforma;Username=postgres;Password=postgres";
services.AddDbContext<EventsDbContext>(opt => opt.UseNpgsql(connection));

// Keycloak JWT Bearer authentication for Events API
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["Keycloak:Authority"] ?? "http://localhost:8080/realms/plataforma-eventos";
            options.Audience = configuration["Keycloak:Audience"] ?? "account";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
                    // In local development allow tokens issued with either the container hostname
                    // or localhost (host machine) as issuer so testing from host works.
                    ValidateAudience = false,
                    RoleClaimType = "realm_access/roles",
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

// register http client for internal calls (other services may call Events)
services.AddHttpClient("events", client =>
{
    client.BaseAddress = new Uri(configuration["Events:BaseUrl"] ?? "http://localhost:5000/");
});

// http client to call Reservations service (used for inter-service sync)
services.AddHttpClient("reservations", client =>
{
    client.BaseAddress = new Uri(configuration["Reservations:BaseUrl"] ?? "http://localhost:5003/");
});

// background job to clean expired seat locks
services.AddHostedService<Events.Api.Infrastructure.Services.ExpiredLocksCleaner>();
// Register a local no-op event bus (avoids RabbitMQ dependency during local development)
services.AddSingleton<Events.Api.Infrastructure.Services.IEventBus, Events.Api.Infrastructure.Services.NoopEventBus>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
 var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
 db.Database.EnsureCreated();
}


app.UseRouting();
app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// Map SignalR hubs
app.MapHub<Events.Api.Hubs.SeatHub>("/hubs/seats");

app.Run();
