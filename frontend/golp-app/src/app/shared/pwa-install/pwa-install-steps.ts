import { PwaBrowser, PwaOs } from './pwa-platform.service';

export interface InstallStep {
  text: string;
}

export interface InstallGuideContent {
  badgeLabel: string;
  steps: InstallStep[];
  hasNativePrompt: boolean;
}

const FALLBACK: InstallGuideContent = {
  badgeLabel: 'Browser non riconosciuto con certezza',
  steps: [
    { text: 'Cerca nel menu del tuo browser un\'opzione simile a "Aggiungi a schermata Home" o "Installa app".' },
    { text: 'Di solito si trova nel menu principale (≡ o ⋮) o nell\'icona di condivisione.' },
    { text: 'Se non trovi l\'opzione, puoi continuare a usare Golp dal browser senza problemi.' }
  ],
  hasNativePrompt: false
};

const GUIDE_MAP: Record<PwaOs, Partial<Record<PwaBrowser, InstallGuideContent>>> = {
  ios: {
    safari: {
      badgeLabel: 'iOS · Safari',
      steps: [
        { text: 'Tocca l\'icona Condividi in basso nella barra di Safari.' },
        { text: 'Scorri il menu e tocca "Aggiungi a schermata Home".' },
        { text: 'Confirma il nome "Golp" e tocca Aggiungi in alto a destra.' }
      ],
      hasNativePrompt: false
    }
  },
  android: {
    chrome: {
      badgeLabel: 'Android · Chrome',
      steps: [
        { text: 'Tocca i tre puntini in alto a destra nella barra di Chrome.' },
        { text: 'Tocca "Installa app" (o "Aggiungi a schermata Home").' },
        { text: 'Confirma toccando Installa nel popup.' }
      ],
      hasNativePrompt: true
    },
    samsung: {
      badgeLabel: 'Android · Samsung Internet',
      steps: [
        { text: 'Tocca l\'icona menu (≡) in basso nella barra di Samsung Internet.' },
        { text: 'Tocca "Aggiungi pagina a" → "Schermata Home".' },
        { text: 'Confirma toccando Aggiungi.' }
      ],
      hasNativePrompt: false
    },
    firefox: {
      badgeLabel: 'Android · Firefox',
      steps: [
        { text: 'Tocca i tre puntini in alto a destra nella barra di Firefox.' },
        { text: 'Tocca "Installa" (o "Aggiungi a schermata Home").' },
        { text: 'Confirma toccando Aggiungi.' }
      ],
      hasNativePrompt: false
    }
  },
  other: {}
};

export function getInstallGuide(os: PwaOs, browser: PwaBrowser): InstallGuideContent {
  return GUIDE_MAP[os]?.[browser] ?? FALLBACK;
}
