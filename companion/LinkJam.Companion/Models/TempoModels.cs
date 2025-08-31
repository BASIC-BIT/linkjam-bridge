using System;
using Newtonsoft.Json;

namespace LinkJam.Companion.Models
{
    public class TempoState
    {
        [JsonProperty("roomId")]
        public string RoomId { get; set; } = string.Empty;
        
        [JsonProperty("bpm")]
        public double Bpm { get; set; }
        
        [JsonProperty("bpi")]
        public int Bpi { get; set; }
        
        [JsonProperty("epoch_ms")]
        public long EpochMs { get; set; }
        
        [JsonProperty("updated_by")]
        public string? UpdatedBy { get; set; }
        
        [JsonProperty("updated_at")]
        public long? UpdatedAt { get; set; }
    }

    public class TempoProposal
    {
        [JsonProperty("roomId")]
        public string RoomId { get; set; } = string.Empty;
        
        [JsonProperty("bpm")]
        public double Bpm { get; set; }
        
        [JsonProperty("proposed_by")]
        public string ProposedBy { get; set; } = string.Empty;
        
        [JsonProperty("client_ms")]
        public long ClientMs { get; set; }
    }

    public class TimeSyncPing
    {
        [JsonProperty("t0_client")]
        public long T0Client { get; set; }
    }

    public class TimeSyncPong
    {
        [JsonProperty("t0_client")]
        public long T0Client { get; set; }
        
        [JsonProperty("t1_server")]
        public long T1Server { get; set; }
        
        [JsonProperty("t2_server")]
        public long T2Server { get; set; }
    }

    public class WebSocketMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonProperty("payload")]
        public object? Payload { get; set; }
    }

    public class BoundaryInfo
    {
        public DateTime NextBoundary { get; set; }
        public double BeatMs { get; set; }
        public double IntervalMs { get; set; }
        public int CurrentBar { get; set; }
        public int CurrentBeat { get; set; }
        public double MsUntilBoundary { get; set; }
    }

    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Armed,
        Locked
    }
}