import React, { useEffect, useState } from 'react';
import { getMyReservations, cancelReservation } from '../api';
import { connectSeatHub, disconnectSeatHub, joinEventGroup, leaveEventGroup } from '../seatHub';

export default function MyReservations() {
  const [reservations, setReservations] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const load = async () => {
    setLoading(true); setError(null);
    try {
      const resp = await getMyReservations();
      const data = resp.data || [];
      setReservations(data);
    } catch (e) { setError(e?.response?.data || e.message); }
    setLoading(false);
  };

  useEffect(() => { load(); }, []);

  // setup SignalR connection to listen to reservation updates and then load reservations
  useEffect(() => {
    let mounted = true;
    const onReservationCreated = (payload) => { try { if (!mounted) return; load(); } catch { } };
    const onReservationCancelled = (payload) => { try { if (!mounted) return; load(); } catch { } };

    (async () => {
      try {
        await connectSeatHub(null, null, onReservationCreated, onReservationCancelled);
        if (!mounted) return;
        // after connection is established, load reservations and join groups
        await load();
        try {
          const data = (await getMyReservations()).data || [];
          for (const g of data) if (g?.eventId) { try { await joinEventGroup(g.eventId); } catch { } }
        } catch { }
      } catch (e) {
        // ignore connect errors, still attempt initial load
        try { await load(); } catch { }
      }
    })();

    return () => { mounted = false; disconnectSeatHub().catch(() => {}); };
  }, []);

  const handleCancel = async (id) => {
    if (!confirm('¿Cancelar esta reserva?')) return;
    try {
      await cancelReservation(id);
      await load();
    } catch (e) { setError(e?.response?.data || e.message); }
  };

  if (loading) return <div>Cargando tus reservas...</div>;
  if (error) return <div style={{ color: 'crimson' }}>Error: {JSON.stringify(error)}</div>;

  return (
    <div style={{ padding: 12 }}>
      <h3>Mis Reservas</h3>
      {!reservations || reservations.length === 0 ? (
        <div>No tienes reservas.</div>
      ) : (
        <div style={{ display: 'grid', gap: 8 }}>
          {reservations.map(group => (
            <div key={group.eventId} style={{ border: '1px solid #ddd', padding: 10, borderRadius: 6 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <div>
                  <div><strong>Evento:</strong> {group.eventName || '—'}</div>
                  <div><strong>Fecha:</strong> {group.eventDate ? new Date(group.eventDate).toLocaleString() : '—'}</div>
                </div>
                <div>
                  <small>{group.reservations?.length ?? 0} reserva(s)</small>
                </div>
              </div>

              <div style={{ marginTop: 8 }}>
                <strong>Asientos reservados:</strong>
                <ul>
                  {(() => {
                    // ensure seat list is unique by id to avoid duplicate key warnings
                    const seats = group.seats || [];
                    const seen = new Set();
                    return seats.filter(s => {
                      const k = s.seatId || s.id || s.SeatId || s.Id;
                      if (!k) return false;
                      if (seen.has(k)) return false;
                      seen.add(k);
                      return true;
                    }).map(s => (<li key={s.seatId || s.id}>{s.code} ({s.type})</li>));
                  })()}
                </ul>
              </div>

              <div style={{ marginTop: 8 }}>
                <strong>Reservas individuales:</strong>
                <ul>
                  {group.reservations?.map(r => (
                    <li key={r.reservationId} style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                      <span><code>{r.reservationId}</code> — {r.status} — {new Date(r.createdAt).toLocaleString()}</span>
                      <button onClick={() => handleCancel(r.reservationId)} style={{ marginLeft: 12, background: '#f44336', color: 'white' }}>Cancelar</button>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
