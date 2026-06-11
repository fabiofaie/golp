/**
 * Proxy config per ng serve.
 * Usa funzione context per evitare che le rotte Angular /circles/*
 * vengano intercettate dal proxy e inoltrate al backend.
 * Le navigazioni browser inviano Accept: text/html → non proxied → Angular SPA.
 * Le chiamate HttpClient inviano Accept: application/json → proxied → backend.
 */
module.exports = [
  {
    context: ['/auth', '/sports'],
    target: 'http://localhost:5120',
    secure: false,
    changeOrigin: true,
    logLevel: 'warn',
  },
  {
    context: (pathname, req) => {
      if (!pathname.startsWith('/circles')) return false;
      const accept = req.headers['accept'] || '';
      // Angular HttpClient sends application/json; browser navigation sends text/html
      return !accept.includes('text/html');
    },
    target: 'http://localhost:5120',
    secure: false,
    changeOrigin: true,
    logLevel: 'warn',
  },
];
