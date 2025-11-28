import React, { useState } from 'react';
import { updateEvent } from './src/api';
import { KeycloakProvider, useKeycloak } from './KeycloakProvider';
import CreateUserForm from './src/components/CreateUserForm';
import Landing from './src/components/Landing';
import Organizer from './src/components/Organizer';
import EventDetails from './src/components/EventDetails';
import EventsList from './src/components/EventsList';
import MyEventsLeft from './src/components/MyEventsLeft';
import MyReservations from './src/components/MyReservations';
import EventForm from './src/components/EventForm';
import './src/styles.css';

function InnerApp() {
    // Usamos 'keycloak' y 'initialized' del hook, proporcionado por nuestro KeycloakProvider
    const { keycloak, initialized, authenticated, login, logout } = useKeycloak();

    const [createdUser, setCreatedUser] = useState(null);
    const [selectedEvent, setSelectedEvent] = useState(null);
    const [externalEdit, setExternalEdit] = useState(null);
    const [leftReloadKey, setLeftReloadKey] = useState(0);

    if (!initialized) return <div>Cargando autenticación...</div>;

        if (!authenticated) return <Landing />;

        const userName = authenticated ? keycloak.tokenParsed?.preferred_username : 'N/A';
        const tokenRoles = authenticated && keycloak.tokenParsed?.realm_access ? (keycloak.tokenParsed.realm_access.roles || []) : [];
        const allowed = ['organizador','usuario','administrador'];
        const filtered = tokenRoles.filter(r => allowed.includes(String(r).toLowerCase()));
        const normalized = filtered.map(r => {
            const low = String(r).toLowerCase();
            if (low === 'organizador') return 'Organizador';
            if (low === 'usuario') return 'Usuario';
            if (low === 'administrador' || low === 'admin') return 'Administrador';
            return r;
        });

        const isAdmin = Boolean(keycloak && typeof keycloak.hasRealmRole === 'function' && (keycloak.hasRealmRole('Administrador') || keycloak.hasRealmRole('admin')));
        const isOrganizer = Boolean(keycloak && typeof keycloak.hasRealmRole === 'function' && (keycloak.hasRealmRole('Organizador') || keycloak.hasRealmRole('organizador')));

        return (
            <div className="app-container">
                <div className="header">
                    <div>
                        <div className="title">Plataforma de Eventos</div>
                        <div className="subtitle">Gestiona eventos, reservas y usuarios</div>
                    </div>
                    <div className="controls">
                        <div className="small-muted">{userName}</div>
                        {normalized.map(r => <div key={r} className="role-badge">{r}</div>)}
                        <button className="btn btn-muted" onClick={() => logout()}>Cerrar sesión</button>
                    </div>
                </div>

                        <div className="row">
                                                                <div className="col">
                                                                <div className="card">
                                                                        <h4 className="section-title">Eventos</h4>
                                                                        {!isOrganizer ? (
                                                                                <EventsList />
                                                                        ) : (
                                                                                <div>
                                                                                    <MyEventsLeft onSelectEvent={(id) => setSelectedEvent(id)} onRequestEdit={(initial) => setExternalEdit(initial)} reloadKey={leftReloadKey} />
                                                                                    {externalEdit && (
                                                                                        <div style={{ marginTop: 12, padding: 8, border: '1px dashed #bbb' }}>
                                                                                            <h3>Editar evento</h3>
                                                                                            <EventForm initial={externalEdit} onSubmit={async (payload) => {
                                                                                                try {
                                                                                                    await updateEvent(externalEdit.id, payload);
                                                                                                    setExternalEdit(null);
                                                                                                    setLeftReloadKey(k => k + 1);
                                                                                                } catch (e) { alert('Error actualizando evento: ' + (e?.response?.data || e.message)); }
                                                                                            }} submitText="Actualizar evento" onCancel={() => setExternalEdit(null)} />
                                                                                        </div>
                                                                                    )}
                                                                                </div>
                                                                        )}
                                                                </div>
                                                        </div>

                            <div className="col">
                                {isAdmin && (
                                    <div className="card">
                                        <h4 className="section-title">Administración</h4>
                                        <CreateUserForm onCreated={(data) => setCreatedUser(data)} />
                                    </div>
                                )}

                                {!isOrganizer && (
                                    <div className="card">
                                        <h4 className="section-title">Mis reservas</h4>
                                        <MyReservations />
                                    </div>
                                )}

                                {isOrganizer && (
                                    <div className="card">
                                        <h4 className="section-title">Organizador</h4>
                                        {selectedEvent ? (
                                            <EventDetails eventId={selectedEvent} onClose={() => setSelectedEvent(null)} />
                                        ) : (
                                            <Organizer externalEdit={externalEdit} />
                                        )}
                                    </div>
                                )}
                            </div>
                        </div>

                {createdUser && (
                    <div style={{ marginTop: 12, padding: 8, background: '#eef', borderRadius: 6 }}>
                        <strong>Último usuario creado:</strong>
                        <pre style={{ whiteSpace: 'pre-wrap' }}>{JSON.stringify(createdUser, null, 2)}</pre>
                    </div>
                )}
            </div>
        );
}

export default function App() {
    return (
        <KeycloakProvider>
            <InnerApp />
        </KeycloakProvider>
    );
}

