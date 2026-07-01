// US-041 Quick Match — interactivity

var selectedCount = 2; // Mario (tu) + Anna già selezionati

function selectPlayer(chipEl, initials, name, rating, isGuest) {
  if (chipEl.classList.contains('used')) return;

  // Trova il primo slot vuoto in squadra B
  var emptySlots = document.querySelectorAll('.player-slot.empty');
  if (!emptySlots.length) return;

  var slot = emptySlots[0];
  slot.classList.remove('empty');
  slot.classList.add(isGuest ? 'ghost-filled' : 'team2-filled');

  var avatarEl = slot.querySelector('.slot-avatar-placeholder');
  var labelEl  = slot.querySelector('.slot-empty-label');

  // Sostituisce placeholder con avatar reale
  var avatar = document.createElement('div');
  avatar.className = 'slot-avatar';
  avatar.textContent = initials;
  slot.replaceChild(avatar, avatarEl);

  var info = document.createElement('div');
  info.className = 'slot-info';
  info.innerHTML =
    '<div class="slot-name">' + name + '</div>' +
    '<div class="slot-meta">' + (isGuest ? 'Ospite' : 'Rating ' + rating) + '</div>';

  slot.replaceChild(info, labelEl);

  // Aggiunge badge ospite o remove btn
  if (isGuest) {
    var badge = document.createElement('span');
    badge.className = 'slot-guest-badge';
    badge.textContent = 'ospite';
    slot.appendChild(badge);
  } else {
    var removeBtn = document.createElement('button');
    removeBtn.className = 'slot-remove';
    removeBtn.textContent = '×';
    removeBtn.onclick = function() { removeSlot(slot, chipEl); };
    slot.appendChild(removeBtn);
  }

  // Marca chip come usato
  chipEl.classList.add('used');

  selectedCount++;
  checkReady();
}

function removeSlot(slot, chipEl) {
  // Ripristina slot vuoto
  slot.classList.remove('team2-filled', 'ghost-filled', 'team1-filled');
  slot.classList.add('empty');
  slot.innerHTML =
    '<div class="slot-avatar-placeholder">+</div>' +
    '<span class="slot-empty-label">Aggiungi avversario</span>';

  if (chipEl) chipEl.classList.remove('used');
  selectedCount--;
  checkReady();
}

function checkReady() {
  // 4 giocatori = Mario (tu) + Anna + 2 slot B → selectedCount arriva a 4
  var btn = document.getElementById('btn-avanti');
  if (!btn) return;
  if (selectedCount >= 4) {
    btn.style.opacity = '1';
    btn.style.pointerEvents = 'auto';
  } else {
    btn.style.opacity = '0.4';
    btn.style.pointerEvents = 'none';
  }
}
