import { TempoState } from './types.js';

export function calculateBeatDuration(bpm: number): number {
  return 60000 / bpm;
}

export function calculateIntervalDuration(bpm: number, bpi: number): number {
  const beatMs = calculateBeatDuration(bpm);
  return bpi * beatMs;
}

export function calculateNextBoundary(state: TempoState, nowMs?: number): number {
  const now = nowMs || Date.now();
  const intervalMs = calculateIntervalDuration(state.bpm, state.bpi);
  const phase = (now - state.epoch_ms) % intervalMs;
  return now + (intervalMs - phase);
}

export function calculatePhase(state: TempoState, nowMs?: number): number {
  const now = nowMs || Date.now();
  const intervalMs = calculateIntervalDuration(state.bpm, state.bpi);
  return (now - state.epoch_ms) % intervalMs;
}

export function calculateBarBeat(state: TempoState, nowMs?: number): { bar: number; beat: number } {
  const now = nowMs || Date.now();
  const beatMs = calculateBeatDuration(state.bpm);
  const totalBeats = Math.floor((now - state.epoch_ms) / beatMs);
  const bar = Math.floor(totalBeats / state.bpi);
  const beat = totalBeats % state.bpi;
  return { bar, beat };
}

export function msUntilNextBoundary(state: TempoState, nowMs?: number): number {
  const now = nowMs || Date.now();
  return calculateNextBoundary(state, now) - now;
}