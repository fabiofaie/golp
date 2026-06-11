// ─── Sport toggle: sets vs flat score ───────────────────────────────────────

function showSets(useSets, btn) {
  document.getElementById('score-sets').style.display = useSets ? '' : 'none';
  document.getElementById('score-flat').style.display = useSets ? 'none' : '';

  // Update toggle buttons
  document.querySelectorAll('.sport-toggle-btn').forEach(b => b.classList.remove('active'));
  btn.classList.add('active');
}

// ─── Dynamic set rows ────────────────────────────────────────────────────────

let setCount = document.querySelectorAll('#sets-list .score-row').length;

function addSet() {
  setCount++;
  const list = document.getElementById('sets-list');
  const row = document.createElement('div');
  row.className = 'score-row';
  row.innerHTML = `
    <span class="score-set-label">Set ${setCount}</span>
    <div class="score-inputs">
      <div class="score-input-wrap">
        <input type="number" class="score-input score-input--team1" placeholder="0" min="0" max="99" autofocus>
      </div>
      <span class="score-dash">—</span>
      <div class="score-input-wrap">
        <input type="number" class="score-input score-input--team2" placeholder="0" min="0" max="99">
      </div>
    </div>
    <button class="score-remove-btn" onclick="removeSet(this)" title="Rimuovi set">×</button>
  `;
  list.appendChild(row);
  // Focus first input in new row
  row.querySelector('input').focus();
  renumberSets();
}

function removeSet(btn) {
  const list = document.getElementById('sets-list');
  if (list.querySelectorAll('.score-row').length <= 1) return; // keep at least 1
  btn.closest('.score-row').remove();
  renumberSets();
}

function renumberSets() {
  document.querySelectorAll('#sets-list .score-row').forEach((row, i) => {
    const label = row.querySelector('.score-set-label');
    if (label) label.textContent = `Set ${i + 1}`;
  });
  setCount = document.querySelectorAll('#sets-list .score-row').length;
}
