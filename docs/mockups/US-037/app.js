function setState(state, btn) {
  document.querySelectorAll('.demo-btn').forEach(b => b.classList.remove('active'));
  btn.classList.add('active');

  const statusBadge = document.getElementById('statusBadge');
  const decisionByPlayer = document.getElementById('decisionByPlayer');
  const decisionForced = document.getElementById('decisionForced');
  const stripPending = document.getElementById('stripPending');
  const stripDisputed = document.getElementById('stripDisputed');
  const deltaSection = document.getElementById('deltaSection');
  const noDelta = document.getElementById('noDelta');

  // reset
  decisionByPlayer.style.display = 'none';
  decisionForced.style.display = 'none';
  stripPending.style.display = 'none';
  stripDisputed.style.display = 'none';
  deltaSection.style.display = 'none';
  noDelta.style.display = 'none';

  if (state === 'confirmed-player') {
    statusBadge.textContent = 'Confermata';
    statusBadge.className = 'status-badge status-badge--confirmed';
    decisionByPlayer.style.display = 'flex';
    deltaSection.style.display = 'block';
  } else if (state === 'confirmed-forced') {
    statusBadge.textContent = 'Confermata';
    statusBadge.className = 'status-badge status-badge--confirmed';
    decisionForced.style.display = 'flex';
    deltaSection.style.display = 'block';
  } else if (state === 'pending') {
    statusBadge.textContent = 'In attesa';
    statusBadge.className = 'status-badge status-badge--pending';
    stripPending.style.display = 'flex';
    noDelta.style.display = 'block';
  } else if (state === 'disputed') {
    statusBadge.textContent = 'Contestata';
    statusBadge.className = 'status-badge status-badge--disputed';
    stripDisputed.style.display = 'flex';
    noDelta.style.display = 'block';
  }
}
