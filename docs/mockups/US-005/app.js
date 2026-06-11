// US-005 mockup interactions
let targetCardId = null;

function openConfirm(cardId) {
  targetCardId = cardId;
  document.getElementById('confirmModal').classList.remove('hidden');
}

function openDispute(cardId) {
  targetCardId = cardId;
  document.getElementById('disputeModal').classList.remove('hidden');
}

function closeModals() {
  document.getElementById('confirmModal').classList.add('hidden');
  document.getElementById('disputeModal').classList.add('hidden');
  targetCardId = null;
}

function doConfirm() {
  closeModals();
  if (!targetCardId) return;

  const card = document.getElementById(targetCardId);
  if (!targetCardId) return;

  // Update dots: fill the "you" dot (index 1, since index 0 is the creator)
  const dots = card.querySelectorAll('.confirm-dot');
  // Find first unfilled dot and mark as "you"
  for (const dot of dots) {
    if (!dot.classList.contains('confirm-dot--filled') && !dot.classList.contains('confirm-dot--you')) {
      dot.classList.add('confirm-dot--you');
      break;
    }
  }

  // Count filled + you dots
  const filled = card.querySelectorAll('.confirm-dot--filled, .confirm-dot--you').length;

  // Update label
  const label = card.querySelector('.confirm-label');

  if (filled >= 4) {
    // All confirmed → redirect to feedback page
    window.location.href = 'feedback.html';
    return;
  }

  // Update label to show confirmed-by-you state
  label.innerHTML = `
    <span class="confirmed-by-you">
      <svg width="10" height="10" viewBox="0 0 10 10" fill="none">
        <path d="M2 5l2.5 2.5 3.5-3.5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>
      Hai confermato
    </span>
    &nbsp;· ${filled} di 4`;

  // Hide action buttons
  const actions = card.querySelector('.match-actions');
  if (actions) {
    actions.innerHTML = `<div style="font-size:var(--font-size-xs);color:var(--color-text-placeholder);
      text-align:center;padding:var(--sp-2) 0;width:100%;">
      In attesa degli altri giocatori…
    </div>`;
  }
}

function doDispute() {
  closeModals();
  // Redirect to feedback / disputed state
  window.location.href = 'feedback.html#disputed';
}

// Handle hash on feedback page load
if (window.location.hash === '#disputed' && typeof showState === 'function') {
  showState('disputed');
}

function toggleEmpty() {
  const list = document.getElementById('matchList');
  const empty = document.getElementById('emptyState');
  if (!list || !empty) return;
  const isHidden = empty.style.display === 'none' || empty.style.display === '';
  list.style.display = isHidden ? 'none' : '';
  empty.style.display = isHidden ? '' : 'none';
  const btn = document.querySelector('[onclick="toggleEmpty()"]');
  if (btn) btn.textContent = isHidden ? '[Demo] Mostra partite' : '[Demo] Mostra stato vuoto';
}
