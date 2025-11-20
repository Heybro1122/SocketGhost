using System;
using System.Collections.Generic;

namespace SocketGhost.Core
{
    public class SocketGhostFlowEvent
    {
        public string v { get; set; } = "0.1";
        public string type { get; set; }
        public SocketGhostFlowData flow { get; set; }
    }

    public class SocketGhostFlowData
    {
        public string flowId { get; set; }
        public int? pid { get; set; }
        public string method { get; set; }
        public string url { get; set; }
        public Dictionary<string, string> headers { get; set; } = new Dictionary<string, string>();
        public string bodyPreview { get; set; }
        public int responseStatusCode { get; set; }
        public Dictionary<string, string> responseHeaders { get; set; }
        public string responseBody { get; set; }
        public List<string> scriptApplied { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents modifications to a paused flow
    /// </summary>
    public class FlowUpdate
    {
        public Dictionary<string, string> headers { get; set; }
        public string body { get; set; }
    }

    public class ManualResendInfo
    {
        public string remoteHost { get; set; }
        public long bodyLength { get; set; }
        public double durationMs { get; set; }
        public int statusCode { get; set; }
    }
}
