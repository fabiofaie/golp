function selectMethod(method) {
  document.querySelectorAll('.method-card').forEach(function (card) {
    card.classList.toggle('selected', card.dataset.method === method);
  });

  var windowParams = document.getElementById('windowParams');
  windowParams.classList.toggle('hidden', method !== 'GameBonus');

  var badge = document.getElementById('lbBadge');
  badge.textContent = method === 'GameBonus' ? 'Game+Bonus' : 'ELO';

  var pointsSuffix = method === 'GameBonus' ? ' pt (Game+Bonus)' : ' pt';
  document.querySelectorAll('.lb-row .points').forEach(function (el) {
    var value = el.textContent.match(/\d+/)[0];
    el.textContent = value + pointsSuffix;
  });
}

function save() {
  var status = document.getElementById('saveStatus');
  status.classList.add('visible');
  setTimeout(function () { status.classList.remove('visible'); }, 1800);
}
