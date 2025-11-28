import React, { useState } from 'react';
import { createProfile } from '../api';

export default function CreateUserForm({ onCreated }) {
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [selectedRoles, setSelectedRoles] = useState([]);
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [result, setResult] = useState(null);

  // Roles disponibles: pueden venir de env (VITE_AVAILABLE_ROLES) o lista por defecto
  const availableRoles = (import.meta.env.VITE_AVAILABLE_ROLES || 'Usuario,Organizador,Administrador')
    .split(',')
    .map(r => r.trim())
    .filter(Boolean);

  const toggleRole = (role) => {
    setSelectedRoles(prev => prev.includes(role) ? prev.filter(r => r !== role) : [...prev, role]);
  };

  const onSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    setResult(null);

    if (!username || !email) {
      setError('Username y email son obligatorios.');
      return;
    }

    const payload = {
      username,
      email,
      roles: selectedRoles,
      // send password only if user provided one; backend will generate if null/empty
      password: password && password.length > 0 ? password : null,
    };

    try {
      setLoading(true);
      const resp = await createProfile(payload);
      setResult(resp.data);
      setUsername('');
      setEmail('');
      setSelectedRoles([]);
      if (typeof onCreated === 'function') onCreated(resp.data);
    } catch (err) {
      console.error(err);
      const msg = err?.response?.data || err.message || 'Error desconocido';
      setError(JSON.stringify(msg));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ border: '1px solid #ddd', padding: 12, borderRadius: 6, marginTop: 16 }}>
      <h4>Crear Usuario (Admin)</h4>
      <form onSubmit={onSubmit}>
        <div style={{ marginBottom: 8 }}>
          <label style={{ display: 'block' }}>Username</label>
          <input value={username} onChange={e => setUsername(e.target.value)} />
        </div>
        <div style={{ marginBottom: 8 }}>
          <label style={{ display: 'block' }}>Email</label>
          <input value={email} onChange={e => setEmail(e.target.value)} />
        </div>

        <div style={{ marginBottom: 8 }}>
          <label style={{ display: 'block', marginBottom: 6 }}>Roles</label>
          {availableRoles.map(role => (
            <label key={role} style={{ display: 'inline-block', marginRight: 12 }}>
              <input type="checkbox" checked={selectedRoles.includes(role)} onChange={() => toggleRole(role)} /> {role}
            </label>
          ))}
        </div>

        <div style={{ marginBottom: 8 }}>
          <label style={{ display: 'block' }}>Contraseña (opcional)</label>
          <input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Dejar vacío para generar" />
          <small style={{ display: 'block', color: '#666' }}>Si dejas vacío, el servidor generará una contraseña temporal.</small>
        </div>

        <div>
          <button type="submit" disabled={loading} style={{ padding: '8px 12px' }}>{loading ? 'Creando...' : 'Crear Usuario'}</button>
        </div>
      </form>

      {error && <div style={{ marginTop: 10, color: 'crimson' }}>Error: {error}</div>}
      {result && (
        <div style={{ marginTop: 10, background: '#f6ffed', border: '1px solid #d4f7c4', padding: 8 }}>
          <strong>Usuario creado:</strong>
          <pre style={{ whiteSpace: 'pre-wrap', margin: 0 }}>{JSON.stringify(result.profile ?? result, null, 2)}</pre>
          {result.password ? (
            <div style={{ marginTop: 8, padding: 8, background: '#fff7e6', border: '1px solid #ffd590', borderRadius: 6 }}>
              <strong>Contraseña generada (temporal):</strong>
              <div style={{ fontFamily: 'monospace', marginTop: 6, padding: 6, background: '#fff', borderRadius: 4 }}>{result.password}</div>
              <small>Esta contraseña es temporal; en producción enviar por email o forzar cambio.</small>
            </div>
          ) : (
            <small>Nota: en desarrollo el password generado por el servidor puede o no devolverse en la respuesta.</small>
          )}
        </div>
      )}
    </div>
  );
}
