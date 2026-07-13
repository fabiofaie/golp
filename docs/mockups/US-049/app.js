// Mockup-only: nessuna chiamata reale, stato simulato in memoria.
// L'algoritmo qui è una euristica DEMO (shuffle + bilanciamento rating). La vera logica
// (storico coppie, tie-break RatingMethod, rotazione riposi equa) è descritta in docs/planning/US-049.md.

const MEMBERS = [
  { id: 'u1', name: 'Marco',   initials: 'MR', rating: 1180, present: false },
  { id: 'u2', name: 'Giulia',  initials: 'GL', rating: 1042, present: false },
  { id: 'u3', name: 'Luca',    initials: 'LC', rating: 1305, present: false },
  { id: 'u4', name: 'Sara',    initials: 'SR', rating:  980, present: false },
  { id: 'u5', name: 'Andrea',  initials: 'AN', rating: 1120, present: false },
  { id: 'u6', name: 'Elena',   initials: 'EL', rating: 1240, present: false },
  { id: 'u7', name: 'Paolo',   initials: 'PL', rating: 1010, present: false },
  { id: 'u8', name: 'Chiara',  initials: 'CH', rating: 1090, present: false },
];

let guests = [];
let plan = []; // array di turni: [{ matches: [{team1,team2}], resting: [] }]
let activeRoundIndex = 0;
let selectedPlayer = null; // { roundIdx, matchIdx, team, playerId }

let courts = 1;
let targetMode = 'Total'; // 'Total' | 'PerPlayer'
let targetValue = 4;

const grid = document.getElementById('member-grid');
const addGuestChip = document.getElementById('add-guest-chip');
const guestRow = document.getElementById('guest-input-row');
const guestInput = document.getElementById('guest-name-input');
const guestConfirmBtn = document.getElementById('guest-confirm-btn');
const presenceCount = document.getElementById('presence-count');
const presenceHint = document.getElementById('presence-hint');
const generateBtn = document.getElementById('generate-btn');
const proposalSection = document.getElementById('proposal-section');
const roundTabsEl = document.getElementById('round-tabs');
const roundsContainer = document.getElementById('rounds-container');
const planSummary = document.getElementById('plan-summary');
const toast = document.getElementById('toast');

const courtsValueEl = document.getElementById('courts-value');
const targetValueEl = document.getElementById('target-value');
const targetValueLabel = document.getElementById('target-value-label');
const targetSublabel = document.getElementById('target-sublabel');

function allPeople() {
  return [...MEMBERS, ...guests];
}

function renderGrid() {
  grid.querySelectorAll('.member-chip:not(.add-guest)').forEach(el => el.remove());
  allPeople().forEach(person => {
    const chip = document.createElement('button');
    chip.type = 'button';
    chip.className = 'member-chip' + (person.present ? ' present' : '');
    chip.innerHTML = `
      <div class="avatar">${person.initials || person.name.slice(0,2).toUpperCase()}</div>
      <div class="name">${person.name}</div>
    `;
    chip.addEventListener('click', () => {
      person.present = !person.present;
      renderGrid();
      updatePresenceCounter();
    });
    grid.insertBefore(chip, addGuestChip);
  });
}

function updatePresenceCounter() {
  const count = allPeople().filter(p => p.present).length;
  presenceCount.textContent = `${count} present${count === 1 ? 'e' : 'i'}`;
  presenceCount.classList.toggle('ready', count >= 4);
  presenceHint.textContent = count >= 4
    ? 'pronto per generare il piano'
    : `servono almeno 4 presenti (mancano ${4 - count})`;

  generateBtn.classList.toggle('ready', count >= 4);
  if (!generateBtn.classList.contains('regenerate')) {
    generateBtn.textContent = count >= 4
      ? `Genera piano (${count} presenti)`
      : 'Genera piano (min. 4 presenti)';
  }
}

addGuestChip.addEventListener('click', () => {
  guestRow.classList.toggle('visible');
  if (guestRow.classList.contains('visible')) guestInput.focus();
});

guestConfirmBtn.addEventListener('click', () => {
  const name = guestInput.value.trim();
  if (!name) return;
  guests.push({ id: 'guest-' + Date.now(), name, initials: name.slice(0,2).toUpperCase(), rating: 1000, present: true, isGuest: true });
  guestInput.value = '';
  guestRow.classList.remove('visible');
  renderGrid();
  updatePresenceCounter();
});

// --- Stepper campi ---
document.getElementById('courts-stepper').addEventListener('click', (e) => {
  const btn = e.target.closest('button');
  if (!btn) return;
  const delta = Number(btn.dataset.delta);
  courts = Math.max(1, Math.min(8, courts + delta));
  courtsValueEl.textContent = courts;
});

// --- Toggle obiettivo Total / PerPlayer ---
document.getElementById('target-toggle').addEventListener('click', (e) => {
  const btn = e.target.closest('button');
  if (!btn) return;
  targetMode = btn.dataset.mode;
  document.querySelectorAll('#target-toggle button').forEach(b => b.classList.toggle('active', b === btn));
  if (targetMode === 'Total') {
    targetValueLabel.textContent = 'Partite totali';
    targetSublabel.textContent = 'quante partite in totale nel raduno';
    targetValue = 4;
  } else {
    targetValueLabel.textContent = 'Partite a testa';
    targetSublabel.textContent = 'quante partite vuole giocare ciascun presente';
    targetValue = 2;
  }
  targetValueEl.textContent = targetValue;
});

// --- Stepper obiettivo valore ---
document.getElementById('target-stepper').addEventListener('click', (e) => {
  const btn = e.target.closest('button');
  if (!btn) return;
  const delta = Number(btn.dataset.delta);
  const max = targetMode === 'Total' ? 40 : 10;
  targetValue = Math.max(1, Math.min(max, targetValue + delta));
  targetValueEl.textContent = targetValue;
});

// --- Pianificazione multi-turno simulata (demo): calcola numero turni da courts+target,
// poi per ognuno genera round bilanciati per rating, evitando (quando possibile) di
// ripetere le stesse coppie già usate nel piano corrente.
function buildPlan() {
  const present = allPeople().filter(p => p.present);
  const n = present.length;

  let roundsNeeded;
  if (targetMode === 'Total') {
    roundsNeeded = Math.ceil(targetValue / courts);
  } else {
    // ogni turno assegna al massimo courts*4 slot-giocatore; distribuiamo finché
    // (in media) ognuno ha giocato targetValue partite
    const totalSlotsNeeded = targetValue * n;
    roundsNeeded = Math.ceil(totalSlotsNeeded / (courts * 4));
  }
  roundsNeeded = Math.max(1, Math.min(20, roundsNeeded));

  const playedTogether = new Set(); // chiavi "id1|id2" già in coppia nel piano
  const restCount = {}; // quante volte ciascuno ha riposato finora nel piano
  present.forEach(p => restCount[p.id] = 0);

  const generatedRounds = [];

  for (let r = 0; r < roundsNeeded; r++) {
    // ordina per chi ha riposato di più -> priorità a giocare
    const pool = [...present].sort((a, b) => (restCount[b.id] - restCount[a.id]) || (Math.random() - 0.5));
    const capacity = courts * 4;
    const playing = pool.slice(0, capacity);
    const resting = pool.slice(capacity);
    resting.forEach(p => restCount[p.id]++);

    const matches = [];
    const byRating = [...playing].sort((a, b) => b.rating - a.rating);
    for (let i = 0; i < byRating.length; i += 4) {
      const group = byRating.slice(i, i + 4);
      if (group.length < 4) break; // gruppo incompleto, scarta (demo semplificata)
      const [a, b, c, d] = group;
      // euristica: 1°+4° vs 2°+3°, marcato come "già giocato" per il tie-break dei turni successivi
      const key = [a.id, b.id, c.id, d.id].sort().join('|');
      matches.push({ team1: [a, d], team2: [b, c] });
      playedTogether.add(key);
    }

    generatedRounds.push({ matches, resting });
  }

  plan = generatedRounds;
  return { roundsNeeded };
}

function renderPlan() {
  planSummary.innerHTML = `Piano di <strong>${plan.length} turn${plan.length === 1 ? 'o' : 'i'}</strong> su <strong>${courts} camp${courts === 1 ? 'o' : 'i'}</strong> — obiettivo: <strong>${targetMode === 'Total' ? targetValue + ' partite totali' : targetValue + ' partite a testa'}</strong>`;

  roundTabsEl.innerHTML = '';
  plan.forEach((_, idx) => {
    const tab = document.createElement('button');
    tab.type = 'button';
    tab.className = 'round-tab' + (idx === activeRoundIndex ? ' active' : '');
    tab.textContent = `Turno ${idx + 1}`;
    tab.addEventListener('click', () => {
      activeRoundIndex = idx;
      renderPlan();
    });
    roundTabsEl.appendChild(tab);
  });

  renderRound(activeRoundIndex);
}

function renderRound(roundIdx) {
  const round = plan[roundIdx];
  roundsContainer.innerHTML = '';

  round.matches.forEach((m, matchIdx) => {
    const card = document.createElement('div');
    card.className = 'round-card';
    card.innerHTML = `
      <div class="round-header">Campo ${matchIdx + 1}</div>
      <div class="round-teams">
        <div class="team-block team1" data-match="${matchIdx}" data-team="1">
          ${m.team1.map(p => playerRow(p, matchIdx, 1)).join('')}
        </div>
        <div class="vs-divider">VS</div>
        <div class="team-block team2" data-match="${matchIdx}" data-team="2">
          ${m.team2.map(p => playerRow(p, matchIdx, 2)).join('')}
        </div>
      </div>
      <button class="confirm-round-btn" data-match="${matchIdx}">Registra questa partita</button>
    `;
    roundsContainer.appendChild(card);
  });

  if (round.resting.length > 0) {
    const strip = document.createElement('div');
    strip.className = 'resting-strip';
    strip.innerHTML = `<span class="icon">⏸</span> Riposa in questo turno: ${round.resting.map(p => p.name).join(', ')} — priorità nel prossimo`;
    roundsContainer.appendChild(strip);
  }

  attachRoundHandlers(roundIdx);
}

function playerRow(p, matchIdx, team) {
  return `
    <div class="player" data-match="${matchIdx}" data-team="${team}" data-player="${p.id}">
      <div class="mini-avatar">${p.initials}</div>
      <span>${p.name}${p.isGuest ? ' (ospite)' : ''}</span>
    </div>
  `;
}

function attachRoundHandlers(roundIdx) {
  document.querySelectorAll('.player').forEach(el => {
    el.addEventListener('click', () => {
      const matchIdx = Number(el.dataset.match);
      const team = Number(el.dataset.team);
      const playerId = el.dataset.player;

      if (!selectedPlayer) {
        selectedPlayer = { matchIdx, team, playerId };
        el.classList.add('selected');
        return;
      }

      if (selectedPlayer.matchIdx !== matchIdx) {
        clearSelection();
        selectedPlayer = { matchIdx, team, playerId };
        el.classList.add('selected');
        return;
      }

      if (selectedPlayer.playerId === playerId) {
        clearSelection();
        return;
      }

      swapPlayers(roundIdx, matchIdx, selectedPlayer, { team, playerId });
      clearSelection();
      renderRound(roundIdx);
      showToast('Giocatori scambiati');
    });
  });

  document.querySelectorAll('.confirm-round-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      const matchIdx = Number(btn.dataset.match);
      const m = plan[roundIdx].matches[matchIdx];
      const names = [...m.team1, ...m.team2].map(p => p.name).join(', ');
      showToast(`→ Apertura "Registra partita" precompilata con: ${names}`);
    });
  });
}

function clearSelection() {
  document.querySelectorAll('.player.selected').forEach(el => el.classList.remove('selected'));
  selectedPlayer = null;
}

function swapPlayers(roundIdx, matchIdx, a, b) {
  const match = plan[roundIdx].matches[matchIdx];
  const teamA = match['team' + a.team];
  const teamB = match['team' + b.team];
  const idxA = teamA.findIndex(p => p.id === a.playerId);
  const idxB = teamB.findIndex(p => p.id === b.playerId);
  if (idxA === -1 || idxB === -1) return;
  const tmp = teamA[idxA];
  teamA[idxA] = teamB[idxB];
  teamB[idxB] = tmp;
}

generateBtn.addEventListener('click', () => {
  const count = allPeople().filter(p => p.present).length;
  if (count < 4) return;
  buildPlan();
  activeRoundIndex = 0;
  renderPlan();
  proposalSection.classList.add('visible');
  generateBtn.textContent = 'Rigenera piano';
  generateBtn.classList.add('regenerate');
  proposalSection.scrollIntoView({ behavior: 'smooth' });
});

function showToast(msg) {
  toast.textContent = msg;
  toast.classList.add('visible');
  clearTimeout(showToast._t);
  showToast._t = setTimeout(() => toast.classList.remove('visible'), 2400);
}

renderGrid();
updatePresenceCounter();
