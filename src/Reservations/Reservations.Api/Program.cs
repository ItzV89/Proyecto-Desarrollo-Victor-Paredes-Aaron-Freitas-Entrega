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

// Keycloak authentication
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

// HttpClient to communicate with Events service
services.AddHttpClient("events", client =>
{
    client.BaseAddress = new Uri(configuration["Events:BaseUrl"] ?? "http://localhost:5000/");
});

services.AddHangfire(config => config.UseMemoryStorage());
services.AddHangfireServer();

// RabbitMQ connection helper (simple client registration)
services.AddSingleton(sp =>
{
    var factory = new RabbitMQ.Client.ConnectionFactory()
    {
        HostName = configuration["RabbitMQ:Host"] ?? "localhost",
        UserName = configuration["RabbitMQ:User"] ?? "guest",
        Password = configuration["RabbitMQ:Password"] ?? "guest",
    };
    return factory.CreateConnection();
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
