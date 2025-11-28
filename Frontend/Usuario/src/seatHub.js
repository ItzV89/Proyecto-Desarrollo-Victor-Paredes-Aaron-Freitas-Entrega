import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

let connection = null;

export async function connectSeatHub(onLocked, onUnlocked, onReservationCreated, onReservationCancelled, onEventCreated, onEventUpdated, onEventDeleted, joinEventId) {
  if (connection) {
    // if already connected and a joinEventId was provided, join that group
    // attach handlers to existing connection (avoid missing handlers when reused)
    try {
      if (onLocked) {
        try { connection.off('SeatLocked'); } catch {}
        connection.on('SeatLocked', onLocked);
        console.debug('seatHub: attached SeatLocked handler to existing connection');
      }
      if (onUnlocked) {
        try { connection.off('SeatUnlocked'); } catch {}
        connection.on('SeatUnlocked', onUnlocked);
        console.debug('seatHub: attached SeatUnlocked handler to existing connection');
      }
      if (onReservationCreated) {
        try { connection.off('ReservationCreated'); } catch {}
        connection.on('ReservationCreated', onReservationCreated);
        console.debug('seatHub: attached ReservationCreated handler to existing connection');
      }
      if (onReservationCancelled) {
        try { connection.off('ReservationCancelled'); } catch {}
        connection.on('ReservationCancelled', onReservationCancelled);
        console.debug('seatHub: attached ReservationCancelled handler to existing connection');
      }
      if (onEventCreated) {
        try { connection.off('EventCreated'); } catch {}
        connection.on('EventCreated', onEventCreated);
        console.debug('seatHub: attached EventCreated handler to existing connection');
      }
      if (onEventUpdated) {
        try { connection.off('EventUpdated'); } catch {}
        connection.on('EventUpdated', onEventUpdated);
        console.debug('seatHub: attached EventUpdated handler to existing connection');
      }
      if (onEventDeleted) {
        try { connection.off('EventDeleted'); } catch {}
        connection.on('EventDeleted', onEventDeleted);
        console.debug('seatHub: attached EventDeleted handler to existing connection');
      }
    } catch (e) { console.warn('seatHub: attach handlers failed', e); }

    if (joinEventId) {
      try { await connection.invoke('JoinEvent', joinEventId); } catch (e) { console.warn('seatHub: joinEvent failed on existing connection', e); }
    }
    return connection;
  }
  const url = import.meta.env.VITE_EVENTS_HUB_URL || 'http://localhost:5002/hubs/seats';

  // build a local connection and start it; assign to global `connection` only after start success
  const localConnection = new HubConnectionBuilder()
    .withUrl(url, {
      accessTokenFactory: () => window.__KEYCLOAK_TOKEN__ || ''
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build();

  if (onLocked) localConnection.on('SeatLocked', onLocked);
  if (onUnlocked) localConnection.on('SeatUnlocked', onUnlocked);
  if (onReservationCreated) localConnection.on('ReservationCreated', onReservationCreated);
  if (onReservationCancelled) localConnection.on('ReservationCancelled', onReservationCancelled);
  if (onEventCreated) localConnection.on('EventCreated', onEventCreated);
  if (onEventUpdated) localConnection.on('EventUpdated', onEventUpdated);
  if (onEventDeleted) localConnection.on('EventDeleted', onEventDeleted);
  if (typeof onEventDeleted === 'function') {
    // placeholder to keep order; SeatRemoved handler should be passed as onUnlocked or additional param by consumers
  }
  // support SeatRemoved handler via dynamic subscription by callers using connection.on('SeatRemoved', handler)

  // wait for token to be available (Keycloak may initialize asynchronously)
  const waitForToken = async (timeoutMs = 5000) => {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      if (window.__KEYCLOAK_TOKEN__ && window.__KEYCLOAK_TOKEN__.length > 10) return window.__KEYCLOAK_TOKEN__;
      await new Promise(r => setTimeout(r, 200));
    }
    return window.__KEYCLOAK_TOKEN__ || null;
  };

    try {
      console.debug('seatHub: starting connection to', url);
      const token = await waitForToken(5000);
      if (!token) console.warn('seatHub: no token available before connect, proceeding without token (negotiate may fail)');

      // attempt to start and retry once on AbortError using localConnection
      try {
        await localConnection.start();
      } catch (err) {
        console.error('seatHub: start failed first attempt', err);
        // if negotiation aborted, retry after short delay
        if (err && err.name === 'AbortError') {
          await new Promise(r => setTimeout(r, 500));
          try {
            await localConnection.start();
          } catch (err2) {
            console.error('seatHub: start failed second attempt', err2);
            throw err2;
          }
        } else {
          throw err;
        }
      }

      console.debug('seatHub: connection started');
      // only set the shared connection if still null (avoid clobbering another concurrent connection)
      if (!connection) {
        connection = localConnection;
        try { window.__SEAT_HUB_CONNECTION__ = connection; } catch {}
        console.debug('seatHub: global connection assigned and exposed on window.__SEAT_HUB_CONNECTION__');
      }

      if (joinEventId) {
        try {
          await connection.invoke('JoinEvent', joinEventId);
          console.debug('seatHub: joined event group', joinEventId);
        } catch (err) {
          console.error('seatHub: join event failed', err);
        }
      }
      return connection;
    } catch (err) {
      console.error('seatHub: start failed', err);
      throw err;
    }
}

export async function disconnectSeatHub() {
  if (!connection) return;
  try {
    // attempt to leave all groups gracefully is not tracked here; just stop
    await connection.stop();
  } catch { }
  connection = null;
}

export async function joinEventGroup(eventId) {
  if (!connection) return;
  try { await connection.invoke('JoinEvent', eventId); } catch { }
}

export async function leaveEventGroup(eventId) {
  if (!connection) return;
  try { await connection.invoke('LeaveEvent', eventId); } catch { }
}
