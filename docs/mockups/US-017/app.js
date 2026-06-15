/* US-017 — ELO Info & Simulator mockup interactions */

/* ── ELO formula replica (MOCKUP ONLY — produzione usa POST /simulate-match) ──
   Amplifier = 0.7; K dipende dal tipo di giocatore scelto dall'utente          */
let K = 32; // default: esperto
const AMPLIFIER = 0.7;

function computeDeltas(r1, r2, r3, r4, myScore, oppScore) {
  const team1Avg = (r1 + r2) / 2;
  const team2Avg = (r3 + r4) / 2;

  const totalPoints = myScore + oppScore;
  if (totalPoints === 0) return null;

  const myWon = myScore >= oppScore;
  const winnerAvg  = myWon ? team1Avg : team2Avg;
  const loserAvg   = myWon ? team2Avg : team1Avg;
  const winnerPts  = myWon ? myScore  : oppScore;

  const eWin = 1 / (1 + Math.pow(10, (loserAvg - winnerAvg) / 400));
  const scoreRatio = Math.min(Math.max(winnerPts / totalPoints, 0.5), 1.0);
  const effectiveResult = 0.5 + (scoreRatio - 0.5) * AMPLIFIER;
  const margin = effectiveResult - eWin;
  const delta = Math.round(K * margin);

  return myWon
    ? { me: +delta, partner: +delta, opp1: -delta, opp2: -delta }
    : { me: -delta, partner: -delta, opp1: +delta, opp2: +delta };
}

/* ── Player type toggle ── */
let playerType = 'esperto';

function setPlayerType(type) {
  playerType = type;
  K = type === 'esperto' ? 32 : 48;

  document.getElementById('btnEsperto').classList.toggle('active', type === 'esperto');
  document.getElementById('btnNuovo').classList.toggle('active', type === 'nuovo');

  const note = document.getElementById('playerTypeNote');
  if (type === 'esperto') {
    note.textContent = 'Il tuo rating è già stabile — i punti vinti e persi variano moderatamente.';
    note.className = 'player-type-note';
  } else {
    note.textContent = 'Stai ancora calibrando il tuo livello — il rating sale e scende più velocemente nelle prime partite.';
    note.className = 'player-type-note player-type-note--fast';
  }

  document.getElementById('resultPanel').style.display = 'none';
}

/* ── Mode switch ── */
let currentMode = 'unico';

function setMode(mode) {
  currentMode = mode;
  document.getElementById('modeUnico').style.display = mode === 'unico' ? '' : 'none';
  document.getElementById('modeSet').style.display   = mode === 'set'   ? '' : 'none';
  document.getElementById('btnUnico').classList.toggle('active', mode === 'unico');
  document.getElementById('btnSet').classList.toggle('active', mode === 'set');
  document.getElementById('resultPanel').style.display = 'none';
}

/* ── Set management ── */
function addSet() {
  const container = document.getElementById('setsContainer');
  const row = document.createElement('div');
  row.className = 'score-row';
  row.innerHTML = `
    <div class="score-row__inputs">
      <input type="number" class="score-input set-my" placeholder="0" min="0" value="">
      <span class="score-row__sep">–</span>
      <input type="number" class="score-input set-opp" placeholder="0" min="0" value="">
    </div>
    <button type="button" class="score-row__remove" onclick="removeSet(this)" aria-label="Rimuovi set">×</button>
  `;
  container.appendChild(row);
}

function removeSet(btn) {
  const rows = document.querySelectorAll('#setsContainer .score-row');
  if (rows.length <= 1) return; // keep at least 1
  btn.closest('.score-row').remove();
}

/* ── Aggregate scores from sets ── */
function getScoresFromSets() {
  const myInputs  = document.querySelectorAll('.set-my');
  const oppInputs = document.querySelectorAll('.set-opp');
  let myTotal = 0, oppTotal = 0;
  myInputs.forEach(i  => myTotal  += parseInt(i.value)  || 0);
  oppInputs.forEach(i => oppTotal += parseInt(i.value) || 0);
  return { myScore: myTotal, oppScore: oppTotal };
}

/* ── Form submit ── */
document.getElementById('simForm').addEventListener('submit', function(e) {
  e.preventDefault();

  const r1 = parseInt(document.getElementById('myRating').value)      || 1000;
  const r2 = parseInt(document.getElementById('partnerRating').value)  || 1000;
  const r3 = parseInt(document.getElementById('opp1Rating').value)     || 1000;
  const r4 = parseInt(document.getElementById('opp2Rating').value)     || 1000;

  let myScore, oppScore;
  if (currentMode === 'unico') {
    myScore  = parseInt(document.getElementById('myScore').value)  || 0;
    oppScore = parseInt(document.getElementById('oppScore').value) || 0;
  } else {
    ({ myScore, oppScore } = getScoresFromSets());
  }

  const result = computeDeltas(r1, r2, r3, r4, myScore, oppScore);
  if (!result || (myScore === 0 && oppScore === 0)) {
    alert('Inserisci almeno un punteggio maggiore di 0.');
    return;
  }

  const myWon = myScore >= oppScore;

  // Update result dividers to reflect who won
  const dividers = document.querySelectorAll('.result-row--divider');
  dividers[0].textContent = 'La mia squadra — ' + (myWon ? 'vittoria' : 'sconfitta');
  dividers[1].textContent = 'Avversari — ' + (myWon ? 'sconfitta' : 'vittoria');

  // Update ratings display
  document.getElementById('myRatingDisplay').textContent      = 'Rating: ' + r1;
  document.getElementById('partnerRatingDisplay').textContent = 'Rating: ' + r2;
  document.getElementById('opp1RatingDisplay').textContent    = 'Rating: ' + r3;
  document.getElementById('opp2RatingDisplay').textContent    = 'Rating: ' + r4;

  // Update deltas
  function setDelta(id, value) {
    const el = document.getElementById(id);
    const isPos = value >= 0;
    el.textContent = (isPos ? '+' : '') + value;
    el.className = 'result-row__delta result-row__delta--' + (isPos ? 'pos' : 'neg');
  }

  setDelta('deltaMe',      result.me);
  setDelta('deltaPartner', result.partner);
  setDelta('deltaOpp1',    result.opp1);
  setDelta('deltaOpp2',    result.opp2);

  // Show panel
  const panel = document.getElementById('resultPanel');
  panel.style.display = '';
  panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
});
