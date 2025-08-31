import { FastifyInstance } from 'fastify';
import { roomStore } from './rooms.js';
import { TempoState } from './types.js';
import { calculateNextBoundary, msUntilNextBoundary, calculateBarBeat } from './boundary.js';

export async function registerApiRoutes(server: FastifyInstance) {
  server.get('/room/:roomId/state', async (request, reply) => {
    const { roomId } = request.params as { roomId: string };
    const room = roomStore.getOrCreateRoom(roomId);
    
    const enrichedState = {
      ...room,
      next_boundary_ms: calculateNextBoundary(room),
      ms_until_boundary: msUntilNextBoundary(room),
      current_bar_beat: calculateBarBeat(room),
    };
    
    return reply.send(enrichedState);
  });

  server.post('/room/:roomId/state', async (request, reply) => {
    const { roomId } = request.params as { roomId: string };
    const updates = request.body as Partial<TempoState>;
    
    if (updates.bpm !== undefined) {
      if (updates.bpm < 20 || updates.bpm > 999) {
        return reply.status(400).send({ error: 'BPM must be between 20 and 999' });
      }
    }
    
    if (updates.bpi !== undefined) {
      if (updates.bpi < 1 || updates.bpi > 64) {
        return reply.status(400).send({ error: 'BPI must be between 1 and 64' });
      }
    }
    
    const updatedRoom = roomStore.updateRoom(roomId, updates);
    
    server.websocketServer?.clients.forEach((client) => {
      if (client.readyState === 1) {
        client.send(JSON.stringify({
          type: 'tempo_state',
          payload: updatedRoom,
        }));
      }
    });
    
    return reply.send(updatedRoom);
  });

  server.get('/rooms', async (_request, reply) => {
    const rooms = roomStore.getAllRooms();
    return reply.send(rooms);
  });

  server.get('/time', async (_request, reply) => {
    return reply.send({ server_time_ms: Date.now() });
  });

  server.get('/health', async (_request, reply) => {
    return reply.send({ status: 'healthy', uptime: process.uptime() });
  });
}