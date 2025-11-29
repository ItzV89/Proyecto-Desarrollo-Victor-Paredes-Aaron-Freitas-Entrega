import React, { useState, useEffect, createContext, useContext } from 'react';
import Keycloak from 'keycloak-js';
import { setupAxios, teardownAxios } from './src/api';

// 1. Crear el contexto para acceder al estado y funciones de Keycloak
const KeycloakContext = createContext(null);

// 2. Definición de la configuración de Keycloak (valores por defecto alineados con el backend)
// Usamos `import.meta.env` para Vite (las variables expuestas deben comenzar con `VITE_`)
const keycloakConfig = {
    url: import.meta.env.VITE_KEYCLOAK_URL || 'http://localhost:8080',
    realm: import.meta.env.VITE_KEYCLOAK_REALM || 'plataforma-eventos',
    clientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID || 'react-app-client',
};

export const KeycloakProvider = ({ children }) => {
    const [keycloak, setKeycloak] = useState(null);
    const [authenticated, setAuthenticated] = useState(false);
    const [initialized, setInitialized] = useState(false);

    useEffect(() => {
        const kc = new Keycloak(keycloakConfig);
        // Permitir configurar el comportamiento `onLoad` vía env (Vite): 'login-required' o 'check-sso'.
        // Por defecto NO pasamos `onLoad` a `kc.init` para evitar redirecciones automáticas de Keycloak.
        // Si se desea comportamiento automático, establecer `VITE_KEYCLOAK_ONLOAD` a 'check-sso' o 'login-required'.
        const onLoadBehavior = import.meta.env.VITE_KEYCLOAK_ONLOAD;
        let intervalId;

        // Inicializar Keycloak usando PKCE. Por defecto usamos 'login-required' para evitar
        // comprobaciones silenciosas que pueden provocar parpadeos si Keycloak aún no responde.
        const initOptions = {
            pkceMethod: 'S256',
            checkLoginIframe: false,
        };

        if (onLoadBehavior) initOptions.onLoad = onLoadBehavior;

        // NOTA: intencionalmente NO configuramos `silentCheckSsoRedirectUri` aquí. Esa opción
        // activa el flujo de detección de cookies de terceros de Keycloak que usa iframes
        // y puede ser bloqueado por políticas CSP.
        kc.init(initOptions)
        .then((auth) => {
            setKeycloak(kc);
            setAuthenticated(auth);
            setInitialized(true);

            // instalar interceptor axios usando la instancia de Keycloak
            try { setupAxios(kc); } catch (e) { console.warn('setupAxios failed', e); }

            // Si está autenticado, programar refresco de token
            if (auth) {
                kc.updateToken(70).catch(() => {});
                intervalId = setInterval(() => {
                    kc.updateToken(70).then((refreshed) => {
                        if (refreshed) console.log('Token refrescado.');
                    }).catch(() => {
                        console.warn('No se pudo refrescar el token.');
                    });
                }, 60000);
            }
        })
        .catch((error) => {
            console.error('Error al inicializar Keycloak:', error);
            setInitialized(true);
        });

        return () => {
            if (intervalId) clearInterval(intervalId);
            try { teardownAxios(); } catch (e) {}
        };
    }, []);

    // 3. Pasar el token de acceso para las peticiones a la API y exponer helpers
    const getAccessToken = () => keycloak?.token;
    const login = (options) => keycloak?.login(options);
    const logout = (options) => keycloak?.logout(options);

    // 4. Mostrar cargando mientras Keycloak se inicializa
    if (!initialized) {
        return <div>Cargando autenticación...</div>;
    }

    return (
        <KeycloakContext.Provider value={{ keycloak, authenticated, initialized, getAccessToken, login, logout }}>
            {children}
        </KeycloakContext.Provider>
    );
};

export const useKeycloak = () => useContext(KeycloakContext);