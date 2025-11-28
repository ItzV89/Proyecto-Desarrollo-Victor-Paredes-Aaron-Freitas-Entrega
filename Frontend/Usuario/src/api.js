import axios from 'axios';

// Cliente axios para el API Gateway
export const api = axios.create({
  baseURL: import.meta.env.VITE_API_GATEWAY_URL || 'http://localhost:5000',
});

let interceptorId = null;

// Configura un interceptor que inserta el token desde Keycloak antes de cada petición
export function setupAxios(keycloak) {
  // limpia interceptor anterior si existía
  if (interceptorId !== null) {
    api.interceptors.request.eject(interceptorId);
    interceptorId = null;
  }

  interceptorId = api.interceptors.request.use(async (config) => {
    try {
      if (!keycloak) return config;
      // Intentar refrescar token cercano a expirar
      if (typeof keycloak.updateToken === 'function') {
        await keycloak.updateToken(5).catch(() => {});
      }
      const token = keycloak.token;
      // expose token to other clients (SignalR) if needed
      try { window.__KEYCLOAK_TOKEN__ = token; } catch { }
      if (token) {
        config.headers = config.headers || {};
        config.headers.Authorization = `Bearer ${token}`;
      }
    } catch (e) {
      // no bloquear la petición por errores del interceptor
      console.warn('setupAxios interceptor error', e);
    }
    return config;
  }, (error) => Promise.reject(error));
}

export function teardownAxios() {
  if (interceptorId !== null) {
    api.interceptors.request.eject(interceptorId);
    interceptorId = null;
  }
}

// Crear perfil en el servicio AuthUser. Usamos URL absoluta para permitir llamar
// directamente al servicio `authuser` (útil en desarrollo). El interceptor de
// `api` ya añade el token si `setupAxios` fue llamado con la instancia Keycloak.
export async function createProfile(payload) {
  const url = import.meta.env.VITE_AUTHUSER_CREATE_PROFILE_URL || 'http://localhost:5001/api/profiles';
  return api.post(url, payload);
}

// Events API for organizer
export async function getMyEvents() {
  // expects gateway routing: GET /api/events/my
  return api.get('/api/events/my');
}

export async function getEventById(id) {
  return api.get(`/api/events/${id}`);
}

export async function getAllEvents() {
  return api.get('/api/events');
}

export async function lockSeat(eventId, scenarioId, seatId, reservationId) {
  return api.post(`/api/events/${eventId}/scenarios/${scenarioId}/seats/${seatId}/lock`, { reservationId });
}

export async function unlockSeatsByReservation(eventId, scenarioId, reservationId) {
  return api.post(`/api/events/${eventId}/scenarios/${scenarioId}/seats/unlock`, { reservationId });
}

export async function removeSeats(eventId, scenarioId, count) {
  return api.post(`/api/events/${eventId}/scenarios/${scenarioId}/seats/remove`, { count });
}

export async function deleteSeat(eventId, scenarioId, seatId) {
  return api.delete(`/api/events/${eventId}/scenarios/${scenarioId}/seats/${seatId}`);
}

// Reservation endpoints (backend may need corresponding implementation)
export async function confirmReservation(payload) {
  // payload: { reservationId, eventId, seats: [{ scenarioId, seatId }] }
  return api.post('/api/reservations', payload);
}

export async function getMyReservations() {
  return api.get('/api/reservations/my');
}

export async function cancelReservation(reservationId) {
  return api.delete(`/api/reservations/${reservationId}`);
}

export async function createEvent(payload) {
  return api.post('/api/events', payload);
}

export async function updateEvent(id, payload) {
  return api.put(`/api/events/${id}`, payload);
}

export async function deleteEvent(id) {
  return api.delete(`/api/events/${id}`);
}
