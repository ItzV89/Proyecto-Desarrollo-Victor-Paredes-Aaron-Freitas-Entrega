<#
  scripts/push-to-remote.ps1

  Automatiza añadir un remoto y empujar el contenido del repo local al remoto.
  Uso:
    ./scripts/push-to-remote.ps1 -RemoteUrl 'https://github.com/ItzV89/Proyecto-Desarrollo-Victor-Paredes-Aaron-Freitas.git'

  El script hace lo siguiente:
  - Comprueba que estamos dentro de un repo git (si no, inicializa uno)
  - Crea un `.gitignore` mínimo si no existe
  - Añade y hace commit de los cambios (si hay cambios)
  - Añade el remoto indicado (si ya existe, lo actualiza)
  - Intenta pushear la rama `main`. Si el remoto tiene commits, hace `git pull --rebase` antes de pushear.

  Nota: necesitarás autenticarte en GitHub (SSH o HTTPS). Para HTTPS puedes usar `gh auth login` o un PAT.
#>

param(
  [Parameter(Mandatory=$true)]
  [string]$RemoteUrl
)

function Ensure-GitRepo {
  try { git rev-parse --is-inside-work-tree > $null 2>&1; return $true } catch { return $false }
}

function Ensure-GitIgnore {
  $g = Join-Path (Get-Location) '.gitignore'
  if (-not (Test-Path $g)) {
    @'
# Node
node_modules/
dist/
.vite/

# .NET
bin/
obj/
*.user
*.suo
*.vs/

# VSCode
.vscode/

# Docker
docker-compose.override.yml

# Logs / temp
*.log
*.tmp
.env
'@ | Set-Content -Path $g -Encoding UTF8
    Write-Host "Creado .gitignore"
  } else { Write-Host ".gitignore ya existe" }
}

if (-not (Ensure-GitRepo)) {
  Write-Host "No hay repo git. Inicializando..."
  git init
} else {
  Write-Host "Repo git detectado"
}

Ensure-GitIgnore

# Añadir y commitear si hay cambios
$status = git status --porcelain
if ($status) {
  Write-Host "Hay cambios locales. Añadiendo y haciendo commit..."
  git add .
  try {
    git commit -m "chore: initial project import"
  } catch {
    Write-Host "No se pudo commitear (posible commit vacío): $_"
  }
} else {
  Write-Host "No hay cambios para commitear"
}

# Asegurar rama main
try { git branch --show-current > $null 2>&1; $cur = git rev-parse --abbrev-ref HEAD } catch { $cur = 'main' }
if ($cur -ne 'main') {
  Write-Host "Cambiando a rama 'main'"
  git branch -M main
}

# Configurar remoto
$existing = git remote get-url origin 2>$null
if ($existing) {
  Write-Host "Remote 'origin' ya existe: $existing"
  if ($existing -ne $RemoteUrl) {
    Write-Host "Actualizando URL remoto origin => $RemoteUrl"
    git remote remove origin
    git remote add origin $RemoteUrl
  }
} else {
  Write-Host "Añadiendo remote origin => $RemoteUrl"
  git remote add origin $RemoteUrl
}

# Intentar push
Write-Host "Intentando push a origin main..."
try {
  git push -u origin main
  Write-Host "Push completado"
} catch {
  Write-Warning "Push falló: intento de reconciliación con remoto (pull --rebase)"
  try {
    git fetch origin
    git pull --rebase origin main
    git push -u origin main
    Write-Host "Push completado tras rebase"
  } catch {
    Write-Error "No se pudo sincronizar con el remoto automáticamente. Revisa conflictos y empuja manualmente. Error: $_"
  }
}
