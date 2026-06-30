/* US-039 mockup interactions */

const SCENARIOS = {
  empty: {
    note: 'Scenario: form iniziale — tutti gli slot sono in modalità Membro. Contact Picker non disponibile (desktop).',
    slots:  { t1p2: 'member', t2p1: 'member', t2p2: 'member' },
    picker: false,
    values: {}
  },
  'no-picker': {
    note: 'Scenario: slot 2 su Ospite, nessun Contact Picker (desktop o browser non supportato). Il cliente compila manualmente.',
    slots:  { t1p2: 'guest', t2p1: 'member', t2p2: 'member' },
    picker: false,
    values: {}
  },
  'with-picker': {
    note: 'Scenario: Contact Picker API disponibile (mobile Android/iOS). Pulsante "Scegli dai contatti" in cima — prima opzione.',
    slots:  { t1p2: 'guest', t2p1: 'guest', t2p2: 'member' },
    picker: true,
    values: {}
  },
  'picker-used': {
    note: 'Scenario: l\'utente ha toccato "Scegli dai contatti" — i campi nome e telefono sono già precompilati dai contatti del telefono.',
    slots:  { t1p2: 'guest', t2p1: 'guest', t2p2: 'member' },
    picker: true,
    values: {
      't1p2-name':  'Marco Esposito',
      't1p2-phone': '+39 340 123 4567',
      't2p1-name':  'Giulia Neri',
      't2p1-phone': '+39 328 456 7890'
    }
  },
  mixed: {
    note: 'Scenario misto: slot 2 compilato via Contact Picker (phone), slot 3 compilato manualmente (email), slot 4 membro.',
    slots:  { t1p2: 'guest', t2p1: 'guest', t2p2: 'member' },
    picker: true,
    values: {
      't1p2-name':  'Marco Esposito',
      't1p2-phone': '+39 340 123 4567',
      't2p1-name':  'Giulia Neri',
      't2p1-email': 'giulia.neri@gmail.com'
    }
  }
};

function setSlotMode(slotId, mode) {
  const memberContent = document.getElementById(`${slotId}-member-content`);
  const guestContent  = document.getElementById(`${slotId}-guest-content`);
  const memberBtn     = document.getElementById(`${slotId}-member-btn`);
  const guestBtn      = document.getElementById(`${slotId}-guest-btn`);
  const card          = document.getElementById(`slot-${slotId}`);

  if (mode === 'guest') {
    memberContent.style.display = 'none';
    guestContent.style.display  = 'flex';
    memberBtn.classList.remove('active');
    guestBtn.classList.add('active', 'guest-active');
    card.classList.add('is-guest');
    card.classList.remove('is-member');
  } else {
    memberContent.style.display = 'block';
    guestContent.style.display  = 'none';
    memberBtn.classList.add('active');
    guestBtn.classList.remove('active', 'guest-active');
    card.classList.remove('is-guest');
    card.classList.add('is-member');
  }
}

function setScenario(key) {
  const s = SCENARIOS[key];
  if (!s) return;

  document.getElementById('scenario-note').textContent = s.note;

  ['t1p2', 't2p1', 't2p2'].forEach(slot => {
    setSlotMode(slot, s.slots[slot] || 'member');
  });

  // Contact Picker: show/hide the picker-wrap (now at TOP of guest fields)
  ['t1p2', 't2p1', 't2p2'].forEach(slot => {
    const wrap = document.getElementById(`${slot}-picker-wrap`);
    if (wrap) wrap.style.display = s.picker ? 'block' : 'none';
  });

  // Fill / clear values
  const allFields = ['t1p2-name','t1p2-email','t1p2-phone','t2p1-name','t2p1-email','t2p1-phone','t2p2-name','t2p2-email','t2p2-phone'];
  allFields.forEach(id => {
    const el = document.getElementById(id);
    if (!el) return;
    const val = s.values[id] || '';
    el.value = val;
    el.classList.toggle('has-value', !!val);
  });

  // Update scenario buttons
  document.querySelectorAll('.scenario-btn').forEach(btn => {
    btn.classList.toggle('active', btn.getAttribute('onclick') === `setScenario('${key}')`);
  });
}

function simulateContactPicker(slotId) {
  const mockContacts = [
    { name: 'Marco Esposito',  tel: '+39 340 123 4567' },
    { name: 'Giulia Neri',     tel: '+39 328 456 7890' },
    { name: 'Roberto Mancini', tel: '+39 347 999 0011' }
  ];
  const picked = mockContacts[Math.floor(Math.random() * mockContacts.length)];

  const nameEl  = document.getElementById(`${slotId}-name`);
  const phoneEl = document.getElementById(`${slotId}-phone`);
  if (nameEl)  { nameEl.value  = picked.name; nameEl.classList.add('has-value'); }
  if (phoneEl) { phoneEl.value = picked.tel;  phoneEl.classList.add('has-value'); }

  // Flash feedback on the button
  const btn = document.querySelector(`#${slotId}-picker-wrap .contact-picker-primary`);
  if (btn) {
    const originalHTML = btn.innerHTML;
    btn.innerHTML = `<svg width="18" height="18" viewBox="0 0 18 18" fill="none">
      <path d="M4 9l4 4 6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
    </svg> ${picked.name} aggiunto`;
    btn.style.background = 'rgba(34,197,94,0.12)';
    btn.style.borderColor = 'rgba(34,197,94,0.30)';
    btn.style.color = '#22C55E';
    setTimeout(() => {
      btn.innerHTML = originalHTML;
      btn.style.background = '';
      btn.style.borderColor = '';
      btn.style.color = '';
    }, 2000);
  }
}

document.addEventListener('DOMContentLoaded', () => setScenario('empty'));
