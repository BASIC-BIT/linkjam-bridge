Got it. Here’s the **complete, minimal‑to‑lovable product spec** you asked for—optimized for shipping fast, keeping the surface area tiny, and leaving room to iterate. No over‑engineering, no filler.

---

## ELI5 (what we’re doing in one minute)

Two or more DJs play together from different places.
We **delay everyone by a musical chunk** (e.g., \~1.38 s), so their music lines up on the beat when it reaches the audience.
Each DJ’s decks **follow the same beat clock** locally (Ableton Link), so nobody “aligns by ear.”
A tiny app on each DJ’s PC **sets that local clock**, and a tiny server **decides the shared tempo**.
JamTaba handles the NINJAM audio pipeline; OBS sends the final mix to VRChat via VRCDN.

---

## Elevator pitch (for others)

> **LinkJam Bridge**: a dead‑simple way to run remote back‑to‑back DJ sets that sound tight.
> We use **NINJAM** to buffer by a musical interval (bar‑aligned) and **Ableton Link** so each DJ’s software follows the same tempo locally.
> A tiny **Companion** app on each DJ’s PC talks to **Carabiner** to set local Link tempo/phase at the next bar; a tiny **Authority** service sets the room tempo once.
> Result: quantized, on‑grid transitions that feel intentional—no “flam,” no guesswork.

---

## Scope & constraints (intentionally narrow)

* **DJ software:** rekordbox (Performance Mode) with **Ableton Link** enabled per deck.
* **Audio transport:** **JamTaba (standalone)** → **NINJAM** server.
* **Program output:** **OBS → VRCDN** → VRChat world.
* **Latency target:** **BPI 4 @ 174 BPM ≈ 1.38 s** interval (reactive feel).
  (Safe preset baked in: BPI 8 @ 128 BPM ≈ 3.75 s.)
* **No “tempo driver” lock:** *any DJ* can change BPM intentionally; we apply on **next interval boundary** to keep it musical.
* **MVP policy:** **No admin‑bot yet** (we set BPM/BPI on the NINJAM server manually for pilots). Admin‑bot comes in Phase 2.

---

## System overview

```
[rekordbox + Link]  →  [JamTaba]  →  NINJAM server  →  (everyone hears previous interval)
       ▲                            ▲
       │                            │
 [Companion (Win)]            [Authority (server)]
    └── talks to Carabiner        └── holds {bpm, bpi, epoch}
        to set local Link             broadcasts tempo over WS
        on the boundary               (optionally controls NINJAM later)
```

* **Authority (Node/TS, server you host):**
  Holds `{ bpm, bpi, epoch_ms }`, calculates next interval boundary, and broadcasts over WebSocket. (Phase 2: add admin‑bot to set BPM/BPI on the NINJAM server automatically.)
* **Companion (Windows C# tray):**
  Connects to Authority, talks to local **Carabiner** (Link daemon), and **sets Ableton Link tempo + re‑phases to bar start at the boundary**. Also watches for **local Link BPM changes** (from rekordbox’s Link window) and **proposes** them upstream.
  *Default UX:* no toasts, no proposal dialog. You’ll notice “surprise BPM change” in rekordbox and on the shared metronome—intentionally simple.

---

## Phased delivery plan (each phase is shippable)

### Phase 1 — **MVP (works end‑to‑end)**

**Goal:** Get a clean, on‑grid B2B into VRChat with BPI 4 @ 174.

**Deliverables**

1. **Authority v1 (Node/TS)**

   * REST: `GET /room/:id/state` → `{ bpm, bpi, epoch_ms }`
   * WS: broadcasts `tempo_state` (same payload) when state changes
   * Simple in‑memory room store; single static HTML page to set BPM/BPI/epoch (no auth for pilot; deploy behind your reverse proxy)
   * **No admin‑bot** yet.

2. **Companion v1 (C# .NET 8, Windows tray)**

   * Bundles and launches **Carabiner** on localhost
   * WS subscribe to Authority
   * **Time sync handshake** to estimate server‑client clock offset (simple NTP‑style ping/pong)
   * Boundary scheduler: at `nextBoundary`, send to Carabiner:

     * set tempo = `{bpm}`
     * set beat‑time so local Link lands **exactly at bar start**
   * Detect local Link BPM changes (via Carabiner subscription); emit `tempo_proposal` → Authority (Authority instantly rebroadcasts and updates state; we apply on the **next boundary**)

3. **Runbook & configs** (docs):

   * **NINJAM server**: install, create a private room; set **BPM=174, BPI=4** for pilot; verify JamTaba shows those values
   * **JamTaba**: connect to your private server; set quality “High”; set buffer as needed
   * **rekordbox**: Performance Mode, enable **Link** globally, switch decks to **LINK**; use Link sub‑screen or mapped encoder to change BPM if needed
   * **Audio routing (Windows)**: one of

     * VoiceMeeter or VB‑Audio Cable (rekordbox → JamTaba input), or
     * Scarlett 2i2 loopback (if your model exposes it)
   * **OBS**: capture JamTaba app audio (or its output device) → stream to VRCDN; add a tiny text source showing current BPM/BPI (optional)

**Acceptance tests**

* Two PCs join; both Companions show **LOCKED**; rekordbox decks in LINK mode.
* Hitting CUE/PLAY on the next bar produces no “flam” on kicks in the VRCDN program.
* Changing BPM on DJ‑A’s Link panel updates both Companions’ local Link and—after you manually change NINJAM room BPM to match—remains tight at the boundary.
* End‑to‑end (JamTaba interval + VRCDN) delay matches expectation (\~1.4 s + platform overhead).

---

### Phase 2 — **Ops polish**

* **Admin‑bot** in Authority to set **NINJAM BPM/BPI** at boundary (no more manual changes in JamTaba/server).
* Minimal auth (shared room token) and HTTPS/WSS by default.
* Authority UI shows **last tempo change** (“172.0 from DJ‑B”) and a small **boundary countdown** (optional; can be hidden by default).

---

### Phase 3 — **Quality of life**

* Companion: tray tooltip with bar\:beat, “LOCKED”, and current offset; autostart toggle.
* Authority: presets (Reactive BPI 4 @ 174; Standard BPI 8 @ 128).
* Logging: JSON logs of tempo changes with DJ name, latency, boundary timing; export dialog.

---

## Authority (Node/TS) — spec

**State**

```ts
type TempoState = {
  roomId: string
  bpm: number       // e.g., 174.0
  bpi: number       // e.g., 4
  epoch_ms: number  // ms wall-clock when bar 0 beat 0 started
  updated_by?: string // optional DJ name
};
```

**REST**

* `GET /room/:id/state` → `TempoState`
* `POST /room/:id/state` (Phase 1 operator action only) → accepts `{ bpm?, bpi?, epoch_ms? }` and updates state

**WebSocket**

* Outbound broadcast: `tempo_state` (TempoState)
* Inbound from Companion: `tempo_proposal`:

```ts
type TempoProposal = {
  roomId: string
  bpm: number
  proposed_by: string
  client_ms: number // client send time (for telemetry)
}
```

**Policy (Phase 1)**

* On `tempo_proposal`, set `state.bpm` immediately and broadcast `tempo_state`.
  *(Rely on boundary scheduling inside Companion to apply locally on the next interval. You’ll manually flip NINJAM room BPM to the same number for pilot runs.)*

**Boundary math (shared)**

* Beat duration (ms): `beat_ms = 60000 / bpm`
* Interval duration (ms): `interval_ms = bpi * beat_ms`
* Given `now_ms` and `epoch_ms`, compute elapsed in interval:
  `phase = (now_ms - epoch_ms) % interval_ms`
  `nextBoundary = now_ms + (interval_ms - phase)`

*(Authority doesn’t need to push `nextBoundary`; each Companion computes it locally after time‑sync.)*

---

## Companion (Windows C#) — spec

**Responsibilities**

* Launch & monitor **Carabiner** (bundled binary) on localhost
* WS connect to Authority, subscribe to `tempo_state`
* **Time sync**: On connect, ping Authority `n=8` times:

  * Send `{ t0_client }` → receive `{ t1_server }` → measure RTT; estimate `clock_offset ≈ t1 - (t0 + rtt/2)`
  * Maintain a rolling median offset
* **Apply schedule**:

  * On new `tempo_state`, compute `nextBoundary` using **server time + offset**
  * At boundary:

    * `SetTempo(bpm)` via Carabiner
    * `SetBeatTimeToBarStart(epoch_ms)` (i.e., re‑phase so local Link thinks it’s exactly bar start now)
  * Show “LOCKED” when applied; otherwise “ARMED”
* **Proposal watcher**:

  * Subscribe to Carabiner tempo updates; if local tempo moves by > 0.1 BPM, send `tempo_proposal{ bpm, proposed_by, client_ms }`

**Tray UI**

* Status dot (LOCKED/ARMED/DISCONNECTED)
* Room URL field + Connect button
* DJ name field
* Small readout: `BPM 174.0  |  BPI 4  |  Next bar in 1.2s`

**Carabiner adapter**

* Abstract calls:

  * `SetTempo(double bpm)`
  * `SetBeatTimeTo(double beatsFromEpoch)` or `RephaseToBarStart(long epoch_ms, bpm, bpi)`
  * `SubscribeToTempo()` (events)
* *Note:* Use Carabiner’s documented TCP API format; don’t invent wire strings. Implement a thin adapter class that hides the TCP details from the rest of the app.

**Error handling**

* If Authority is unreachable, Companion keeps last known state (no tempo changes).
* If Carabiner dies, restart it; if it’s missing, show a “Install components” link.

---

## NINJAM & JamTaba — pilot setup (Windows)

1. **NINJAM server**

   * Install on your host VM. Create a private room with login (pilot only).
   * For Phase 1, set **BPM=174** and **BPI=4** in the server (or in JamTaba if you have permissions).
   * Ensure firewall/NAT allow the server’s configured ports.
   * Keep a simple admin console handy to bump BPM later.

2. **JamTaba (standalone)**

   * Connect to your server, log in to the private room.
   * **Input** = your routed rekordbox master (VoiceMeeter/Virtual Cable/loopback).
   * **Quality** = High; adjust buffer if you hear artifacts.
   * Verify it displays **BPM 174 / BPI 4** after you set the room.

3. **rekordbox**

   * Performance Mode.
   * **Enable LINK** globally; set decks to LINK.
   * Use the Link sub‑screen (or MIDI‑mapped encoder) to nudge BPM when you intend to.

4. **OBS → VRCDN**

   * Add JamTaba as an **Application Audio Capture** (or capture its output device).
   * Stream to VRCDN; optionally add a small text element showing current BPM/BPI (pulled from Authority’s REST once a second).

---

## Test plan (fast)

* **T0: Local lock** — Companion shows LOCKED on both PCs; decks in LINK. Load kick loops and start on next bar.
* **T1: Crossfades** — Perform 8‑beat crossfades A↔B; verify no flam in the VRCDN program.
* **T2: Tempo change** — DJ‑A bumps Link BPM to 172. Companion sends proposal → Authority broadcasts → both Companions apply at next boundary. Manually set NINJAM room to 172 to match; verify continuity.
* **T3: Abuse** — Brief Wi‑Fi hiccup or CPU spike; JamTaba buffer + High quality should keep the program coherent. If not, increase buffer or switch to the safe preset (BPI 8 @ 128).

---

## Risks & mitigations

* **Tiny intervals are fragile.** That’s why we set **BPI 4** (≈1.38 s at 174). Lower than this with JamTaba isn’t supported; higher is safer.
* **Two encodes (Vorbis → AAC)**. Keep JamTaba quality high and OBS bitrate adequate; audience quality is typically fine in VRChat.
* **Clock skew**. The Companion’s NTP‑like sync avoids Windows clock wander. If drift is observed, re‑sync every \~20–30 s.

---

## Future phases (only if needed)

* **Admin‑bot**: Authority sets NINJAM BPM/BPI at the boundary (no manual step).
* **UI niceties**: lightweight toasts (“→ 172 at next bar”), on/off.
* **OBS helper**: minimal plugin/panel to overlay current BPM/BPI from Authority REST (optional).
* **Cross‑platform Companion**: Electron/TS version if you later need macOS.

---

## What to hand to your coding agent (crisp instructions)

**Repo layout**

```
/authority
  package.json / tsconfig.json
  src/
    index.ts              // Fastify + ws server
    rooms.ts              // in-memory rooms store
    api.ts                // REST: get/set state
    ws.ts                 // WS hub: broadcasts tempo_state, receives tempo_proposal
    boundary.ts           // beat/interval math helpers
    public/
      index.html          // minimal UI: bpm, bpi, set buttons (no toasts)
      app.js              // fetches state; sends updates
/companion
  LinkJam.Companion.sln   // .NET 8
  /LinkJam.Companion
    CarabinerClient.cs    // TCP client; SetTempo, SetBeatTime, Subscribe
    AuthorityClient.cs    // WS client; time-sync handshake
    Scheduler.cs          // boundary computation, timer
    TrayApp.xaml/.cs      // tray UI
    Program.cs            // single-instance, autostart, process mgmt for carabiner.exe
  /ThirdParty
    carabiner.exe (bundled)
```

**Authority**

* Implement REST+WS as defined; no auth (pilot).
* On `tempo_proposal`, **update state immediately** and broadcast (Companions defer application to boundary).
* Add `GET /room/:id/state` polling for the OBS text overlay (optional).

**Companion**

* On connect: perform 8× ping/pong; compute rolling median **offset\_ms**.
* On state update:

  * `beat_ms = 60000 / bpm`
  * `interval_ms = bpi * beat_ms`
  * `server_now ≈ client_now + offset_ms`
  * `phase = (server_now - epoch_ms) % interval_ms`
  * `nextBoundary_client = client_now + (interval_ms - phase)`
  * Schedule a high‑precision timer at `nextBoundary_client`.
  * On fire: `SetTempo(bpm); RephaseToBarStart(epoch_ms)` via Carabiner.
* Watch Carabiner for local Link BPM changes; if ΔBPM ≥ 0.1, send `tempo_proposal`.

**Carabiner adapter**

* Implement per its doc (don’t guess the wire format).
* Wrap TCP reconnects; expose events for tempo updates and beat time.

**Docs**

* Provide a **pilot runbook** with your exact NINJAM room, JamTaba settings, and OBS profile.

**Done‑means (acceptance)**

* Two Windows machines, on commodity internet, achieve LOCKED status and produce **clean program** at BPI 4 @ 174 with manual NINJAM BPM match.
* BPM changes from either DJ apply at boundary with no audible lurch.

---

## Final defaults (so we move fast)

* **Preset:** BPI 4 @ 174.
* **Policy:** *Anyone can change BPM*; apply at **next boundary**; no UI toasts in MVP.
* **No “try BPI 3” toggle** in UI (per your direction).
* **Admin‑bot:** Phase 2.

If you want, I can follow up with a one‑page **pilot checklist** you can literally tick off during the first run (server, JamTaba, Companion, rekordbox, OBS).
