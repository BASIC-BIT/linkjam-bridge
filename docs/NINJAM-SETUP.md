# Complete NINJAM + LinkJam Bridge Setup Guide

## System Overview

The LinkJam Bridge **does NOT directly connect to NINJAM**. Instead, it works alongside NINJAM to synchronize tempo across remote DJs. Here's how the components work together:

```
[rekordbox] → [Ableton Link] ← [Carabiner] ← [Companion App]
     ↓                                              ↑
[Audio Output]                               [Authority Server]
     ↓                                              ↑
[JamTaba] → [NINJAM Server] ← [Other DJs]    [Web UI Control]
```

## What Each Component Does

1. **Authority Server** - Coordinates tempo/BPM changes between all DJs
2. **Companion App** - Syncs your local Ableton Link to the shared tempo
3. **Carabiner** - Bridge between Companion and Ableton Link
4. **rekordbox** - Your DJ software (with Link enabled)
5. **JamTaba** - Sends your audio to NINJAM server
6. **NINJAM Server** - Handles the actual audio streaming with bar-aligned delay

## Step-by-Step Setup

### 1. Set Up Your NINJAM Server

If you don't have a NINJAM server yet:

**Option A: Use a public server**
- List of servers: https://www.cockos.com/ninjam/servers.php

**Option B: Run your own server**
```bash
# Download NINJAM server from https://www.cockos.com/ninjam/
# Windows: ninjamserv.exe
# Configure server.cfg with:
Port 2049
MaxUsers 8
Topic "LinkJam DJ Session"
BPM 174
BPI 4
```

### 2. Install and Configure JamTaba

1. Download JamTaba standalone from: https://jamtaba-music-web-site.appspot.com/
2. Install and run JamTaba
3. Connect to your NINJAM server:
   - Click "Enter a room" 
   - Enter your NINJAM server address (e.g., `yourserver.com:2049`)
   - Use a private room with password if available

### 3. Configure Audio Routing (Windows)

You need to route rekordbox audio to JamTaba. Choose one method:

**Method A: VoiceMeeter (Recommended)**
1. Download VoiceMeeter from: https://vb-audio.com/Voicemeeter/
2. Install and run VoiceMeeter
3. In rekordbox:
   - Settings → Audio → Master Output: VoiceMeeter Input
4. In JamTaba:
   - Audio Input Device: VoiceMeeter Output

**Method B: VB-Audio Virtual Cable**
1. Download from: https://vb-audio.com/Cable/
2. Install VB-Audio Cable
3. In rekordbox:
   - Master Output: CABLE Input
4. In JamTaba:
   - Audio Input: CABLE Output

**Method C: Hardware Loopback (if your audio interface supports it)**
- Route physical output back to input using your audio interface

### 4. Configure rekordbox for Ableton Link

1. Open rekordbox in **Performance Mode**
2. Click **LINK** in the top menu bar
3. In the Link window:
   - Enable "LINK" (should show as ON)
   - You'll see the current BPM
4. For each deck:
   - Click the deck's SYNC button
   - Select "LINK" mode (not BEAT SYNC)

### 5. Start LinkJam Bridge Components

**Start Authority Server:**
```bash
cd authority
npm run dev
```
- Web UI will be at: http://localhost:3000
- Set initial BPM: 174, BPI: 4

**Start Carabiner (if not using Companion auto-start):**
```bash
cd companion
.\test-carabiner.bat
```
Or manually: `carabiner.exe -p 17000`

**Start Companion App:**
```bash
cd companion
dotnet run --project LinkJam.Companion
```
1. Enter Authority server URL: `http://localhost:3000`
2. Room ID: `main`
3. DJ Name: Your name
4. Click "Connect"

### 6. Verify Synchronization

1. **Check Companion Status:**
   - Should show "LOCKED" when synchronized
   - BPM should match Authority server

2. **Check rekordbox Link:**
   - Link window should show the BPM from Authority
   - Deck beat grids should be aligned

3. **Check JamTaba:**
   - Should show server BPM/BPI (174/4)
   - You should hear your rekordbox audio

### 7. Manual NINJAM BPM Sync (Phase 1)

**IMPORTANT:** In Phase 1, you must manually match NINJAM server BPM:

1. When Authority BPM changes (e.g., to 172):
2. In JamTaba or NINJAM server admin:
   - Manually change BPM to match (172)
3. The interval will recalculate automatically

## Full DJ Workflow

### For the First DJ:
1. Start all components (Authority, Companion, Carabiner)
2. Connect JamTaba to NINJAM server
3. Load track in rekordbox
4. Hit play on the downbeat
5. Your audio goes through NINJAM with BPI delay

### For Additional DJs:
1. Connect Companion to same Authority server
2. Connect JamTaba to same NINJAM server  
3. Wait for "LOCKED" status
4. Load track and wait for downbeat
5. Start playing in sync

### Tempo Changes:
1. Any DJ changes BPM in rekordbox Link window
2. Companion detects and proposes to Authority
3. Authority broadcasts to all DJs
4. All Companions apply at next boundary
5. **Manual step:** Update NINJAM server BPM

## Testing Your Setup

### Local Test (Single Machine):
1. Run Authority server
2. Open web UI: http://localhost:3000
3. Start Companion and connect
4. Play a kick loop in rekordbox
5. You should hear it in JamTaba with delay

### Two DJ Test:
1. Both DJs connect to same Authority
2. Both connect to same NINJAM server
3. DJ 1 plays kick on beats 1 & 3
4. DJ 2 plays kick on beats 2 & 4
5. Should hear clean 4/4 pattern in NINJAM

## Common Issues

### "Can't hear audio in JamTaba"
- Check VoiceMeeter/Cable routing
- Verify rekordbox master output
- Check JamTaba input device selection

### "Beats are not aligned"
- Ensure both DJs show "LOCKED" status
- Check that NINJAM BPM matches Authority
- Verify BPI is set to 4 on NINJAM server

### "Companion won't connect"
- Check Windows Firewall for Carabiner
- Verify Authority server is running
- Try running Carabiner manually first

### "Link not working in rekordbox"
- Enable LINK globally in top menu
- Set each deck to LINK mode (not BEAT SYNC)
- Check firewall isn't blocking Link

## Important Settings

**Default Configuration:**
- BPM: 174
- BPI: 4 (bars per interval)
- Interval: ~1.38 seconds
- Port (Carabiner): 17000
- Port (Authority): 3000

**Safe Mode (if latency issues):**
- BPM: 128
- BPI: 8
- Interval: ~3.75 seconds

## Phase 2 Features (Coming Soon)

- **Automatic NINJAM control** - Authority will set NINJAM BPM automatically
- **No manual BPM changes** - Everything syncs automatically at boundaries
- **Authentication** - Secure rooms with access control
- **Preset switching** - Quick change between BPM/BPI configurations

## Quick Checklist

- [ ] NINJAM server running with BPM 174, BPI 4
- [ ] JamTaba connected to NINJAM server
- [ ] Audio routing configured (VoiceMeeter/Cable)
- [ ] rekordbox in Performance Mode with LINK enabled
- [ ] Authority server running (http://localhost:3000)
- [ ] Carabiner running (manual or via Companion)
- [ ] Companion connected and showing "LOCKED"
- [ ] Test audio playing through full chain

Once everything is connected, your synchronized remote DJ session is ready!