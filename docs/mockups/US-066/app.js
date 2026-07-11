function toggleSheet() {
  const overlay = document.getElementById('overlay');
  const sheet = document.getElementById('sheet');
  const btn = document.getElementById('picker-btn');
  overlay.classList.toggle('visible');
  sheet.classList.toggle('visible');
  if (btn) btn.classList.toggle('open');
}

function selectRow(el) {
  document.querySelectorAll('.circle-row').forEach(r => {
    r.classList.remove('selected');
    const badge = r.querySelector('.check-badge');
    if (badge) badge.remove();
  });
  el.classList.add('selected');
  const check = document.createElement('span');
  check.className = 'check-badge';
  check.textContent = '✓';
  el.appendChild(check);
}

function toggleFav(evt, star) {
  evt.stopPropagation();
  star.classList.toggle('active');
}

function filterRows(query) {
  const q = query.trim().toLowerCase();
  document.querySelectorAll('.circle-row').forEach(row => {
    const name = row.dataset.name || '';
    row.style.display = name.includes(q) ? '' : 'none';
  });
  document.querySelectorAll('.sheet-group-label').forEach(label => {
    const group = label.dataset.group;
    const anyVisible = [...document.querySelectorAll(`.circle-row[data-group="${group}"]`)]
      .some(r => r.style.display !== 'none');
    label.style.display = anyVisible ? '' : 'none';
  });
}
