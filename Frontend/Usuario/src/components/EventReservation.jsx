import React, { useEffect, useState } from 'react';
import { getEventById, lockSeat, confirmReservation, unlockSeatsByReservation } from '../api';
import { connectSeatHub, disconnectSeatHub, joinEventGroup, leaveEventGroup } from '../seatHub';

function uuidv4() {
  // simple UUID v4 generator for client reservation ids
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    const r = Math.random() * 16 | 0, v = c === 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}

export default function EventReservation({ eventId, onClose }) {
  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [reservationId, setReservationId] = useState(null);
  const [reservingSeat, setReservingSeat] = useState(null);
  const prevEventRef = React.useRef(null);

  useEffect(() => {
    if (!eventId) return;
    // if we had a previous event and a pending local reservation, attempt to unlock those seats
    (async () => {
      try {
        if (prevEventRef.current && prevEventRef.current.id && prevEventRef.current.id !== eventId && reservationId) {
          // unlock all scenarios for previous event
          if (prevEventRef.current.scenarios) {
            for (const s of prevEventRef.current.scenarios) {
              try { await unlockSeatsByReservation(prevEventRef.current.id, s.id, reservationId); } catch (e) { }
            }
          }
          // clear local reservation queue
          setReservationId(null);
        }
      } catch (e) { }

      setLoading(true);
      setError(null);
      try {
        const r = await getEventById(eventId);
        setEvent(r.data);
        prevEventRef.current = r.data;
      } catch (e) {
        setError(e?.response?.data || e.message);
      } finally {
        setLoading(false);
      }
    })();
  }, [eventId]);

  useEffect(() => {
    let mounted = true;
    const onLocked = (payload) => {
      if (!mounted) return;
      try {
        const p = payload;
        if (!p?.eventId || p.eventId !== eventId) return;
        const sPayload = p.seat;
        setEvent(ev => {
          if (!ev) return ev;
          const newScenarios = ev.scenarios.map(s => {
            if (s.id !== p.scenarioId) return s;
            // update matching seat by id, otherwise keep
            const updatedSeats = s.seats.map(se => se.id === sPayload.id ? { ...se, ...sPayload } : se);
            // dedupe seats by id (just in case)
            const seen = new Set();
            const deduped = [];
            for (const st of updatedSeats) {
              const key = st.id || st.seatId || st.Id || st.SeatId;
              if (!seen.has(key)) { seen.add(key); deduped.push(st); }
            }
            return { ...s, seats: deduped };
          });
          return { ...ev, scenarios: newScenarios };
        });
      } catch { }
    };

    const onUnlocked = (payload) => {
      if (!mounted) return;
      try {
        const p = payload;
        if (!p?.eventId || p.eventId !== eventId) return;
        const seats = p.seats || [];
        setEvent(ev => {
          if (!ev) return ev;
          const newScenarios = ev.scenarios.map(s => {
              // try to match seats that belong to this scenario (new server payload includes scenarioId per seat)
              const seatsForScenario = seats.filter(x => (x.scenarioId && x.scenarioId === s.id) || (!x.scenarioId));
              if (!seatsForScenario.length) {
                // fallback: also try to update by matching seat id across scenarios
                const updatedFallback = s.seats.map(se => {
                  const found = seats.find(x => x.id === se.id || x.seatId === se.id || x.Id === se.id || x.SeatId === se.id);
                  return found ? { ...se, isAvailable: true, lockOwner: null, lockExpiresAt: null } : se;
                });
                // dedupe seats by id
                const seenF = new Set();
                const dedupedF = [];
                for (const st of updatedFallback) {
                  const key = st.id || st.seatId || st.Id || st.SeatId;
                  if (!seenF.has(key)) { seenF.add(key); dedupedF.push(st); }
                }
                return { ...s, seats: dedupedF };
              }
              const updated = s.seats.map(se => {
                const found = seatsForScenario.find(x => x.id === se.id || x.seatId === se.id || x.Id === se.id || x.SeatId === se.id);
                return found ? { ...se, isAvailable: true, lockOwner: null, lockExpiresAt: null } : se;
              });
              // dedupe seats by id
              const seen = new Set();
              const deduped = [];
              for (const st of updated) {
                const key = st.id || st.seatId || st.Id || st.SeatId;
                if (!seen.has(key)) { seen.add(key); deduped.push(st); }
              }
              return { ...s, seats: deduped };
            });
            return { ...ev, scenarios: newScenarios };
        });
      } catch (err) { console.warn('EventReservation.onUnlocked error', err); }
    };

    (async () => {
      try {
        const conn = await connectSeatHub(onLocked, onUnlocked, null, null, null, null, null, eventId);
        // attach SeatRemoved handler to remove seats from UI when organizer deletes them
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
                // filter out removed seats by id
                const removedIds = new Set(removed.map(x => x.id));
                const filtered = s.seats.filter(se => !removedIds.has(se.id));
                return { ...s, seats: filtered };
              });
              return { ...ev, scenarios: newScenarios };
            });
          } catch (err) { console.warn('onSeatRemoved error', err); }
        };

        try { conn.off && conn.off('SeatRemoved'); } catch {}
        try { conn.on && conn.on('SeatRemoved', onSeatRemoved); } catch (e) { console.warn('attach SeatRemoved failed', e); }

      } catch (err) {
        console.warn('connectSeatHub failed', err);
      }
    })();

    return () => { mounted = false; leaveEventGroup(eventId).catch(() => {}); /* keep connection alive for other events */ };
  }, [eventId]);

  const handleReserve = async (scenarioId, seatId) => {
    setError(null);
    setReservingSeat(seatId);
    const rid = reservationId || uuidv4();
    setReservationId(rid);
    try {
      const resp = await lockSeat(eventId, scenarioId, seatId, rid);
      // update seat in local state
      const updatedSeat = resp.data;
      setEvent(ev => {
        if (!ev) return ev;
        const newScenarios = ev.scenarios.map(s => {
          if (s.id !== scenarioId) return s;
          return { ...s, seats: s.seats.map(se => se.id === seatId ? { ...se, ...updatedSeat } : se) };
        });
        return { ...ev, scenarios: newScenarios };
      });
    } catch (e) {
      setError(((e?.response?.data) || e.message) || 'No se pudo reservar');
    } finally {
      setReservingSeat(null);
    }
  };

  const handleClose = async () => {
    // if we have a reservationId but not confirmed, unlock seats by reservation for all scenarios
    if (reservationId && !event?.confirmed) {
      try {
        if (event?.scenarios) {
          for (const s of event.scenarios) {
            await unlockSeatsByReservation(event.id, s.id, reservationId).catch(() => {});
          }
        }
      } catch (e) {
        // ignore errors on cleanup
      }
    }
    onClose();
  };

  const handleConfirm = async () => {
    setError(null);
    if (!reservationId) { setError('No hay reserva temporal para confirmar'); return; }
    try {
      // collect locked seats
      const seats = [];
      for (const s of event.scenarios || []) {
        for (const se of s.seats || []) {
          if (se.lockOwner === reservationId) seats.push({ scenarioId: s.id, seatId: se.id });
        }
      }
      const payload = { reservationId, eventId: event.id, seats };
      await confirmReservation({ reservationId: payload.reservationId, eventId: payload.eventId, seats: payload.seats.map(s => s) });
      // clear local reservation queue so user can start new reservations
      setReservationId(null);
      // mark event confirmed locally briefly (UI may rely on this flag), then clear so new reservations allowed
      setEvent(ev => ({ ...ev, confirmed: true }));
      setTimeout(() => setEvent(ev => ev ? { ...ev, confirmed: false } : ev), 200);
    } catch (e) {
      setError(e?.message || 'Error confirmando reserva');
    }
  };

  if (loading) return <div>Cargando evento...</div>;
  if (error) return <div style={{ color: 'crimson' }}>Error: {JSON.stringify(error)}</div>;
  if (!event) return null;

  return (
    <div style={{ border: '1px solid #ddd', padding: 12, borderRadius: 6 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h4 style={{ margin: 0 }}>{event.name}</h4>
        <div>
          <button onClick={handleConfirm} style={{ marginRight: 8 }} disabled={!reservationId || event.confirmed}>Confirmar reserva</button>
          <button onClick={handleClose} style={{ marginLeft: 8 }}>Cerrar</button>
        </div>
      </div>

      <div style={{ marginTop: 8 }}><strong>Fecha:</strong> {event.date}</div>
      <div style={{ marginTop: 4 }}><strong>Lugar:</strong> {event.place}</div>

      <div style={{ marginTop: 12 }}>
        {event.scenarios && event.scenarios.length > 0 ? (
          event.scenarios.map(s => (
            <div key={s.id} style={{ marginBottom: 12 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div><strong>Escenario:</strong> {s.name}</div>
                <div><small>{s.seats?.length ?? 0} asientos</small></div>
              </div>

              <div style={{ marginTop: 8 }}>
                <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                  <thead>
                    <tr style={{ textAlign: 'left', borderBottom: '1px solid #ccc' }}>
                      <th style={{ padding: '6px' }}>Código</th>
                      <th style={{ padding: '6px' }}>Tipo</th>
                      <th style={{ padding: '6px' }}>Precio</th>
                      <th style={{ padding: '6px' }}>Disponible</th>
                      <th style={{ padding: '6px' }}>Acción</th>
                    </tr>
                  </thead>
                  <tbody>
                    {s.seats && s.seats.length > 0 ? s.seats.map(se => (
                      <tr key={se.id} style={{ borderBottom: '1px solid #f0f0f0' }}>
                        <td style={{ padding: '6px' }}>{se.code}</td>
                        <td style={{ padding: '6px' }}>{se.type}</td>
                        <td style={{ padding: '6px' }}>{se.price}</td>
                        <td style={{ padding: '6px' }}>{se.isAvailable ? 'Sí' : 'No'}</td>
                        <td style={{ padding: '6px' }}>
                          {se.isAvailable ? (
                            <button disabled={reservingSeat === se.id} onClick={() => handleReserve(s.id, se.id)}>
                              {reservingSeat === se.id ? 'Reservando...' : 'Reservar'}
                            </button>
                          ) : (
                            <span style={{ color: '#888' }}>No disponible</span>
                          )}
                        </td>
                      </tr>
                    )) : (
                      <tr><td colSpan={5} style={{ padding: 6 }}>No hay asientos.</td></tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          ))
        ) : (
          <div>No hay escenarios para este evento.</div>
        )}
      </div>

      {reservationId && (
        <div style={{ marginTop: 12, padding: 8, background: '#eef', borderRadius: 6 }}>
          <strong>Reserva temporal:</strong>
          <div>ID: <code>{reservationId}</code></div>
          <small>Este ID identifica tu reserva temporal. Confirma o finaliza el pago para completar la reserva (flujo no implementado en este demo).</small>
        </div>
      )}
    </div>
  );
}
