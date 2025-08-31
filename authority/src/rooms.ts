import { TempoState } from './types.js';

class RoomStore {
  private rooms: Map<string, TempoState> = new Map();

  getRoom(roomId: string): TempoState | undefined {
    return this.rooms.get(roomId);
  }

  getOrCreateRoom(roomId: string): TempoState {
    let room = this.rooms.get(roomId);
    if (!room) {
      room = this.createDefaultRoom(roomId);
      this.rooms.set(roomId, room);
    }
    return room;
  }

  updateRoom(roomId: string, updates: Partial<TempoState>): TempoState {
    const room = this.getOrCreateRoom(roomId);
    const updatedRoom: TempoState = {
      ...room,
      ...updates,
      roomId,
      updated_at: Date.now(),
    };
    this.rooms.set(roomId, updatedRoom);
    return updatedRoom;
  }

  private createDefaultRoom(roomId: string): TempoState {
    return {
      roomId,
      bpm: 174,
      bpi: 4,
      epoch_ms: Date.now(),
      updated_at: Date.now(),
    };
  }

  getAllRooms(): TempoState[] {
    return Array.from(this.rooms.values());
  }

  deleteRoom(roomId: string): boolean {
    return this.rooms.delete(roomId);
  }
}

export const roomStore = new RoomStore();