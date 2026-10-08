/**
 * 本地模拟线上 nginx 的子路径部署行为，用来在部署前验证 /personlib/ 前缀改造。
 *
 * 与线上 nginx 的对应关系：
 *   - /personlib      -> 301 到 /personlib/（否则前端产物里的相对资源会以根目录为基准而 404）
 *   - /personlib/*    -> 剥掉前缀转发给后端（等价于 proxy_pass .../ 的尾斜杠行为）
 *   - 其它路径        -> 404（线上这里归创意工坊，本地不代理）
 */
import http from 'node:http';

const BACKEND_HOST = '127.0.0.1';
const BACKEND_PORT = 3001;
const LISTEN_PORT = 8090;
const PREFIX = '/personlib';

const server = http.createServer((req, res) => {
  if (req.url === PREFIX) {
    res.writeHead(301, { Location: PREFIX + '/' });
    res.end();
    return;
  }
  if (!req.url.startsWith(PREFIX + '/')) {
    res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
    res.end('only ' + PREFIX + '/ is proxied here');
    return;
  }

  const upstreamPath = req.url.slice(PREFIX.length);
  const upstream = http.request(
    {
      host: BACKEND_HOST,
      port: BACKEND_PORT,
      method: req.method,
      path: upstreamPath,
      headers: { ...req.headers, host: BACKEND_HOST + ':' + BACKEND_PORT },
    },
    (up) => {
      res.writeHead(up.statusCode, up.headers);
      up.pipe(res);
    },
  );

  upstream.on('error', (err) => {
    res.writeHead(502, { 'Content-Type': 'text/plain; charset=utf-8' });
    res.end('proxy error: ' + err.message);
  });

  req.pipe(upstream);
});

server.listen(LISTEN_PORT, () => {
  console.log('personlib proxy listening on http://localhost:' + LISTEN_PORT + PREFIX + '/');
});
