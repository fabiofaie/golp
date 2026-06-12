/* eslint-disable no-undef */
// Service worker FCM — gestisce le push in background e il tap sulla notifica.
// I valori firebaseConfig sono pubblici: compilali da Firebase Console
// seguendo docs/firebase-setup.md (stessi valori di src/environments/environment.ts).

importScripts('https://www.gstatic.com/firebasejs/11.10.0/firebase-app-compat.js');
importScripts('https://www.gstatic.com/firebasejs/11.10.0/firebase-messaging-compat.js');

const firebaseConfig = {
  apiKey: '',
  authDomain: '',
  projectId: '',
  messagingSenderId: '',
  appId: '',
};

try {
  firebase.initializeApp(firebaseConfig);
  const messaging = firebase.messaging();

  // Messaggi data-only in background: mostra la notifica manualmente.
  // (I messaggi con payload "notification" vengono mostrati in automatico dall'SDK.)
  messaging.onBackgroundMessage((payload) => {
    if (payload.notification) {
      return; // già mostrata dall'SDK
    }
    const { matchId, circleId } = payload.data || {};
    self.registration.showNotification('Partita da confermare', {
      body: 'Una nuova partita ti aspetta: conferma il risultato!',
      data: { matchId, circleId },
    });
  });
} catch (e) {
  // Config mancante: il SW resta attivo solo per il notificationclick
}

self.addEventListener('notificationclick', (event) => {
  event.notification.close();

  // FCM incapsula il payload originale in data.FCM_MSG per le notifiche auto-mostrate
  const raw = event.notification.data || {};
  const data = raw.FCM_MSG ? (raw.FCM_MSG.data || {}) : raw;
  const url = data.circleId && data.matchId
    ? `/circles/${data.circleId}/matches/${data.matchId}`
    : '/';

  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      // Se l'app è già aperta, naviga e porta in foreground quella finestra
      const existing = windows.find((w) => 'focus' in w);
      if (existing) {
        existing.navigate(url);
        return existing.focus();
      }
      return clients.openWindow(url);
    })
  );
});
