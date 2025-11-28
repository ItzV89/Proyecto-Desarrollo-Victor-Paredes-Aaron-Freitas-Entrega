import React, { useEffect, useState } from 'react';
import { getMyEvents, getEventById, deleteEvent, removeSeats } from '../api';

export default function MyEventsLeft({ onSelectEvent, onRequestEdit, reloadKey }) {
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const load = async () => {
    setLoading(true); setError(null);
    try {
      const resp = await getMyEvents();
      setEvents(resp.data || []);
    } catch (e) { setError(e?.response?.data || e.message); }
    setLoading(false);
  };

  useEffect(() => { load(); }, []);

  useEffect(() => {
    // when parent signals a reload (e.g., after update), refresh list
    if (typeof reloadKey !== 'undefined') load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reloadKey]);

  if (loading) return <div>Cargando eventos...</div>;
  if (error) return <div style={{ color: 'crimson' }}>Error: {JSON.stringify(error)}</div>;

  return (
    <div style={{ display: 'grid', gap: 8 }}>
      {events.map(ev => (
        <div key={ev.id} style={{ border: '1px solid #ddd', padding: 10, borderRadius: 6 }}>
          <strong>{ev.name}</strong> <small>({ev.date})</small>
          <div><em>{ev.eventType}</em> - <strong>{ev.place}</strong></div>
          <div style={{ marginTop: 8 }}>{ev.description}</div>
          <div style={{ marginTop: 8 }}>
            <button onClick={() => onSelectEvent && onSelectEvent(ev.id)} style={{ padding: '6px 10px', marginRight: 8 }}>Ver detalles</button>
            <button onClick={async () => {
              if (!confirm('¿Eliminar este evento?')) return;
              try { await deleteEvent(ev.id); await load(); } catch (e) { alert('Error: ' + (e?.response?.data || e.message)); }
            }} style={{ padding: '6px 10px', marginRight: 8, background:'#f44336', color:'white' }}>Eliminar</button>
            <button onClick={async () => {
              try {
                const full = (await getEventById(ev.id)).data;
                // transform scenarios->seatTypes for editing form
                let seatTypes = [];
                if (full?.scenarios && full.scenarios.length > 0) {
                  const seats = full.scenarios[0].seats || [];
                  const grouped = {};
                  seats.forEach(s => {
                    const key = s.type || 'General';
                    if (!grouped[key]) grouped[key] = { name: key, quantity: 0, price: s.price ?? 0 };
                    grouped[key].quantity += 1;
                  });
                  seatTypes = Object.values(grouped);
                }
                const initial = { id: full.id, name: full.name, date: full.date, description: full.description, place: full.place, eventType: full.eventType, seatTypes };
                onRequestEdit && onRequestEdit(initial);
              } catch (e) { alert('Error cargando evento para editar'); }
            }} style={{ padding: '6px 10px' }}>Editar</button>
          </div>
        </div>
      ))}
    </div>
  );
}
