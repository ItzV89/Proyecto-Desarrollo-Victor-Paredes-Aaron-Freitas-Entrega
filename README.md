# Plataforma de Eventos — Guía de inicio rápido

Este repositorio contiene el backend (.NET 8 + EF Core + SignalR) y el frontend (React + Vite) de una plataforma de gestión de eventos con reservas en tiempo real.

Este README explica cómo poner el proyecto en marcha en una máquina Windows usando PowerShell.

**Estructura relevante**
- `Backend/` — proyectos .NET para microservicios (Reserva, Usuario, ...)
- `src/` — microservicios .NET (AuthUser, Events, Gateway, Reservations, ...)
- `Frontend/Usuario` — cliente React para usuarios/organizadores (Vite)
- `docker-compose.yml` — orquesta Keycloak, Postgres y los servicios Docker de desarrollo
- `scripts/run-migrations.ps1` — utilidades para aplicar migraciones (PowerShell)

Requisitos previos
- Windows 10/11
- PowerShell (v5.1 o superior) — este README usa PowerShell
- Docker Desktop (con Docker Compose integrado)
- .NET 8 SDK (si vas a ejecutar o compilar proyectos .NET localmente)
- Node.js 18+ y npm (para los frontends)
- (Opcional) un cliente HTTP como `curl` o `Invoke-RestMethod` en PowerShell

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

 - Si sólo quieres reconstruir/levantar el servicio `events` (por ejemplo tras cambiar controllers), usa:

```powershell
docker compose up -d --build events
```

3. Verificar que los contenedores están arriba y ver logs (útil para debugging):

```powershell
docker compose ps
docker compose logs --no-color --tail 200 events
```

4. (Opcional) Aplicar migraciones si trabajas con la base de datos local o cambias modelos:

```powershell
.\scripts\run-migrations.ps1
```

5. Iniciar la UI (frontend) — carpeta del usuario (Vite):

```powershell
# Desde la raíz del repositorio puedes entrar al frontend de usuario así:
cd .\Frontend\Usuario
npm install
npm run dev
```

 - Abrir la URL que Vite muestra (por defecto `http://localhost:5173` o la que aparezca en consola).
 - Si trabajas con otro frontend (por ejemplo `Frontend/Reserva`), repite los pasos de `npm install` y `npm run dev` en esa carpeta.

6. Acceder a Keycloak (si está levantado por docker-compose): normalmente en `http://localhost:8080` (usuario/contraseña según la configuración del `docker-compose.yml` o la documentación interna del proyecto).

Notas importantes sobre autenticación y SignalR
 - El cliente react expone el token Keycloak en `window.__KEYCLOAK_TOKEN__` para que SignalR (que necesita un `accessTokenFactory`) pueda leerlo y establecer la conexión.
 - Asegúrate de iniciar sesión desde la UI para que `KeycloakProvider` inicialice `setupAxios(keycloak)` y las peticiones al API Gateway lleven el token.

Cómo desarrollar y probar cambios en Backend
 - Si editas C# en `src/Events/Events.Api` (o proyectos .NET similares), puedes:
	 - Compilar y ejecutar localmente con `dotnet run` dentro del proyecto (requiere .NET 8 SDK).
	 - O reconstruir la imagen Docker y reiniciar el servicio con `docker compose up -d --build events`.

**Flujo de comandos (PowerShell) — lista rápida**

Estos comandos resumen el flujo típico para preparar, arrancar y depurar el proyecto en una máquina Windows usando PowerShell. Copia/pega cada bloque según la tarea que necesites.

- Preparar entorno (en la raíz del repo):
```powershell
# Abre PowerShell en la raíz del repositorio o cámbiate a ella:
cd '<ruta-al-repo>'
dotnet tool restore
```

- Levantar todos los servicios con Docker Compose (Keycloak, Postgres, gateway, services):
```powershell
docker compose up -d --build
```

- Reconstruir/levantar sólo el servicio `events` (útil tras cambios en el backend):
```powershell
docker compose up -d --build events
```

- Ver el estado de los contenedores y logs (últimos 200 registros del servicio `events`):
```powershell
docker compose ps
docker compose logs --no-color --tail 200 events
```

- Aplicar migraciones (si trabajas en modelos / EF Core):
```powershell
.\scripts\run-migrations.ps1
```

- Iniciar frontend (carpeta Usuario con Vite):
```powershell
cd 'C:\Users\victo\Desktop\All\Desarrollo\Frontend\Usuario'
npm install
npm run dev
```

- Petición de prueba al API Gateway (comprobar events):
```powershell
Invoke-RestMethod -Method Get -Uri 'http://localhost:5002/api/events' | Format-Table -AutoSize
```

Comandos útiles (PowerShell)
 - Ver logs del servicio `events` (últimos 200 registros):
```powershell
docker compose logs --no-color --tail 200 events
```
 - Reconstruir y levantar un servicio concreto:
```powershell
docker compose up -d --build events
```
 - Ejecutar una petición test al API Gateway:
```powershell
Invoke-RestMethod -Method Get -Uri 'http://localhost:5002/api/events' | Format-Table -AutoSize
```

Solución de problemas comunes
 - Dev server Vite falla en `npm run dev`:
	 - Asegúrate de haber corrido `npm install` en la carpeta correcta.
	 - Revisa la consola de Vite para mensajes de import faltante o errores de compilación (p.ej. hooks mal ordenados en React).

 - Al actualizar un evento, desaparecen los asientos:
	 - Causa frecuente: el formulario de edición estaba enviando `seatTypes: []` en la petición de actualización. Si editas un evento, asegúrate de que el formulario esté prellenado con `seatTypes` (se hizo un cambio en `EventForm.jsx` para derivar `seatTypes` desde `initial.scenarios`). Si los asientos ya se eliminaron en la base de datos, necesitas restaurarlos desde backup o recrearlos manualmente.

 - SignalR no recibe eventos:
	 - Verifica que el token Keycloak esté presente (inicia sesión) y que `window.__KEYCLOAK_TOKEN__` esté definido.
	 - Revisa logs del backend y el hub de SignalR (`/hubs/seats`) para errores de autenticación.

Pruebas manuales sugeridas (flows críticos)
1. Flujo organizador / usuario (reservas en tiempo real):
	- Loguear como organizador e iniciar un evento con escenarios/asientos.
	- Abrir otra ventana en modo usuario y navegar al evento.
	- Desde el usuario: intentar reservar un asiento (debe recibir lock/unlock en tiempo real).
	- Desde el organizador: eliminar un asiento y verificar que el usuario recibe `SeatRemoved` y que reservas afectadas se cancelan (broadcast `ReservationCancelled`).

2. Crear / Editar eventos (Organizador):
	- Crear un evento con tipos de asientos.
	- Editar sólo el nombre (sin tocar tipos de asientos) y comprobar que los asientos no desaparecen.

Cómo contribuir
 - Sigue el estilo del proyecto y realiza cambios pequeños y revisables.
 - Si tocas esquemas de persistencia (migrations), añade las migraciones y pruébalas en una base de datos local antes de subir cambios.

Restauración de datos (si perdiste asientos)
 - Si los asientos fueron eliminados por una actualización accidental, la única forma segura de restaurarlos es desde un backup de la base de datos.
 - Alternativa rápida: recrear escenarios/asientos manualmente desde el panel de organizador (crear tipos de asientos y ejecutar la lógica para poblar asientos si se implementa).

Contacto / notas finales
 - Este README cubre el flujo de desarrollo y pruebas local en Windows. Si quieres, puedo añadir scripts de automatización (PowerShell) para preparar todo (levantar docker, aplicar migraciones y arrancar frontends).

---
Generado por el asistente del proyecto — instrucciones orientadas a desarrollo local en Windows (PowerShell). Si quieres una versión reducida o instrucciones para Mac/Linux, dime y las adapto.
