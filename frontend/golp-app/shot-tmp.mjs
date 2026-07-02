import { chromium, request as playwrightRequest } from '@playwright/test';

const API = 'http://localhost:5120';
const ts = Date.now();

async function api(method, path, data, token) {
  const ctx = await playwrightRequest.newContext({
    extraHTTPHeaders: token ? { Authorization: `Bearer ${token}` } : {}
  });
  const r = await ctx[method](`${API}${path}`, data ? { data } : undefined);
  const body = await r.json();
  await ctx.dispose();
  return body;
}

// Crea 4 utenti
const u1 = await api('post', '/auth/register', { email: `s1_${ts}@t.com`, password: 'testpass123', name: 'Luca Rossi' });
const u2 = await api('post', '/auth/register', { email: `s2_${ts}@t.com`, password: 'testpass123', name: 'Roberto Fontana' });
const u3 = await api('post', '/auth/register', { email: `s3_${ts}@t.com`, password: 'testpass123', name: 'Beatrice Lombardi' });
const u4 = await api('post', '/auth/register', { email: `s4_${ts}@t.com`, password: 'testpass123', name: 'Federico Gallo' });

const getId = (tok) => JSON.parse(Buffer.from(tok.split('.')[1], 'base64').toString()).sub;
const id1 = getId(u1.token), id2 = getId(u2.token), id3 = getId(u3.token), id4 = getId(u4.token);

// Crea circolo con u1
const circle = await api('post', '/circles', { name: `TestCircolo_${ts}`, sport: 'padel' }, u1.token);
const cid = circle.id;

// Tutti joinano
await api('post', `/circles/${cid}/join`, null, u2.token);
await api('post', `/circles/${cid}/join`, null, u3.token);
await api('post', `/circles/${cid}/join`, null, u4.token);

// Crea partita
const match = await api('post', `/circles/${cid}/matches`, {
  team1: [{ userId: id1 }, { userId: id2 }],
  team2: [{ userId: id3 }, { userId: id4 }],
  sets: [{ team1: 6, team2: 4 }]
}, u1.token);
const mid = match.id;

// u3 conferma (vince team2 secondo i set)
await api('post', `/circles/${cid}/matches/${mid}/confirm`, null, u3.token);

// Browser: apri come u1 (ha partite in attesa)
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 390, height: 844 } });

await page.goto('http://localhost:4200/login');
await page.waitForLoadState('networkidle');
await page.fill('#email', `s1_${ts}@t.com`);
await page.fill('#password', 'testpass123');
await page.click('button[type="submit"]');
await page.waitForURL('**/dashboard', { timeout: 10000 });

// Screenshot my-matches
await page.goto('http://localhost:4200/my-matches');
await page.waitForLoadState('networkidle');
await page.waitForTimeout(1500);
await page.screenshot({ path: 'C:/Users/fabio/AppData/Local/Temp/claude/screenshot-my-matches.png' });
console.log('DONE my-matches');

// Screenshot circle match history
await page.goto(`http://localhost:4200/circles/${cid}/matches`);
await page.waitForLoadState('networkidle');
await page.waitForTimeout(1500);
await page.screenshot({ path: 'C:/Users/fabio/AppData/Local/Temp/claude/screenshot-circle-matches.png' });
console.log('DONE circle-matches');

await browser.close();
