using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SocketGhost.Core
{
    public interface IFlowStore
    {
        Task InitializeAsync();
        Task StoreFlowAsync(StoredFlow flow);
        Task<StoredFlow?> GetFlowAsync(string flowId);
        Task<IEnumerable<FlowMetadata>> GetFlowsAsync(int limit, int offset, FlowFilter filter);
        Task DeleteFlowAsync(string flowId);
        Task PruneAsync(int retentionDays, long maxTotalBytes);
        Task<long> GetTotalSizeAsync();
    }

    public class FlowFilter
    {
        public int? Pid { get; set; }
        public string? Method { get; set; }
        public string? Query { get; set; }
        public DateTime? Since { get; set; }
    }

    public class StoredFlow
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
        public int Pid { get; set; }
        public string Method { get; set; } = "";
        public string Url { get; set; } = "";
        public StoredFlowRequest Request { get; set; } = new();
        public StoredFlowResponse Response { get; set; } = new();
        public bool ViaUpdate { get; set; }
        public bool ViaManualResend { get; set; }
        public List<string> ScriptApplied { get; set; } = new();
        public string? Notes { get; set; }
        public long SizeBytes { get; set; }
    }

    public class StoredFlowRequest
    {
        public Dictionary<string, string> Headers { get; set; } = new();
        public string? BodyPreview { get; set; }
        public string? FullBodyPath { get; set; }
        public bool BodyIsBinary { get; set; }
    }

    public class StoredFlowResponse
    {
        public int StatusCode { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public string? BodyPreview { get; set; }
        public string? FullBodyPath { get; set; }
        public bool BodyIsBinary { get; set; }
    }
    
    public class FlowMetadata
    {
        public string Id { get; set; } = "";
        public DateTime CapturedAt { get; set; }
        public int Pid { get; set; }
        public string Method { get; set; } = "";
        public string Url { get; set; } = "";
        public int StatusCode { get; set; }
        public long SizeBytes { get; set; }
        public bool ViaUpdate { get; set; }
        public bool ViaManualResend { get; set; }
    }
}
