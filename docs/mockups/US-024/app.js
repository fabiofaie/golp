const PLATFORM_CONTENT = {
  'ios-safari': {
    badge: '📱 Rilevato: iOS · Safari',
    steps: [
      'Tocca l\'icona <strong>Condividi</strong> in basso nella barra di Safari.',
      'Scorri il menu e tocca <strong>Aggiungi a schermata Home</strong>.',
      'Confirma il nome "Golp" e tocca <strong>Aggiungi</strong> in alto a destra.'
    ],
    native: null
  },
  'android-chrome': {
    badge: '📱 Rilevato: Android · Chrome',
    steps: [
      'Tocca i <strong>tre puntini</strong> in alto a destra nella barra di Chrome.',
      'Tocca <strong>Installa app</strong> (o "Aggiungi a schermata Home").',
      'Confirma toccando <strong>Installa</strong> nel popup.'
    ],
    native: 'Il tuo browser supporta l\'installazione diretta — niente bisogno di seguire i passaggi manuali.'
  },
  'android-samsung': {
    badge: '📱 Rilevato: Android · Samsung Internet',
    steps: [
      'Tocca l\'icona <strong>menu</strong> (≡) in basso nella barra di Samsung Internet.',
      'Tocca <strong>Aggiungi pagina a</strong> → <strong>Schermata Home</strong>.',
      'Confirma toccando <strong>Aggiungi</strong>.'
    ],
    native: null
  },
  'fallback': {
    badge: '📱 Browser non riconosciuto con certezza',
    steps: [
      'Cerca nel menu del tuo browser un\'opzione simile a <strong>"Aggiungi a schermata Home"</strong> o <strong>"Installa app"</strong>.',
      'Di solito si trova nel menu principale (≡ o ⋮) o nell\'icona di condivisione.',
      'Se non trovi l\'opzione, puoi continuare a usare Golp dal browser senza problemi.'
    ],
    native: null
  }
};

function render(platform) {
  const data = PLATFORM_CONTENT[platform];
  document.getElementById('badge').textContent = data.badge;

  document.querySelectorAll('.tab').forEach(t => {
    t.classList.toggle('active', t.dataset.platform === platform);
  });

  const stepsHtml = data.steps.map((text, i) => `
    <div class="step">
      <div class="step__num">${i + 1}</div>
      <div class="step__text">${text}</div>
    </div>
  `).join('');

  const nativeHtml = data.native ? `
    <div class="install-guide__native">
      <p>${data.native}</p>
      <button class="btn btn-primary">Installa ora</button>
    </div>
  ` : '';

  document.getElementById('body').innerHTML = stepsHtml + nativeHtml;
}

document.getElementById('tabs').addEventListener('click', (e) => {
  const btn = e.target.closest('.tab');
  if (!btn) return;
  render(btn.dataset.platform);
});

const initialPlatform = new URLSearchParams(window.location.search).get('platform') || 'android-chrome';
render(initialPlatform);
