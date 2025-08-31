# LinkJam Bridge

A synchronization system for remote DJ collaboration using NINJAM and Ableton Link.

## Overview

LinkJam Bridge enables multiple DJs to perform synchronized back-to-back sets from different locations by:
- Using NINJAM for bar-aligned audio buffering (BPI intervals)
- Synchronizing tempo via Ableton Link across all participants
- Applying tempo changes at musical boundaries to maintain grid alignment

## System Components

### 1. Authority Server (Node.js/TypeScript)
Central coordination server that maintains tempo state and broadcasts changes to all connected DJs.

### 2. Companion App (Windows C# .NET 8)
Windows tray application that runs on each DJ's PC, managing local Ableton Link synchronization.

## Quick Start

### Prerequisites
- Node.js 18+ and npm
- .NET 8 SDK
- Carabiner (Ableton Link bridge)
- rekordbox with Ableton Link enabled
- NINJAM server access
- JamTaba standalone client

### Authority Server Setup

```bash
cd authority
npm install
npm run dev
```

The server will start on `http://localhost:3000` with:
- Web UI: `http://localhost:3000`
- REST API: `http://localhost:3000/room/:roomId/state`
- WebSocket: `ws://localhost:3000/ws/:roomId`

### Companion App Setup

1. **Download Carabiner:**
   - Get the Windows release from [Carabiner Releases](https://github.com/Deep-Symmetry/carabiner/releases)
   - Place `carabiner.exe` in `companion/LinkJam.Companion/ThirdParty/`

2. **Build and Run:**
```bash
cd companion
dotnet build
dotnet run --project LinkJam.Companion
```

## Configuration

### rekordbox Setup
1. Open rekordbox in Performance Mode
2. Enable LINK in the top menu
3. Set each deck to LINK mode
4. Use the Link window to monitor/adjust BPM

### JamTaba Setup
1. Connect to your NINJAM server
2. Set room BPM to 174 and BPI to 4 (for MVP testing)
3. Configure audio input from rekordbox (via VoiceMeeter or virtual cable)
4. Set quality to "High"

### Audio Routing (Windows)
Options for routing rekordbox audio to JamTaba:
- **VoiceMeeter**: Route rekordbox master to virtual input
- **VB-Audio Cable**: Use virtual audio cable
- **Hardware Loopback**: If your audio interface supports it

## Testing the System

### Basic Connection Test
1. Start Authority server
2. Open web UI at `http://localhost:3000`
3. Launch Companion app on DJ machine
4. Enter server URL, room ID, and DJ name
5. Click Connect
6. Verify status shows "LOCKED"

### Tempo Sync Test
1. Connect two DJ machines to the same room
2. Load kick loops on both machines
3. Start playback on the downbeat
4. Verify no "flam" between kicks
5. Change BPM in rekordbox Link window
6. Verify both machines update at the next boundary

### Default Configuration
- **BPM**: 174
- **BPI**: 4 (bars per interval)
- **Latency**: ~1.38 seconds
- **Safe Mode**: BPM 128, BPI 8 (~3.75 seconds)

## Architecture Details

### Boundary Synchronization
All tempo changes are applied at interval boundaries to maintain musical alignment:
- Beat duration: `60000 / bpm` ms
- Interval duration: `bpi * beat_duration` ms
- Next boundary: Calculated from shared epoch time

### Time Synchronization
The Companion app performs NTP-style time sync with the Authority server:
- 8 ping/pong samples on connection
- Rolling median offset calculation
- Accurate boundary scheduling

### Tempo Proposals
Any DJ can change tempo:
1. Local Link BPM change detected by Companion
2. Proposal sent to Authority
3. Authority broadcasts new state
4. All Companions apply at next boundary

## Troubleshooting

### Companion Can't Find Carabiner
- Ensure `carabiner.exe` is in `ThirdParty/` directory
- Check Windows Defender/antivirus isn't blocking it
- Try running Carabiner manually first

### WebSocket Connection Fails
- Check firewall settings
- Verify Authority server is running
- Confirm correct server URL and port

### Audio Sync Issues
- Increase JamTaba buffer if hearing artifacts
- Verify NINJAM server BPM matches Authority state
- Check network latency between locations

## Phase 2 Features (Planned)

- Admin-bot for automatic NINJAM server control
- Authentication and HTTPS/WSS
- Preset management (quick BPM/BPI switching)
- Detailed logging and telemetry
- Cross-platform Companion app

## Development

### Authority Server
```bash
cd authority
npm run dev     # Development with hot reload
npm run build   # Build for production
npm start       # Run production build
```

### Companion App
```bash
cd companion
dotnet build
dotnet publish -c Release -r win-x64 --self-contained
```

## License

This project is designed for the VRChat DJ community to enable remote collaboration.