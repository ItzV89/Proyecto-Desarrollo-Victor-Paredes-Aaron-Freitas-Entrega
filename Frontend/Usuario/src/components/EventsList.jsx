import React, { useEffect, useState } from 'react';
import { getAllEvents } from '../api';
import EventReservation from './EventReservation';
import { useKeycloak } from '../../KeycloakProvider';
import { connectSeatHub } from '../seatHub';

export default function EventsList() {
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [selectedEvent, setSelectedEvent] = useState(null);

  const { keycloak } = useKeycloak() || {};
  const roles = keycloak?.tokenParsed?.realm_access?.roles || [];
  const isOrganizer = Array.isArray(roles) && roles.find(r => String(r).toLowerCase() === 'organizador');

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const resp = await getAllEvents();
      setEvents(resp.data || []);
    } catch (e) {
      setError(e?.response?.data || e.message || 'Error cargando eventos');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  useEffect(() => {
    // subscribe to server-side events so list updates in realtime
    let mounted = true;
    const onEventCreated = (payload) => {
      try {
        const p = payload || {};
        if (!mounted) return;
        // normalize server payload: may contain eventInfo or event
        const newEvent = p.eventInfo ?? p.event ?? p;
        if (!newEvent || !newEvent.id) return;
        setEvents(prev => {
          const exists = prev.find(x => x && x.id === newEvent.id);
          if (exists) return prev;
          return [newEvent, ...prev];
        });
      } catch (e) { console.warn('onEventCreated handler error', e); }
    };
    const onEventUpdated = (payload) => {
      try {
        const p = payload || {};
        const updatedEvent = p.eventInfo ?? p.event ?? p;
        if (!updatedEvent || !updatedEvent.id) return;
        setEvents(prev => prev.map(ev => (ev && ev.id === updatedEvent.id) ? { ...ev, ...updatedEvent } : ev));
      } catch (e) { console.warn('onEventUpdated handler error', e); }
    };
    const onEventDeleted = (payload) => {
      try {
        const p = payload || {};
        const id = p.eventId ?? (p.eventInfo && p.eventInfo.id) ?? (p.event && p.event.id);
        if (!id) return;
        setEvents(prev => prev.filter(ev => ev && ev.id !== id));
      } catch (e) { console.warn('onEventDeleted handler error', e); }
    };

    connectSeatHub(null, null, null, null, onEventCreated, onEventUpdated, onEventDeleted).catch(() => {});

    return () => { mounted = false; };
  }, []);

  if (loading) return <div>Cargando eventos...</div>;
  if (error) return <div style={{ color: 'crimson' }}>Error: {JSON.stringify(error)}</div>;

  if (loading) return <div>Cargando eventos...</div>;
  if (error) return <div style={{ color: 'crimson' }}>Error: {JSON.stringify(error)}</div>;

  // hide the whole EventsList for organizers as requested
  if (isOrganizer) return null;

  return (
    <div style={{ padding: 12 }}>
      <h3>Eventos disponibles</h3>
      {!events || events.length === 0 ? (
        <div>No hay eventos disponibles.</div>
      ) : (
        <div style={{ display: 'grid', gap: 8 }}>
          {events.map(ev => (
            <div key={ev.id} style={{ border: '1px solid #ddd', padding: 10, borderRadius: 6 }}>
              <strong>{ev.name}</strong> <small>({ev.date})</small>
              <div><em>{ev.eventType}</em> - <strong>{ev.place}</strong></div>
              <div style={{ marginTop: 8 }}>{ev.description}</div>
              <div style={{ marginTop: 8 }}>
                <button onClick={() => setSelectedEvent(ev.id)} style={{ padding: '6px 10px' }}>Ver y reservar</button>
              </div>
            </div>
          ))}
        </div>
      )}

      {selectedEvent && (
        <div style={{ marginTop: 16 }}>
          <EventReservation eventId={selectedEvent} onClose={() => { setSelectedEvent(null); load(); }} />
        </div>
      )}
    </div>
  );
}
