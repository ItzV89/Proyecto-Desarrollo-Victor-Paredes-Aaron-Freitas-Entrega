# Frontend - Usuario

Pequeñas instrucciones para configurar y ejecutar el frontend de `Usuario` (React + Vite) y su integración con Keycloak.

Requisitos
- Node.js 18+ y npm
- Backend(s) corriendo (API Gateway / AuthUser APIs)
- Keycloak disponible (por defecto en `http://localhost:8080`)

Variables de entorno
Coloca valores en el archivo `.env` (ya incluido) o exporta variables antes de ejecutar:

- `REACT_APP_KEYCLOAK_URL` (ej: `http://localhost:8080`)
- `REACT_APP_KEYCLOAK_REALM` (ej: `plataforma-eventos`)
- `REACT_APP_KEYCLOAK_CLIENT_ID` (ej: `react-app-client`)

Configuración en Keycloak
1. Entra a la consola de Keycloak (ej: `http://localhost:8080/admin`).
2. Selecciona el realm `plataforma-eventos` o crea uno con ese nombre.
3. Crea un cliente con `Client ID` = el valor de `REACT_APP_KEYCLOAK_CLIENT_ID` (por defecto `react-app-client`).
   - `Access Type`: `public` o `confidential` según tu flujo. Para PKCE usualmente `public`.
   - `Valid Redirect URIs`: `http://localhost:3000/*`
   - `Web Origins`: `http://localhost:3000`

Ejecución local
1. Abrir PowerShell en `Frontend/Usuario`:
```powershell
cd 'C:\Users\victo\Desktop\All\Desarrollo\Frontend\Usuario'
npm install
npm start
```
2. Abrir `http://localhost:3000` y probar el botón "Iniciar Sesión con Keycloak".

Notas sobre integración
- El backend de `AuthUser` ya está configurado para validar tokens contra `http://localhost:8080/realms/plataforma-eventos` (revisa `src/AuthUser/AuthUser.Api`).
- El front envía el `Authorization: Bearer <token>` al API Gateway.
- Si usas roles, el código frontend comprueba `realm_access.roles` y el backend usa `RoleClaimType = "realm_access/roles"`.

Próximos pasos recomendados
- Implementar un interceptor axios que adjunte automáticamente el token a todas las peticiones.
- Añadir guardas de rutas (React Router) que redirijan al login cuando no esté autenticado.
- Soportar refresh token de forma robusta (Keycloak JS ya expone `updateToken`).

Contacto
Si quieres que implemente el interceptor axios y las guardas de rutas ahora, dime y lo desarrollo.
