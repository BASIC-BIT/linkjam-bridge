# LinkJam Bridge - Quick Start Guide

## The Key Concept

**LinkJam Bridge does NOT replace NINJAM** - it works alongside it to keep everyone's tempo in sync.

- **NINJAM** handles the audio streaming and bar-aligned delays
- **LinkJam Bridge** keeps all DJs at the same BPM via Ableton Link
- You still use JamTaba to connect to NINJAM

## Minimal Setup (5 Minutes)

### What You Need Running:

1. **Your NINJAM Server** (you already have this)
   - Set to BPM: 174, BPI: 4

2. **JamTaba** → Connected to your NINJAM server
   - Download: https://jamtaba-music-web-site.appspot.com/

3. **Audio Routing** (pick one):
   - VoiceMeeter: https://vb-audio.com/Voicemeeter/
   - OR VB-Cable: https://vb-audio.com/Cable/
   - Route: rekordbox → VoiceMeeter/Cable → JamTaba

4. **rekordbox**
   - Performance Mode
   - LINK enabled (top menu)
   - Decks set to LINK mode

5. **LinkJam Bridge Authority** (coordinator server)
   ```bash
   cd authority
   npm install  # first time only
   npm run dev
   ```
   - Open http://localhost:3000 in browser
   - Set BPM: 174, BPI: 4

6. **Carabiner** (run manually for now)
   ```bash
   cd companion
   .\test-carabiner.bat
   ```
   - Should open a window showing Link status

7. **LinkJam Companion** (Windows tray app)
   ```bash
   cd companion
   dotnet run --project LinkJam.Companion
   ```
   - Server URL: http://localhost:3000
   - Room: main
   - DJ Name: (your name)
   - Click Connect
   - Should show "LOCKED"

## Quick Test

1. Play a kick loop in rekordbox
2. You should hear it in JamTaba (with NINJAM delay)
3. Change BPM in rekordbox Link window
4. Companion shows new BPM
5. **Manual:** Update NINJAM server to match new BPM

## For Multiple DJs

Each DJ needs:
- JamTaba → Same NINJAM server
- Companion → Same Authority server (host's IP:3000)
- rekordbox with Link enabled
- Same audio routing setup

## The Flow

```
Your Setup:
rekordbox (LINK) → Audio → JamTaba → NINJAM Server
    ↑                                      ↑
    Link                              (all DJs connect here)
    ↑
Carabiner ← Companion ← Authority Server
                            ↑
                    (all DJs connect here too)
```

## If Things Don't Work

1. **Can't connect Companion?**
   - Make sure Carabiner is running (window should be visible)
   - Check Authority server is running (http://localhost:3000 works)

2. **No audio in JamTaba?**
   - Check VoiceMeeter/Cable routing
   - rekordbox master → VoiceMeeter Input
   - JamTaba input ← VoiceMeeter Output

3. **Not synced?**
   - Companion must show "LOCKED"
   - NINJAM BPM must match Authority BPM (manual for now)

## Remember

- **Phase 1:** You manually change NINJAM BPM when someone changes tempo
- **Phase 2:** Will do this automatically (coming soon)
- The ~1.38 second delay is normal (BPI 4 @ 174 BPM)