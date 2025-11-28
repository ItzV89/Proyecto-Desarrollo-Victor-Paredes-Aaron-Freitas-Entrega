<#
  scripts/start-all.ps1

  Script de conveniencia para desarrollo local (Windows PowerShell).
  - Ejecuta `docker compose up -d --build` desde la raíz del repo
  - Espera hasta que Postgres y Keycloak respondan
  - Ejecuta `scripts/run-migrations.ps1` si existe
  - Opcionalmente abre una ventana nueva y arranca el frontend (npm run dev)

  Uso:
    ./scripts/start-all.ps1            # levanta docker, espera y aplica migraciones
    ./scripts/start-all.ps1 -StartFrontend  # además abre el frontend en ventana nueva

# Parámetros
param(
  [switch]$StartFrontend
)

function Wait-ForTcpPort {
  param(
    [string]$Host = 'localhost',
    [int]$Port = 5432,
    [int]$TimeoutSec = 120
  )
  $deadline = (Get-Date).AddSeconds($TimeoutSec)
  while((Get-Date) -lt $deadline) {
    try {
      $res = Test-NetConnection -ComputerName $Host -Port $Port -WarningAction SilentlyContinue
      if ($res.TcpTestSucceeded) { Write-Host "TCP $Host:$Port disponible"; return $true }
    } catch { }
    Write-Host "Esperando TCP $Host:$Port..." -NoNewline; Start-Sleep -Seconds 2; Write-Host ""
  }
  return $false
}

function Wait-ForHttp {
  param(
    [string]$Url,
    [int]$TimeoutSec = 120
  )
  $deadline = (Get-Date).AddSeconds($TimeoutSec)
  while((Get-Date) -lt $deadline) {
    try {
      $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
      if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 400) { Write-Host "HTTP $Url OK (status $($r.StatusCode))"; return $true }
    } catch {
      # ignore and retry
    }
    Write-Host "Esperando HTTP $Url..." -NoNewline; Start-Sleep -Seconds 2; Write-Host ""
  }
  return $false
}

try {
  $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
  $repoRoot = Resolve-Path (Join-Path $scriptDir '..')
  Write-Host "Repo root: $repoRoot"

  Push-Location $repoRoot

  Write-Host "Ejecutando: docker compose up -d --build"
  docker compose up -d --build

  Write-Host "Esperando servicios dependientes..."

  # Esperar Postgres (puerto 5432)
  if (-not (Wait-ForTcpPort -Host 'localhost' -Port 5432 -TimeoutSec 120)) {
    Write-Warning "Timeout esperando Postgres en localhost:5432"
  }

  # Esperar Keycloak (puerto 8080) - endpoint principal
  if (-not (Wait-ForHttp -Url 'http://localhost:8080' -TimeoutSec 120)) {
    Write-Warning "Timeout esperando Keycloak en http://localhost:8080"
  }

  # Ejecutar migraciones si existe el script
  $migrationsScript = Join-Path $repoRoot 'scripts\run-migrations.ps1'
  if (Test-Path $migrationsScript) {
    Write-Host "Ejecutando migraciones: $migrationsScript"
    & $migrationsScript
  } else {
    Write-Host "No se encontró scripts/run-migrations.ps1, omitiendo migraciones."
  }

  if ($StartFrontend) {
    $frontendPath = Join-Path $repoRoot 'Frontend\Usuario'
    if (-not (Test-Path $frontendPath)) { Write-Warning "No existe carpeta frontend: $frontendPath" } else {
      Write-Host "Abriendo ventana nueva para arrancar frontend en: $frontendPath"
      $cmd = "cd `"$frontendPath`"; npm install; npm run dev"
      Start-Process powershell -ArgumentList "-NoExit","-Command",$cmd
    }
  }

  Write-Host "Script completado. Revisa logs si algo falló."
} catch {
  Write-Error "Error en start-all.ps1: $_"
} finally {
  Pop-Location -ErrorAction SilentlyContinue
}
