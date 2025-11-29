import React, { useEffect, useState } from 'react';
import { getEventById, deleteSeat } from '../api';
import { useKeycloak } from '../../KeycloakProvider';
import { connectSeatHub, disconnectSeatHub, joinEventGroup, leaveEventGroup } from '../seatHub';

export default function EventDetails({ eventId, onClose }) {
  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const { keycloak } = useKeycloak();
  const [deletingSeatId, setDeletingSeatId] = useState(null);

  useEffect(() => {
    if (!eventId) return;
    setLoading(true);
    setError(null);
    getEventById(eventId).then(r => {
      setEvent(r.data);
    }).catch(e => setError(e?.response?.data || e.message)).finally(() => setLoading(false));
  }, [eventId]);

  // subscribe to SignalR hub to receive seat/reservation updates for organizers
  useEffect(() => {
    if (!eventId) return;
    let mounted = true;

    const onLocked = (payload) => {
      if (!mounted) return;
      try {
        const p = payload || {};
        if (!p?.eventId || p.eventId !== eventId) return;
        const sPayload = p.seat;
        setEvent(ev => {
          if (!ev) return ev;
          const newScenarios = ev.scenarios.map(s => {
            if (s.id !== p.scenarioId) return s;
            const updatedSeats = s.seats.map(se => se.id === sPayload.id ? { ...se, ...sPayload } : se);
            return { ...s, seats: updatedSeats };
          });
          return { ...ev, scenarios: newScenarios };
        });
      } catch { }
    };

    const onUnlocked = (payload) => {
      if (!mounted) return;
      try {
        const p = payload || {};
        if (!p?.eventId || p.eventId !== eventId) return;
        const seats = p.seats || [];
        setEvent(ev => {
          if (!ev) return ev;
          const newScenarios = ev.scenarios.map(s => {
            const seatsForScenario = seats.filter(x => (x.scenarioId && x.scenarioId === s.id) || (!x.scenarioId));
            if (!seatsForScenario.length) {
              const updatedFallback = s.seats.map(se => {
                const found = seats.find(x => x.id === se.id || x.seatId === se.id || x.Id === se.id || x.SeatId === se.id);
                return found ? { ...se, isAvailable: true, lockOwner: null, lockExpiresAt: null } : se;
              });
              return { ...s, seats: updatedFallback };
            }
            const updated = s.seats.map(se => {
              const found = seatsForScenario.find(x => x.id === se.id || x.seatId === se.id || x.Id === se.id || x.SeatId === se.id);
              return found ? { ...se, isAvailable: true, lockOwner: null, lockExpiresAt: null } : se;
            });
            return { ...s, seats: updated };
          });
          return { ...ev, scenarios: newScenarios };
        });
      } catch (e) { console.warn('EventDetails.onUnlocked error', e); }
    };

    const onSeatRemoved = (payload) => {
      if (!mounted) return;
      try {
        const p = payload || {};
        if (!p?.eventId || p.eventId !== eventId) return;
        const removed = p.seats || [];
        setEvent(ev => {
          if (!ev) return ev;
          const newScenarios = ev.scenarios.map(s => {
            if (s.id !== p.scenarioId) return s;
            const removedIds = new Set(removed.map(x => x.id));
            const filtered = s.seats.filter(se => !removedIds.has(se.id));
            return { ...s, seats: filtered };
          });
          return { ...ev, scenarios: newScenarios };
        });
      } catch (err) { console.warn('onSeatRemoved error', err); }
    };

    const onReservationCancelled = (payload) => {
      if (!mounted) return;
      try {
        const p = payload || {};
        if (!p?.eventId || p.eventId !== eventId) return;
        // when a reservation is cancelled due to seat deletion, update seats and remove reservation references
        const seats = p.seats || [];
        setEvent(ev => {
          if (!ev) return ev;
          const newScenarios = ev.scenarios.map(s => {
            const seatsForScenario = seats.filter(x => (x.scenarioId && x.scenarioId === s.id) || (!x.scenarioId));
            if (!seatsForScenario.length) return s;
            const updated = s.seats.map(se => {
              const found = seatsForScenario.find(x => x.id === se.id || x.seatId === se.id || x.Id === se.id || x.SeatId === se.id);
              return found ? { ...se, isAvailable: true, lockOwner: null, lockExpiresAt: null } : se;
            });
            return { ...s, seats: updated };
          });
          return { ...ev, scenarios: newScenarios };
        });
      } catch (e) { console.warn('EventDetails.onReservationCancelled error', e); }
    };

    (async () => {
      try {
        const conn = await connectSeatHub(onLocked, onUnlocked, null, onReservationCancelled);
        try { conn.off && conn.off('SeatRemoved'); } catch {}
        try { conn.on && conn.on('SeatRemoved', onSeatRemoved); } catch (e) { console.warn('attach SeatRemoved failed', e); }
        try { await joinEventGroup(eventId); } catch (e) { }
      } catch (err) { console.warn('EventDetails connectSeatHub failed', err); }
    })();

    return () => { mounted = false; leaveEventGroup(eventId).catch(() => {}); /* keep connection alive */ };
  }, [eventId]);

  if (loading) return <div>Cargando detalles...</div>;
  if (error) return <div style={{ color: 'crimson' }}>Error: {JSON.stringify(error)}</div>;
  if (!event) return null;

  const isOrganizer = Boolean(
    keycloak && typeof keycloak.hasRealmRole === 'function' && (keycloak.hasRealmRole('Organizador') || keycloak.hasRealmRole('organizador'))
  );

  const handleDeleteSeat = async (scenarioId, seat) => {
    if (!confirm(`Eliminar asiento ${seat.code}? Esta acción cancelará reservas que lo incluyan.`)) return;
    try {
      setDeletingSeatId(seat.id);
      await deleteSeat(eventId, scenarioId, seat.id);
      // optimistic UI: remove seat from state
      setEvent(prev => {
        if (!prev) return prev;
        return {
          ...prev,
          scenarios: prev.scenarios.map(s => s.id === scenarioId ? { ...s, seats: s.seats.filter(se => se.id !== seat.id) } : s)
        };
      });
    } catch (e) {
      alert('Error eliminando asiento: ' + (e?.response?.data?.message || e.message));
    } finally {
      setDeletingSeatId(null);
    }
  };

  return (
    <div style={{ border: '1px solid #ddd', padding: 12, borderRadius: 6 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h4 style={{ margin: 0 }}>{event.name}</h4>
        <div>
          <button onClick={onClose} style={{ marginLeft: 8 }}>Cerrar</button>
        </div>
      </div>
      <div style={{ marginTop: 8 }}><strong>Fecha:</strong> {event.date}</div>
      <div style={{ marginTop: 4 }}><strong>Tipo:</strong> {event.eventType}</div>
      <div style={{ marginTop: 4 }}><strong>Lugar:</strong> {event.place}</div>
      <div style={{ marginTop: 8 }}><strong>Descripción:</strong>
        <div style={{ marginTop: 4 }}>{event.description}</div>
      </div>

      <div style={{ marginTop: 12 }}>
        <h5>Escenarios y Asientos</h5>
        {event.scenarios && event.scenarios.length > 0 ? (
          event.scenarios.map(s => (
            <div key={s.id} style={{ marginBottom: 12 }}>
              <div><strong>Escenario:</strong> {s.name}</div>
              <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: 6 }}>
                <thead>
                  <tr style={{ textAlign: 'left', borderBottom: '1px solid #ccc' }}>
                    <th style={{ padding: '6px' }}>Código</th>
                    <th style={{ padding: '6px' }}>Tipo</th>
                    <th style={{ padding: '6px' }}>Precio</th>
                    <th style={{ padding: '6px' }}>Disponible</th>
                    <th style={{ padding: '6px' }}>LockOwner</th>
                    {isOrganizer && <th style={{ padding: '6px' }}>Acciones</th>}
                  </tr>
                </thead>
                <tbody>
                  {s.seats && s.seats.length > 0 ? s.seats.map(se => (
                    <tr key={se.id} style={{ borderBottom: '1px solid #f0f0f0' }}>
                      <td style={{ padding: '6px' }}>{se.code}</td>
                      <td style={{ padding: '6px' }}>{se.type}</td>
                      <td style={{ padding: '6px' }}>{se.price}</td>
                      <td style={{ padding: '6px' }}>{se.isAvailable ? 'Sí' : 'No'}</td>
                      <td style={{ padding: '6px' }}>{se.lockOwner ?? '-'}</td>
                      {isOrganizer && (
                        <td style={{ padding: '6px' }}>
                          <button disabled={deletingSeatId === se.id} onClick={() => handleDeleteSeat(s.id, se)} style={{ padding: '6px', background: '#e74c3c', color: 'white', border: 'none', cursor: 'pointer' }}>
                            {deletingSeatId === se.id ? 'Eliminando...' : 'Eliminar asiento'}
                          </button>
                        </td>
                      )}
                    </tr>
                  )) : <tr><td colSpan={isOrganizer ? 6 : 5} style={{ padding: 6 }}>No hay asientos.</td></tr>}
                </tbody>
              </table>
              
            </div>
          ))
        ) : (
          <div>No hay escenarios para este evento.</div>
        )}
      </div>
    </div>
  );
}
