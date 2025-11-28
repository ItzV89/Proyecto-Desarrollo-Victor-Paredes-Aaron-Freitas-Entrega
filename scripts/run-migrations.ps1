# Script para crear y aplicar migraciones EF Core para los microservicios
# Requisitos: dotnet-ef instalada globalmente: dotnet tool install --global dotnet-ef

Write-Host "Aplicando migraciones para AuthUser..."
cd $PSScriptRoot/..\src\AuthUser\AuthUser.Api
dotnet ef migrations add InitialCreate -o Infrastructure\Persistence\Migrations --project . --startup-project . -v
dotnet ef database update --project . --startup-project . -v

Write-Host "Aplicando migraciones para Events..."
cd $PSScriptRoot/..\src\Events\Events.Api
dotnet ef migrations add InitialCreate -o Infrastructure\Persistence\Migrations --project . --startup-project . -v
dotnet ef database update --project . --startup-project . -v

Write-Host "Aplicando migraciones para Reservations..."
cd $PSScriptRoot/..\src\Reservations\Reservations.Api
dotnet ef migrations add InitialCreate -o Infrastructure\Persistence\Migrations --project . --startup-project . -v
dotnet ef database update --project . --startup-project . -v

Write-Host "Migraciones aplicadas."
