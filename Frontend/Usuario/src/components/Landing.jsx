import React, { useState } from 'react';
import CreateUserForm from './CreateUserForm';
import { useKeycloak } from '../../KeycloakProvider';

export default function Landing() {
  const { login } = useKeycloak();
  const [mode, setMode] = useState(null); // null | 'create'
  const [created, setCreated] = useState(null);

  if (!mode) {
    return (
      <div style={{ padding: 20 }}>
        <h1>Bienvenido</h1>
        <p>Elige una opción:</p>
        <div style={{ marginTop: 16 }}>
          <button onClick={() => login()} style={{ padding: 10, marginRight: 12 }}>Iniciar Sesión</button>
          <button onClick={() => setMode('create')} style={{ padding: 10 }}>Crear Cuenta</button>
        </div>
      </div>
    );
  }

  if (mode === 'create') {
    return (
      <div style={{ padding: 20 }}>
        <h2>Crear Cuenta</h2>
        <p>Rellena los datos y marca el rol apropiado.</p>
        <CreateUserForm onCreated={(data) => setCreated(data)} />

        {created && (
          <div style={{ marginTop: 12 }}>
            <p>Cuenta creada correctamente.</p>
            {created.password && (
              <div style={{ marginBottom: 8 }}>
                <strong>Contraseña temporal:</strong>
                <div style={{ fontFamily: 'monospace', background: '#fff', padding: 8, borderRadius: 4 }}>{created.password}</div>
              </div>
            )}
            <p>Ahora puedes iniciar sesión con tus credenciales.</p>
            <div style={{ marginTop: 8 }}>
              <button onClick={() => login()} style={{ padding: 8 }}>Ir a Iniciar Sesión</button>
              <button onClick={() => { setMode(null); setCreated(null); }} style={{ marginLeft: 8, padding: 8 }}>Volver</button>
            </div>
          </div>
        )}

        <div style={{ marginTop: 16 }}>
          <button onClick={() => setMode(null)} style={{ padding: 6 }}>Volver</button>
        </div>
      </div>
    );
  }

  return null;
}
