import React, { useState, useEffect } from 'react';

export default function EventForm({ initial = null, onSubmit, submitText = 'Guardar', onCancel }) {
  const [name, setName] = useState('');
  const [eventType, setEventType] = useState('');
  const [description, setDescription] = useState('');
  const [place, setPlace] = useState('');
  const [date, setDate] = useState('');
  const [seatTypes, setSeatTypes] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (initial) {
      setName(initial.name || '');
      setEventType(initial.eventType || '');
      setDescription(initial.description || '');
      setPlace(initial.place || '');
      // convert server ISO date to input-friendly local datetime if available
      if (initial.date) {
        try {
          const d = new Date(initial.date);
          const off = d.getTimezoneOffset();
          const local = new Date(d.getTime() - off * 60000);
          setDate(local.toISOString().slice(0,16));
        } catch { setDate(initial.date); }
      }
      // Populate seatTypes from initial if available. If not provided, try to derive
      // seatTypes from initial.scenarios to avoid sending an empty array when
      // updating an event (which would remove all seats server-side).
      if (initial.seatTypes && Array.isArray(initial.seatTypes)) {
        setSeatTypes(initial.seatTypes.map(st => ({ name: st.name || '', quantity: st.quantity || 0, price: st.price || 0 })));
      } else if (initial.scenarios && Array.isArray(initial.scenarios) && initial.scenarios.length > 0) {
        // derive seatTypes by grouping seats of the first scenario (common pattern)
        const seats = initial.scenarios.flatMap(s => s.seats || []);
        const grouped = {};
        seats.forEach(s => {
          const key = s.type || 'General';
          if (!grouped[key]) grouped[key] = { name: key, quantity: 0, price: s.price ?? 0 };
          grouped[key].quantity += 1;
        });
        setSeatTypes(Object.values(grouped));
      }
    } else {
      setName(''); setEventType(''); setDescription(''); setPlace(''); setDate(''); setSeatTypes([]);
    }
  }, [initial]);

  const addSeatType = () => setSeatTypes(s => [...s, { name: '', quantity: 0, price: 0 }]);
  const updateSeatType = (idx, key, value) => setSeatTypes(s => s.map((st,i) => i===idx ? { ...st, [key]: value } : st));
  const removeSeatType = (idx) => setSeatTypes(s => s.filter((_,i) => i!==idx));

  const totalSeats = seatTypes.reduce((acc, t) => acc + (parseInt(t.quantity||0) || 0), 0);

  const handle = async (e) => {
    e.preventDefault();
    setError(null);
    // validate required fields before sending
    if (!name || !date) {
      setError('Nombre y fecha son obligatorios.');
      return;
    }
    setLoading(true);
    try {
      const payload = {
        name,
        date: date ? new Date(date).toISOString() : null,
        seatTypes: seatTypes.map(st => ({ name: st.name, quantity: parseInt(st.quantity||0)||0, price: parseFloat(st.price||0)||0 })),
        description,
        place,
        eventType
      };
      await onSubmit(payload);
      // reset only when creating new
      if (!initial) {
        setName(''); setEventType(''); setDescription(''); setPlace(''); setDate(''); setSeatTypes([]);
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handle} style={{ marginTop: 8 }}>
      <div style={{ marginBottom: 8 }}>
        <label style={{ display: 'block' }}>Nombre</label>
        <input value={name} onChange={e => setName(e.target.value)} required />
      </div>
      <div style={{ marginBottom: 8 }}>
        <label style={{ display: 'block' }}>Tipo de evento</label>
        <input value={eventType} onChange={e => setEventType(e.target.value)} />
      </div>
      <div style={{ marginBottom: 8 }}>
        <label style={{ display: 'block' }}>Descripción</label>
        <textarea value={description} onChange={e => setDescription(e.target.value)} rows={3} />
      </div>
      <div style={{ marginBottom: 8 }}>
        <label style={{ display: 'block' }}>Lugar</label>
        <input value={place} onChange={e => setPlace(e.target.value)} />
      </div>
      <div style={{ marginBottom: 8 }}>
        <label style={{ display: 'block' }}>Fecha</label>
        <input required type="datetime-local" value={date} onChange={e => setDate(e.target.value)} />
      </div>

      <div style={{ marginTop: 12, marginBottom: 12 }}>
        <h4>Tipos de asientos</h4>
        <div style={{ marginBottom: 8 }}>
          <button type="button" onClick={addSeatType}>Agregar tipo de asiento</button>
        </div>
        {seatTypes.map((st, idx) => (
          <div key={idx} style={{ display: 'flex', gap: 8, marginBottom: 6, alignItems: 'center' }}>
            <input placeholder="Nombre" value={st.name} onChange={e => updateSeatType(idx, 'name', e.target.value)} style={{width:120}} />
            <input placeholder="Cantidad" type="number" min={0} value={st.quantity} onChange={e => updateSeatType(idx, 'quantity', e.target.value)} style={{width:100}} />
            <input placeholder="Precio" type="number" step="0.01" min={0} value={st.price} onChange={e => updateSeatType(idx, 'price', e.target.value)} style={{width:120}} />
            <button type="button" onClick={() => removeSeatType(idx)} style={{background:'#f44336',color:'white'}}>Eliminar</button>
          </div>
        ))}
        <div style={{ marginTop: 8 }}><strong>Total asientos:</strong> {totalSeats}</div>
      </div>

      <div>
        <button type="submit" disabled={loading} style={{ padding: '6px 10px' }}>{loading ? 'Enviando...' : submitText}</button>
        {onCancel && <button type="button" onClick={onCancel} style={{ marginLeft: 8 }}>Cancelar</button>}
      </div>
      {error && <div style={{ marginTop: 8, color: 'crimson' }}>Error: {error}</div>}
    </form>
  );
}
