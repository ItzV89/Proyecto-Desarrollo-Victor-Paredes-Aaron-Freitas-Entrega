import fs from 'fs';

const base = 'http://localhost:8080';
const out = {};

async function postForm(url, form) {
  const body = new URLSearchParams(form);
  const res = await fetch(url, { method: 'POST', body });
  const text = await res.text();
  return { status: res.status, ok: res.ok, body: text };
}

async function postJson(url, json, token) {
  const res = await fetch(url, { method: 'POST', body: JSON.stringify(json), headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` } });
  const text = await res.text();
  return { status: res.status, ok: res.ok, body: text };
}

(async () => {
  try {
    const t = await postForm(`${base}/realms/master/protocol/openid-connect/token`, { client_id: 'admin-cli', username: 'admin', password: 'admin', grant_type: 'password' });
    out.tokenRequest = t;
    if (!t.ok) {
      fs.writeFileSync('keycloak_result.json', JSON.stringify(out, null, 2));
      process.exit(1);
    }
    const token = JSON.parse(t.body).access_token;
    out.token = { length: token ? token.length : 0 };

    // List realms
    const realmsRes = await fetch(`${base}/admin/realms`, { headers: { Authorization: `Bearer ${token}` } });
    out.listRealms = { status: realmsRes.status };
    out.realmsBody = await realmsRes.text();

    // Create realm
    const realm = { realm: 'plataforma-eventos', enabled: true };
    out.createRealm = await postJson(`${base}/admin/realms`, realm, token);

    // Create clients
    const client1 = { clientId: 'react-app-client', publicClient: true, redirectUris: ['http://localhost:5173/*','http://localhost:5173/'], webOrigins: ['http://localhost:5173'], standardFlowEnabled: true };
    out.createClient1 = await postJson(`${base}/admin/realms/plataforma-eventos/clients`, client1, token);

    const client2 = { clientId: 'authuser-admin-client', publicClient: false, serviceAccountsEnabled: true, secret: 'ffs6Czk5UNCbEE0NX0oS1ZbjICYnigWp', standardFlowEnabled: false };
    out.createClient2 = await postJson(`${base}/admin/realms/plataforma-eventos/clients`, client2, token);

    fs.writeFileSync('keycloak_result.json', JSON.stringify(out, null, 2));
    console.log('Done. Wrote keycloak_result.json');
  } catch (err) {
    out.error = String(err);
    fs.writeFileSync('keycloak_result.json', JSON.stringify(out, null, 2));
    console.error(err);
    process.exit(1);
  }
})();
