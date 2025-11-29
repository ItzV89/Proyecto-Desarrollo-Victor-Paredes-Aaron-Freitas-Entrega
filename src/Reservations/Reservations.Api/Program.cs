using Hangfire;
using Hangfire.MemoryStorage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Reservations.Api.Infrastructure.Persistence;
using Reservations.Api.Domain.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

services.AddCors(options =>
{
 options.AddPolicy("AllowReactApp",
 b => b.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod());
});

services.AddMediatR(typeof(Program));

var connection = configuration.GetConnectionString("DefaultConnection") ?? "Host=postgres;Database=plataforma;Username=postgres;Password=postgres";
services.AddDbContext<ReservationsDbContext>(opt => opt.UseNpgsql(connection));
services.AddScoped<IReservationRepository, ReservationRepository>();

// Autenticación Keycloak
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

// HttpClient para comunicarse con el servicio Events (sincronización entre servicios)
services.AddHttpClient("events", client =>
{
    client.BaseAddress = new Uri(configuration["Events:BaseUrl"] ?? "http://localhost:5000/");
});

services.AddHangfire(config => config.UseMemoryStorage());
services.AddHangfireServer();

// Helper de conexión a RabbitMQ (registro simple del factory; no crea conexión aquí)
services.AddSingleton<object>(sp =>
{
    var factory = new RabbitMQ.Client.ConnectionFactory()
    {
        HostName = configuration["RabbitMQ:Host"] ?? "localhost",
        UserName = configuration["RabbitMQ:User"] ?? "guest",
        Password = configuration["RabbitMQ:Password"] ?? "guest",
    };
    // Registramos la ConnectionFactory (no creamos la conexión aquí)
    return factory;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
 var db = scope.ServiceProvider.GetRequiredService<ReservationsDbContext>();
 db.Database.EnsureCreated();
}

app.UseCors("AllowReactApp");
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
