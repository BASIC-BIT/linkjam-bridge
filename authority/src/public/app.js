class AuthorityControl {
    constructor() {
        this.ws = null;
        this.roomId = 'main';
        this.currentState = null;
        this.updateInterval = null;
        this.wsReconnectInterval = null;
        
        this.initializeElements();
        this.attachEventListeners();
        this.startUpdateLoop();
    }
    
    initializeElements() {
        this.elements = {
            roomId: document.getElementById('roomId'),
            connectionDot: document.getElementById('connectionDot'),
            connectionText: document.getElementById('connectionText'),
            currentBpm: document.getElementById('currentBpm'),
            currentBpi: document.getElementById('currentBpi'),
            currentBar: document.getElementById('currentBar'),
            currentBeat: document.getElementById('currentBeat'),
            boundaryCountdown: document.getElementById('boundaryCountdown'),
            bpmInput: document.getElementById('bpmInput'),
            bpiInput: document.getElementById('bpiInput'),
            updateBtn: document.getElementById('updateBtn'),
            resetEpochBtn: document.getElementById('resetEpochBtn'),
            connectBtn: document.getElementById('connectBtn'),
            refreshBtn: document.getElementById('refreshBtn'),
            log: document.getElementById('log'),
        };
    }
    
    attachEventListeners() {
        this.elements.updateBtn.addEventListener('click', () => this.updateTempo());
        this.elements.resetEpochBtn.addEventListener('click', () => this.resetEpoch());
        this.elements.connectBtn.addEventListener('click', () => this.toggleConnection());
        this.elements.refreshBtn.addEventListener('click', () => this.fetchState());
        this.elements.roomId.addEventListener('change', () => {
            this.roomId = this.elements.roomId.value;
            if (this.ws) {
                this.disconnect();
                this.connect();
            }
        });
    }
    
    async connect() {
        this.roomId = this.elements.roomId.value || 'main';
        const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
        const wsUrl = `${protocol}//${window.location.host}/ws/${this.roomId}`;
        
        try {
            this.ws = new WebSocket(wsUrl);
            
            this.ws.onopen = () => {
                this.log('Connected to WebSocket', 'success');
                this.updateConnectionStatus(true);
                this.elements.connectBtn.textContent = 'Disconnect';
                clearInterval(this.wsReconnectInterval);
            };
            
            this.ws.onmessage = (event) => {
                try {
                    const message = JSON.parse(event.data);
                    if (message.type === 'tempo_state') {
                        this.currentState = message.payload;
                        this.updateDisplay();
                        this.log(`State update: BPM=${message.payload.bpm}, BPI=${message.payload.bpi}`);
                    }
                } catch (error) {
                    console.error('Failed to parse message:', error);
                }
            };
            
            this.ws.onerror = (error) => {
                this.log('WebSocket error', 'error');
                console.error('WebSocket error:', error);
            };
            
            this.ws.onclose = () => {
                this.log('Disconnected from WebSocket');
                this.updateConnectionStatus(false);
                this.elements.connectBtn.textContent = 'Connect';
                this.ws = null;
                
                if (!this.wsReconnectInterval) {
                    this.wsReconnectInterval = setInterval(() => {
                        if (!this.ws) {
                            this.log('Attempting to reconnect...');
                            this.connect();
                        }
                    }, 5000);
                }
            };
            
        } catch (error) {
            this.log(`Connection failed: ${error.message}`, 'error');
        }
    }
    
    disconnect() {
        if (this.ws) {
            clearInterval(this.wsReconnectInterval);
            this.wsReconnectInterval = null;
            this.ws.close();
            this.ws = null;
        }
    }
    
    toggleConnection() {
        if (this.ws) {
            this.disconnect();
        } else {
            this.connect();
        }
    }
    
    async fetchState() {
        try {
            const response = await fetch(`/room/${this.roomId}/state`);
            const state = await response.json();
            this.currentState = state;
            this.updateDisplay();
            this.log('State fetched via REST');
        } catch (error) {
            this.log(`Failed to fetch state: ${error.message}`, 'error');
        }
    }
    
    async updateTempo() {
        const bpm = parseFloat(this.elements.bpmInput.value);
        const bpi = parseInt(this.elements.bpiInput.value);
        
        if (bpm < 20 || bpm > 999) {
            this.log('BPM must be between 20 and 999', 'error');
            return;
        }
        
        if (bpi < 1 || bpi > 64) {
            this.log('BPI must be between 1 and 64', 'error');
            return;
        }
        
        try {
            const response = await fetch(`/room/${this.roomId}/state`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ bpm, bpi, updated_by: 'Web UI' }),
            });
            
            if (response.ok) {
                const state = await response.json();
                this.currentState = state;
                this.updateDisplay();
                this.log(`Tempo updated: BPM=${bpm}, BPI=${bpi}`, 'success');
            } else {
                const error = await response.json();
                this.log(`Update failed: ${error.error}`, 'error');
            }
        } catch (error) {
            this.log(`Update failed: ${error.message}`, 'error');
        }
    }
    
    async resetEpoch() {
        try {
            const response = await fetch(`/room/${this.roomId}/state`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ epoch_ms: Date.now(), updated_by: 'Web UI' }),
            });
            
            if (response.ok) {
                const state = await response.json();
                this.currentState = state;
                this.updateDisplay();
                this.log('Epoch reset to current time', 'success');
            } else {
                const error = await response.json();
                this.log(`Reset failed: ${error.error}`, 'error');
            }
        } catch (error) {
            this.log(`Reset failed: ${error.message}`, 'error');
        }
    }
    
    updateDisplay() {
        if (!this.currentState) return;
        
        this.elements.currentBpm.textContent = this.currentState.bpm.toFixed(1);
        this.elements.currentBpi.textContent = this.currentState.bpi;
        this.elements.bpmInput.value = this.currentState.bpm;
        this.elements.bpiInput.value = this.currentState.bpi;
    }
    
    updateConnectionStatus(connected) {
        if (connected) {
            this.elements.connectionDot.classList.remove('disconnected');
            this.elements.connectionDot.classList.add('connected');
            this.elements.connectionText.textContent = 'Connected';
        } else {
            this.elements.connectionDot.classList.remove('connected');
            this.elements.connectionDot.classList.add('disconnected');
            this.elements.connectionText.textContent = 'Disconnected';
        }
    }
    
    startUpdateLoop() {
        this.updateInterval = setInterval(() => {
            if (this.currentState) {
                const beatMs = 60000 / this.currentState.bpm;
                const intervalMs = this.currentState.bpi * beatMs;
                const now = Date.now();
                const elapsed = (now - this.currentState.epoch_ms) % intervalMs;
                const msUntilBoundary = intervalMs - elapsed;
                
                const totalBeats = Math.floor((now - this.currentState.epoch_ms) / beatMs);
                const bar = Math.floor(totalBeats / this.currentState.bpi);
                const beat = (totalBeats % this.currentState.bpi) + 1;
                
                this.elements.currentBar.textContent = bar;
                this.elements.currentBeat.textContent = beat;
                this.elements.boundaryCountdown.textContent = `${(msUntilBoundary / 1000).toFixed(1)}s`;
            }
        }, 50);
    }
    
    log(message, type = '') {
        const entry = document.createElement('div');
        entry.className = 'log-entry';
        if (type) entry.classList.add(type);
        
        const timestamp = new Date().toLocaleTimeString();
        entry.textContent = `[${timestamp}] ${message}`;
        
        this.elements.log.insertBefore(entry, this.elements.log.firstChild);
        
        while (this.elements.log.children.length > 20) {
            this.elements.log.removeChild(this.elements.log.lastChild);
        }
    }
}

document.addEventListener('DOMContentLoaded', () => {
    const control = new AuthorityControl();
    control.fetchState();
    control.connect();
});