# Plataforma de Eventos — Guía de inicio rápido

Este repositorio contiene el backend (.NET 8 + EF Core + SignalR) y el frontend (React + Vite) de una plataforma de gestión de eventos con reservas en tiempo real.

E
**Estructura relevante**

- `src/` — microservicios .NET (AuthUser, Events, Gateway, Reservations, ...)
- `Frontend/Usuario` — cliente React para usuarios/organizadores (Vite)
- `docker-compose.yml` — orquesta Keycloak, Postgres y los servicios Docker de desarrollo
- `scripts/run-migrations.ps1` — utilidades para aplicar migraciones (PowerShell)

Requisitos previos
- Windows 10/11
- Docker Desktop (con Docker Compose integrado)
- .NET 8 SDK (si vas a ejecutar o compilar proyectos .NET localmente)
- Node.js 18+ y npm (para los frontends)
-

Variables de entorno / configuración
- El frontend usa variables `VITE_API_GATEWAY_URL` y `VITE_AUTHUSER_CREATE_PROFILE_URL` si quieres apuntar a otros endpoints. Puedes definirlas en tu entorno o en un archivo `.env` en cada carpeta del frontend según Vite.

Pasos para arrancar en local (modo recomendado con Docker Compose)
1. Abrir PowerShell en la raíz del proyecto (`c:\Users\<tu_usuario>\Desktop\All\Desarrollo`).
2. Iniciar servicios con Docker Compose (Keycloak, Postgres, gateways y servicios configurados por el `docker-compose.yml`):

```powershell
# Abre PowerShell en la raíz del repositorio o cámbiate a ella:
cd '<ruta-al-repo>'
docker compose up -d --build
```



3. (Opcional) Aplicar migraciones si trabajas con la base de datos local o cambias modelos:

```powershell
.\scripts\run-migrations.ps1
```

4. Iniciar la UI (frontend) — carpeta del usuario (Vite):

```powershell
cd .\Frontend\Usuario
npm install
npm run dev
```

 - Abrir la URL que Vite muestra (por defecto `http://localhost:5173` o la que aparezca en consola).
