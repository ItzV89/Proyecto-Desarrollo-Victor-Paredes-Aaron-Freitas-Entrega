import React, { useState } from 'react';
import { createEvent } from '../api';
import EventForm from './EventForm';

export default function Organizer() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const onCreate = async (payload) => {
    setLoading(true);
    try {
      await createEvent(payload);
      setError(null);
    } catch (e) { setError(e?.response?.data || e.message); }
    setLoading(false);
  };

  return (
    <div style={{ padding: 16 }}>
      <h2>Panel de Organizador</h2>
      <p>Gestiona tus eventos: crea, edita o elimina.</p>
      {error && <div style={{ color: 'crimson' }}>Error: {JSON.stringify(error)}</div>}

      <div style={{ marginTop: 12, marginBottom: 12 }}>
        <h3>Crear nuevo evento</h3>
        <EventForm onSubmit={onCreate} submitText="Crear evento" />
      </div>
    </div>
  );
}
