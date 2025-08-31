export interface TempoState {
  roomId: string;
  bpm: number;
  bpi: number;
  epoch_ms: number;
  updated_by?: string;
  updated_at?: number;
}

export interface TempoProposal {
  roomId: string;
  bpm: number;
  proposed_by: string;
  client_ms: number;
}

export interface TimeSyncPing {
  t0_client: number;
}

export interface TimeSyncPong {
  t0_client: number;
  t1_server: number;
  t2_server: number;
}

export interface WebSocketMessage {
  type: 'tempo_state' | 'tempo_proposal' | 'time_sync_ping' | 'time_sync_pong' | 'subscribe';
  payload: any;
}