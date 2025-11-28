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

async function getJson(url, token) {
  const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
  const text = await res.text();
  return { status: res.status, ok: res.ok, body: text };
}

(async () => {
  try {
    const t = await postForm(`${base}/realms/master/protocol/openid-connect/token`, { client_id: 'admin-cli', username: 'admin', password: 'admin', grant_type: 'password' });
    out.tokenRequest = t;
    if (!t.ok) {
      fs.writeFileSync('keycloak_roles_result.json', JSON.stringify(out, null, 2));
      process.exit(1);
    }
    const token = JSON.parse(t.body).access_token;
    out.token = { length: token ? token.length : 0 };

    const roles = ['Usuario','Organizador','Administrador','admin','organizador'];
    out.create = [];
    for (const r of roles) {
      const res = await postJson(`${base}/admin/realms/plataforma-eventos/roles`, { name: r }, token);
      out.create.push({ role: r, result: res });
    }

    const list = await getJson(`${base}/admin/realms/plataforma-eventos/roles`, token);
    out.list = list;

    fs.writeFileSync('keycloak_roles_result.json', JSON.stringify(out, null, 2));
    console.log('Done. Wrote keycloak_roles_result.json');
  } catch (err) {
    out.error = String(err);
    fs.writeFileSync('keycloak_roles_result.json', JSON.stringify(out, null, 2));
    console.error(err);
    process.exit(1);
  }
})();
