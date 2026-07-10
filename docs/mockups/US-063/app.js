(function () {
  var root = document.documentElement;
  var saved = localStorage.getItem('golp-mockup-theme');
  if (saved) root.setAttribute('data-theme', saved);

  document.querySelectorAll('.theme-toggle').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var current = root.getAttribute('data-theme') ||
        (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
      var next = current === 'dark' ? 'light' : 'dark';
      root.setAttribute('data-theme', next);
      localStorage.setItem('golp-mockup-theme', next);
      btn.textContent = next === 'dark' ? '☾' : '☀';
    });
  });
})();
