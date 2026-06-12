/* Dashboard mockup — demo state toggle */

let hasPending = true;

const circleData = [
  {
    name: 'Padel Club Roma',
    sport: 'Padel',
    sportClass: 'padel',
    rating: '1 142',
    delta: '▲ +18 ultima partita',
    deltaClass: 'pos',
    rank: '#3',
    total: 'di 14',
    played: 23,
    wins: 15,
    losses: 8,
  },
  {
    name: 'Beach Roma Sud',
    sport: 'Beach',
    sportClass: 'beachtennis',
    rating: '1 005',
    delta: '▼ −6 ultima partita',
    deltaClass: 'neg',
    rank: '#7',
    total: 'di 9',
    played: 4,
    wins: 2,
    losses: 2,
  },
];

let currentCircleIdx = 0;

function toggleCircle(btn) {
  currentCircleIdx = (currentCircleIdx + 1) % circleData.length;
  const c = circleData[currentCircleIdx];

  document.getElementById('circle-name').textContent = c.name;
  btn.querySelector('.sport-badge').textContent = c.sport;
  btn.querySelector('.sport-badge').className = `sport-badge sport-badge--${c.sportClass}`;

  document.getElementById('hero-rating').textContent = c.rating;

  const delta = document.getElementById('hero-delta');
  delta.textContent = c.delta;
  delta.className = `hero-rating-delta hero-rating-delta--${c.deltaClass}`;

  document.getElementById('hero-rank').textContent = c.rank;
  document.getElementById('hero-total').textContent = c.total;
  document.getElementById('hero-played').textContent = c.played;
  document.getElementById('hero-wins').textContent = c.wins;
  document.getElementById('hero-losses').textContent = c.losses;
}

function toggleDemoState() {
  hasPending = !hasPending;

  document.getElementById('section-confirm').style.display = hasPending ? '' : 'none';
  document.getElementById('section-confirm-empty').style.display = hasPending ? 'none' : '';

  const btn = document.querySelector('.demo-toggle');
  btn.textContent = hasPending
    ? '↔ Stato: con conferme in attesa'
    : '↔ Stato: nessuna conferma';
}
