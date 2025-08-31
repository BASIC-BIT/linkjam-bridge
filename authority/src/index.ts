import Fastify from 'fastify';
import cors from '@fastify/cors';
import websocket from '@fastify/websocket';
import { registerApiRoutes } from './api.js';
import { registerWebSocketHandlers } from './ws.js';
import path from 'path';
import { fileURLToPath } from 'url';
import fs from 'fs/promises';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const server = Fastify({
  logger: {
    level: process.env.LOG_LEVEL || 'info',
    transport: {
      target: 'pino-pretty',
      options: {
        translateTime: 'HH:MM:ss Z',
        ignore: 'pid,hostname',
      },
    },
  },
});

async function start() {
  try {
    await server.register(cors, {
      origin: true,
      credentials: true,
    });
    
    await server.register(websocket, {
      options: {
        maxPayload: 1048576,
      },
    });
    
    await registerApiRoutes(server);
    await registerWebSocketHandlers(server);
    
    server.get('/', async (_request, reply) => {
      const htmlPath = path.join(__dirname, 'public', 'index.html');
      const html = await fs.readFile(htmlPath, 'utf-8');
      return reply.type('text/html').send(html);
    });
    
    server.get('/app.js', async (_request, reply) => {
      const jsPath = path.join(__dirname, 'public', 'app.js');
      const js = await fs.readFile(jsPath, 'utf-8');
      return reply.type('application/javascript').send(js);
    });
    
    const port = parseInt(process.env.PORT || '3000', 10);
    const host = process.env.HOST || '0.0.0.0';
    
    await server.listen({ port, host });
    
    console.log(`\n🎵 LinkJam Authority Server`);
    console.log(`   Running at: http://${host === '0.0.0.0' ? 'localhost' : host}:${port}`);
    console.log(`   WebSocket:  ws://${host === '0.0.0.0' ? 'localhost' : host}:${port}/ws/:roomId`);
    console.log(`   REST API:   http://${host === '0.0.0.0' ? 'localhost' : host}:${port}/room/:roomId/state\n`);
    
  } catch (err) {
    server.log.error(err);
    process.exit(1);
  }
}

process.on('SIGINT', async () => {
  console.log('\nShutting down gracefully...');
  await server.close();
  process.exit(0);
});

process.on('SIGTERM', async () => {
  await server.close();
  process.exit(0);
});

start();