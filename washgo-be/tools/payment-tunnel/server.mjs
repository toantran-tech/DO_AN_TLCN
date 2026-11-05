import http from 'node:http';

// Expose only the test landing/return pages and signed gateway notifications.
// Swagger, login, order data and all other backend routes remain private.
const page = '<!doctype html><html lang="vi"><meta charset="utf-8"><title>WashGo</title><body><h1>WashGo</h1><p>Môi trường thử nghiệm thanh toán.</p><p>Vui lòng kiểm tra trạng thái đơn hàng trong ứng dụng sau khi thanh toán.</p></body></html>';
const reply = (res, status, body = '') => {
  res.writeHead(status, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end(body);
};
const server = http.createServer((req, res) => {
  if (!req.url || req.url.length > 16384) return reply(res, 414);
  const path = new URL(req.url, 'http://localhost').pathname;
  if (path === '/' || path === '/payment/return') {
    return reply(res, req.method === 'GET' ? 200 : 405, req.method === 'GET' ? page : '');
  }
  const method = path === '/payment/vnpay-ipn' ? 'GET' : path === '/payment/momo-ipn' ? 'POST' : null;
  if (!method) return reply(res, 404);
  if (req.method !== method) return reply(res, 405);
  const chunks = [];
  let size = 0;
  let rejected = false;
  req.on('data', chunk => {
    size += chunk.length;
    if (size > 65536) {
      rejected = true;
      if (!res.writableEnded) reply(res, 413);
    } else chunks.push(chunk);
  });
  req.on('end', () => {
    if (rejected) return;
    const body = Buffer.concat(chunks);
    const upstream = http.request({
      hostname: 'washgo-be', port: 8080, path: req.url, method,
      headers: { 'Content-Type': req.headers['content-type'] || 'application/json', 'Content-Length': body.length },
      timeout: 15000
    }, response => {
      if (response.statusCode >= 500) {
        response.resume();
        return reply(res, 502, 'Gateway notification temporarily unavailable.');
      }
      res.writeHead(response.statusCode, {
        'Content-Type': response.headers['content-type'] || 'application/json', 'Cache-Control': 'no-store'
      });
      response.pipe(res);
    });
    upstream.on('timeout', () => upstream.destroy());
    upstream.on('error', () => {
      if (!res.headersSent) reply(res, 502, 'Gateway notification temporarily unavailable.');
      else res.destroy();
    });
    upstream.end(body);
  });
  req.on('error', () => { if (!res.writableEnded) res.destroy(); });
});
server.listen(8080, '0.0.0.0');
