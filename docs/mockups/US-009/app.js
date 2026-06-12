function setTab(btn, filter) {
  document.querySelectorAll('.tab-item').forEach(t => t.classList.remove('active'));
  btn.classList.add('active');

  const cards = document.querySelectorAll('.match-card');
  const empty = document.getElementById('emptyState');
  let visible = 0;

  cards.forEach(card => {
    const status = card.dataset.status;
    const show = filter === 'all' || status === filter;
    card.style.display = show ? 'flex' : 'none';
    if (show) visible++;
  });

  empty.style.display = visible === 0 ? 'flex' : 'none';
}
