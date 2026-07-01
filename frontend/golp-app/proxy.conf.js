const bypass = (req) => {
  const accept = req.headers['accept'] || '';
  if (accept.includes('text/html')) return '/index.html';
};

module.exports = {
  '/auth':    { target: 'http://localhost:5120', secure: false, changeOrigin: true, logLevel: 'warn', bypass },
  '/circles': { target: 'http://localhost:5120', secure: false, changeOrigin: true, logLevel: 'warn', bypass },
  '/sports':  { target: 'http://localhost:5120', secure: false, changeOrigin: true, logLevel: 'warn', bypass },
  '/api':     { target: 'http://localhost:5120', secure: false, changeOrigin: true, logLevel: 'warn', bypass },
  '/m/':      { target: 'http://localhost:5120', secure: false, changeOrigin: true, logLevel: 'warn', bypass },
};
