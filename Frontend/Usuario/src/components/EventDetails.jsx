import React, { useEffect, useState } from 'react';
import { getEventById, deleteSeat } from '../api';
import { useKeycloak } from '../../KeycloakProvider';

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
