# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LinkJam Bridge enables synchronized remote DJ collaboration by:
- Using NINJAM for bar-aligned audio buffering (BPI intervals)
- Synchronizing tempo via Ableton Link across remote DJs
- Streaming to VRChat via OBS → VRCDN

## Architecture

The system consists of two main components:

### Authority (Node.js/TypeScript server)
- Maintains tempo state: `{roomId, bpm, bpi, epoch_ms}`
- REST API: `GET/POST /room/:id/state`
- WebSocket: broadcasts `tempo_state`, receives `tempo_proposal`
- Calculates interval boundaries for synchronized tempo changes
- Location: `/authority`

### Companion (Windows C# .NET 8 tray app)
- Bundles and manages Carabiner.exe (Ableton Link daemon)
- Connects to Authority via WebSocket with time-sync
- Applies tempo changes at musical boundaries
- Monitors local Link BPM changes and proposes them upstream
- Location: `/companion`

## Development Setup

### Authority Server
```bash
cd authority
npm install
npm run dev     # Development with hot reload
npm run build   # Build for production
npm start       # Run production build
npm test        # Run tests
```

### Companion App
```bash
cd companion
dotnet restore
dotnet build
dotnet run --project LinkJam.Companion
# For release build:
dotnet publish -c Release -r win-x64 --self-contained
```

## Key Implementation Details

### Boundary Synchronization
- Beat duration: `beat_ms = 60000 / bpm`
- Interval duration: `interval_ms = bpi * beat_ms`
- Next boundary: `now + (interval_ms - ((now - epoch_ms) % interval_ms))`
- All tempo changes apply at interval boundaries to maintain musical alignment

### Time Synchronization
- Companion performs NTP-style handshake with Authority (8 pings)
- Maintains rolling median clock offset for accurate boundary timing
- Re-syncs periodically to handle clock drift

### Carabiner Integration
- TCP API for Ableton Link control
- Key operations: `SetTempo(bpm)`, `SetBeatTime()`, tempo subscription
- Companion abstracts TCP details in `CarabinerClient.cs`

## Testing

### Authority
- Test WebSocket broadcasts and state management
- Verify boundary calculations match expected intervals
- Test concurrent tempo proposals from multiple clients

### Companion
- Test Carabiner connection and Link control
- Verify boundary scheduling accuracy
- Test time-sync with simulated network latency

## Default Configuration

- **Preset BPM/BPI**: 174 BPM, 4 BPI (≈1.38s intervals)
- **Safe fallback**: 128 BPM, 8 BPI (≈3.75s intervals)
- **Policy**: Any DJ can change tempo; applies at next boundary
- **Target software**: rekordbox (Performance Mode) with Ableton Link

## Project Structure

```
/authority
  src/
    index.ts          # Fastify + WebSocket server
    rooms.ts          # In-memory room state store
    api.ts            # REST endpoints
    ws.ts             # WebSocket handler
    boundary.ts       # Interval math utilities
    public/           # Minimal web UI
/companion
  LinkJam.Companion.sln
  LinkJam.Companion/
    CarabinerClient.cs    # Carabiner TCP client
    AuthorityClient.cs    # WebSocket + time sync
    Scheduler.cs          # Boundary timer logic
    TrayApp.xaml/.cs      # Windows tray UI
    Program.cs            # Process management
  ThirdParty/
    carabiner.exe         # Bundled Link daemon
```