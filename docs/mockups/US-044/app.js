// ─── Dataset simulato ────────────────────────────────────────────────────────
const ALL_MATCHES = [
  {
    matchId: '1', circleId: 'c1', circleName: 'Padel Roma', sport: 'padel',
    createdAt: '2026-06-30T18:30:00Z', status: 'confirmed',
    winnerTeam: 1, myTeam: 1,
    sets: [{t1:6,t2:3},{t1:7,t2:5}],
    myDelta: 18, hasCurrentUserConfirmed: true,
  },
  {
    matchId: '2', circleId: 'c2', circleName: 'Beach Ostia', sport: 'beach tennis',
    createdAt: '2026-06-28T15:00:00Z', status: 'confirmed',
    winnerTeam: 2, myTeam: 1,
    sets: [{t1:4,t2:6},{t1:3,t2:6}],
    myDelta: -11, hasCurrentUserConfirmed: true,
  },
  {
    matchId: '3', circleId: 'c1', circleName: 'Padel Roma', sport: 'padel',
    createdAt: '2026-06-26T20:00:00Z', status: 'pending',
    winnerTeam: 1, myTeam: 2,
    sets: [{t1:6,t2:2},{t1:6,t2:4}],
    myDelta: null, hasCurrentUserConfirmed: false,
  },
  {
    matchId: '4', circleId: 'c3', circleName: 'Quick · 25 giu', sport: 'padel',
    createdAt: '2026-06-25T12:00:00Z', status: 'confirmed',
    winnerTeam: 1, myTeam: 1,
    sets: [{t1:6,t2:4},{t1:6,t2:3}],
    myDelta: 14, hasCurrentUserConfirmed: true,
  },
  {
    matchId: '5', circleId: 'c1', circleName: 'Padel Roma', sport: 'padel',
    createdAt: '2026-06-22T19:00:00Z', status: 'disputed',
    winnerTeam: 1, myTeam: 1,
    sets: [{t1:6,t2:2},{t1:6,t2:1}],
    myDelta: null, hasCurrentUserConfirmed: true,
  },
  {
    matchId: '6', circleId: 'c2', circleName: 'Beach Ostia', sport: 'beach tennis',
    createdAt: '2026-06-20T16:00:00Z', status: 'confirmed',
    winnerTeam: 2, myTeam: 2,
    sets: [{t1:3,t2:6},{t1:6,t2:4},{t1:2,t2:6}],
    myDelta: -9, hasCurrentUserConfirmed: true,
  },
  {
    matchId: '7', circleId: 'c1', circleName: 'Padel Roma', sport: 'padel',
    createdAt: '2026-06-18T11:00:00Z', status: 'pending',
    winnerTeam: 2, myTeam: 2,
    sets: [{t1:4,t2:6},{t1:4,t2:6}],
    myDelta: null, hasCurrentUserConfirmed: true,
  },
  {
    matchId: '8', circleId: 'c1', circleName: 'Padel Roma', sport: 'padel',
    createdAt: '2026-06-15T20:00:00Z', status: 'confirmed',
    winnerTeam: 1, myTeam: 1,
    sets: [{t1:6,t2:4},{t1:6,t2:3}],
    myDelta: 21, hasCurrentUserConfirmed: true,
  },
];

const PAGE_SIZE = 5;
let currentFilter = 'all';
let visibleCount = PAGE_SIZE;

function filteredMatches() {
  if (currentFilter === 'all') return ALL_MATCHES;
  return ALL_MATCHES.filter(m => m.status === currentFilter);
}

function formatDate(iso) {
  const d = new Date(iso);
  return d.toLocaleDateString('it-IT', { day: '2-digit', month: 'short', year: '2-digit' });
}

function formatScore(sets) {
  return sets.map(s => `${s.t1}–${s.t2}`).join(' / ');
}

function renderMatch(m) {
  const isWin = m.status === 'confirmed' && m.winnerTeam === m.myTeam;
  const isLoss = m.status === 'confirmed' && m.winnerTeam !== m.myTeam;

  let rowClass = 'match-row';
  if (m.status === 'pending')   rowClass += ' match-row--pending';
  else if (m.status === 'disputed') rowClass += ' match-row--disputed';
  else if (isWin)  rowClass += ' match-row--win';
  else if (isLoss) rowClass += ' match-row--loss';

  let resultHtml = '';
  if (m.status === 'confirmed') {
    if (isWin)  resultHtml = `<span class="result-pill result-pill--win">WIN</span>`;
    else        resultHtml = `<span class="result-pill result-pill--loss">LOSS</span>`;
  } else {
    resultHtml = `<span class="result-pill result-pill--dash">—</span>`;
  }

  let statusHtml = '';
  if (m.status === 'confirmed') statusHtml = `<span class="status-pip status-pip--confirmed">Confermata</span>`;
  else if (m.status === 'pending')   statusHtml = `<span class="status-pip status-pip--pending">In attesa</span>`;
  else if (m.status === 'disputed')  statusHtml = `<span class="status-pip status-pip--disputed">Disputata</span>`;

  let deltaHtml = '';
  if (m.myDelta !== null && m.myDelta !== undefined) {
    const sign = m.myDelta >= 0 ? '+' : '';
    const cls  = m.myDelta >= 0 ? 'delta-value--pos' : 'delta-value--neg';
    deltaHtml = `<span class="delta-value ${cls}">${sign}${m.myDelta}</span>`;
  } else {
    deltaHtml = `<span class="delta-value delta-value--none">—</span>`;
  }

  const sportLabel = m.sport.charAt(0).toUpperCase() + m.sport.slice(1);

  return `
    <div class="${rowClass}">
      <div class="match-row-top">
        <span class="circle-tag">${m.circleName}</span>
        <span class="sport-pip">· ${sportLabel}</span>
        <span class="match-date-small">${formatDate(m.createdAt)}</span>
      </div>
      <div class="match-row-bottom">
        ${resultHtml}
        <span class="score-inline">${formatScore(m.sets)}</span>
        ${statusHtml}
      </div>
      <div class="match-row-delta">
        ${deltaHtml}
      </div>
    </div>`;
}

function render() {
  const list = document.getElementById('match-list');
  const loadWrap = document.getElementById('load-more-wrap');
  const totalLabel = document.getElementById('total-label');

  const items = filteredMatches();
  const slice = items.slice(0, visibleCount);

  totalLabel.textContent = `${items.length} partite`;

  if (items.length === 0) {
    list.innerHTML = `
      <div class="empty-state">
        <span class="empty-state-icon">🎾</span>
        Nessuna partita ancora
      </div>`;
    loadWrap.style.display = 'none';
    return;
  }

  list.innerHTML = slice.map(renderMatch).join('');
  loadWrap.style.display = visibleCount < items.length ? 'block' : 'none';
}

function setFilter(filter, btn) {
  currentFilter = filter;
  visibleCount = PAGE_SIZE;
  document.querySelectorAll('.filter-tab').forEach(t => t.classList.remove('active'));
  btn.classList.add('active');
  render();
}

function loadMore() {
  visibleCount += PAGE_SIZE;
  render();
}

// init
render();
