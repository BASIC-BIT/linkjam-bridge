# LinkJam Bridge Troubleshooting Guide

## rekordbox Shows "0 Links" Issue

This is a common issue where rekordbox doesn't see Ableton Link peers. Here's how to fix it:

### 1. Check Windows Firewall

Ableton Link uses **UDP multicast** on port **20808**. Windows Firewall often blocks this.

**Quick Fix:**
1. When rekordbox starts with Link enabled, you should get a Windows Firewall prompt
2. Make sure to check **both** Private and Public networks
3. Click "Allow access"

**Manual Firewall Rules:**
```powershell
# Run as Administrator in PowerShell
netsh advfirewall firewall add rule name="Ableton Link" dir=in action=allow protocol=UDP localport=20808
netsh advfirewall firewall add rule name="Ableton Link" dir=out action=allow protocol=UDP localport=20808
```

### 2. Check Carabiner is Actually Connected to Link

In your Carabiner window, you should see:
```
Link bpm: 174 Peers: 1 Connections: 1
```

The **Peers** count should be at least 1 when rekordbox is running with Link enabled.

If Peers = 0, Carabiner itself isn't seeing rekordbox.

### 3. Network Adapter Issues

Ableton Link can have issues with multiple network adapters (VPN, VMware, etc.).

**Solution:**
1. Disable unnecessary network adapters temporarily:
   - Open Network Connections (ncpa.cpl)
   - Right-click and disable VPN adapters, VMware adapters, etc.
   - Keep only your main network adapter active

2. Or specify the adapter for Carabiner:
   ```bash
   carabiner.exe -p 17000 -i 192.168.1.100  # Use your actual IP
   ```

### 4. rekordbox Link Setup

Make sure Link is properly enabled in rekordbox:

1. **Global Link Enable:**
   - Top menu bar → LINK → Make sure it's ON (blue)
   
2. **Per-Deck Link:**
   - Click SYNC button on each deck
   - Select "LINK" from the dropdown (not BEAT SYNC)
   
3. **Link Settings:**
   - Click the LINK button to open Link window
   - You should see "LINK: ON" at the top
   - BPM should be shown

### 5. Test with Link Diagnostic Tool

Download Link diagnostic tool to verify Link is working:
1. Download "LinkHut" (free Ableton Link tester): https://linkhut.app/
2. Run LinkHut - it should show other Link peers
3. If LinkHut sees peers but rekordbox doesn't, it's a rekordbox issue

### 6. Order of Operations

The order you start things matters:

**Correct Order:**
1. Start Carabiner first
2. Start rekordbox and enable Link
3. Verify Carabiner shows "Peers: 1"
4. Then start Companion app

### 7. Common Windows Issues

**Windows Defender:**
- May block multicast traffic
- Add exceptions for rekordbox.exe and carabiner.exe

**Network Profile:**
- Make sure your network is set to "Private" not "Public"
- Settings → Network & Internet → Status → Properties → Network profile → Private

**Multicast Routing:**
```cmd
# Check if multicast is enabled
route print | findstr 224.0.0.0
```

### 8. Alternative: Direct Connection Test

If nothing works, test if Carabiner and rekordbox can see each other:

1. Close Companion app
2. In Carabiner window, type:
   ```
   status
   ```
   You should see current BPM and peers

3. In rekordbox, change the BPM
4. Carabiner should immediately show the new BPM

If this doesn't work, it's a network/firewall issue, not a LinkJam Bridge issue.

## Quick Checklist

- [ ] Windows Firewall allows rekordbox and Carabiner
- [ ] Network is set to Private, not Public
- [ ] No VPN or virtual adapters interfering
- [ ] rekordbox LINK is enabled globally AND per-deck
- [ ] Carabiner started before rekordbox
- [ ] UDP port 20808 is not blocked
- [ ] Multicast traffic is allowed on your network

## If All Else Fails

1. **Restart everything in order:**
   ```
   1. Close all apps
   2. Start Carabiner: .\test-carabiner.bat
   3. Start rekordbox, enable Link
   4. Check Carabiner shows "Peers: 1"
   5. Start Companion app
   ```

2. **Try on a different network** (to rule out network issues)

3. **Use a single machine test** (run everything on one PC first)

## Network Diagnostic Commands

```powershell
# Check if port 20808 is in use
netstat -an | findstr 20808

# Check Windows Firewall status
netsh advfirewall show allprofiles

# Check network adapters
ipconfig /all

# Test multicast
ping 224.0.0.251
```

## Still Not Working?

The issue is likely:
1. **Firewall/Antivirus** blocking UDP multicast
2. **Network adapter** confusion (multiple adapters)
3. **rekordbox Link bug** (try restarting rekordbox)
4. **Windows network profile** set to Public

Remember: Ableton Link uses **UDP multicast** which is often blocked by default on Windows!