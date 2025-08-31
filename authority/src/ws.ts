import { FastifyInstance } from 'fastify';
import { WebSocket } from 'ws';
import { roomStore } from './rooms.js';
import { TempoProposal, TimeSyncPing, TimeSyncPong, WebSocketMessage } from './types.js';

interface ExtendedWebSocket extends WebSocket {
  roomId?: string;
  clientId?: string;
  isAlive?: boolean;
}

const clients = new Map<string, Set<ExtendedWebSocket>>();

export async function registerWebSocketHandlers(server: FastifyInstance) {
  server.get('/ws/:roomId', { websocket: true }, (socket, req) => {
    // The first parameter is the WebSocket itself in @fastify/websocket
    const ws = socket as ExtendedWebSocket;
    const { roomId } = req.params as { roomId: string };
    
    // Store properties on the WebSocket object
    ws.roomId = roomId;
    ws.clientId = `client_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    ws.isAlive = true;
    
    if (!clients.has(roomId)) {
      clients.set(roomId, new Set());
    }
    clients.get(roomId)!.add(ws);
    
    console.log(`Client ${ws.clientId} connected to room ${roomId}`);
    
    const room = roomStore.getOrCreateRoom(roomId);
    ws.send(JSON.stringify({
      type: 'tempo_state',
      payload: room,
    }));
    
    ws.on('message', (data) => {
      try {
        const message: WebSocketMessage = JSON.parse(data.toString());
        handleMessage(ws, message, server);
      } catch (error) {
        console.error('Failed to parse message:', error);
        ws.send(JSON.stringify({
          type: 'error',
          payload: { message: 'Invalid message format' },
        }));
      }
    });
    
    ws.on('pong', () => {
      ws.isAlive = true;
    });
    
    ws.on('close', () => {
      console.log(`Client ${ws.clientId} disconnected from room ${roomId}`);
      const roomClients = clients.get(roomId);
      if (roomClients) {
        roomClients.delete(ws);
        if (roomClients.size === 0) {
          clients.delete(roomId);
        }
      }
    });
    
    ws.on('error', (error) => {
      console.error(`WebSocket error for client ${ws.clientId}:`, error);
    });
  });
  
  const heartbeatInterval = setInterval(() => {
    clients.forEach((roomClients) => {
      roomClients.forEach((socket) => {
        if (socket.isAlive === false) {
          socket.terminate();
          return;
        }
        socket.isAlive = false;
        socket.ping();
      });
    });
  }, 30000);
  
  server.addHook('onClose', () => {
    clearInterval(heartbeatInterval);
  });
}

function handleMessage(socket: ExtendedWebSocket, message: WebSocketMessage, server: FastifyInstance) {
  switch (message.type) {
    case 'tempo_proposal':
      handleTempoProposal(socket, message.payload as TempoProposal, server);
      break;
      
    case 'time_sync_ping':
      handleTimeSyncPing(socket, message.payload as TimeSyncPing);
      break;
      
    case 'subscribe':
      break;
      
    default:
      socket.send(JSON.stringify({
        type: 'error',
        payload: { message: `Unknown message type: ${message.type}` },
      }));
  }
}

function handleTempoProposal(socket: ExtendedWebSocket, proposal: TempoProposal, _server: FastifyInstance) {
  const { roomId, bpm, proposed_by } = proposal;
  
  if (bpm < 20 || bpm > 999) {
    socket.send(JSON.stringify({
      type: 'error',
      payload: { message: 'BPM must be between 20 and 999' },
    }));
    return;
  }
  
  const updatedRoom = roomStore.updateRoom(roomId, {
    bpm,
    updated_by: proposed_by,
  });
  
  console.log(`Room ${roomId} BPM updated to ${bpm} by ${proposed_by}`);
  
  broadcastToRoom(roomId, {
    type: 'tempo_state',
    payload: updatedRoom,
  });
}

function handleTimeSyncPing(socket: ExtendedWebSocket, ping: TimeSyncPing) {
  const t1_server = Date.now();
  
  const pong: TimeSyncPong = {
    t0_client: ping.t0_client,
    t1_server,
    t2_server: Date.now(),
  };
  
  socket.send(JSON.stringify({
    type: 'time_sync_pong',
    payload: pong,
  }));
}

function broadcastToRoom(roomId: string, message: any) {
  const roomClients = clients.get(roomId);
  if (roomClients) {
    const messageStr = JSON.stringify(message);
    roomClients.forEach((client) => {
      if (client.readyState === WebSocket.OPEN) {
        client.send(messageStr);
      }
    });
  }
}

export function broadcastTempoStateToAll(_server: FastifyInstance) {
  clients.forEach((_roomClients, roomId) => {
    const room = roomStore.getRoom(roomId);
    if (room) {
      broadcastToRoom(roomId, {
        type: 'tempo_state',
        payload: room,
      });
    }
  });
}