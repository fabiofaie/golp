// Service worker FCM — gestisce le push in background e il tap sulla notifica.
// Niente SDK Firebase qui: getToken() lato app si occupa della subscription,
// questo SW legge l'evento push raw e mostra la notifica direttamente
// (la SDK compat non invoca onBackgroundMessage per payload con campo "notification").
self.addEventListener('push', (event) => {
  if (!event.data) {
    return;
  }
  let payload;
  try {
    payload = event.data.json();
  } catch (e) {
    return;
  }
  const { matchId, circleId } = payload.data || {};
  const title = payload.notification?.title ?? 'Partita da confermare';
  const body = payload.notification?.body ?? 'Una nuova partita ti aspetta: conferma il risultato!';
  event.waitUntil(
    self.registration.showNotification(title, {
      body,
      data: { matchId, circleId },
    })
  );
});

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
