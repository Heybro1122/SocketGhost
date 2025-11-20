using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Titanium.Web.Proxy.EventArguments;

namespace SocketGhost.Core
{
    /// <summary>
    /// Manages flow interception: pause/resume/drop for selected PIDs
    /// </summary>
    public class InterceptorManager
    {
        private readonly HashSet<int> _interceptPids = new HashSet<int>();
        private readonly ConcurrentDictionary<string, PausedFlow> _pausedFlows = new ConcurrentDictionary<string, PausedFlow>();
        private readonly WebSocketServer _wsServer;
        private readonly object _lock = new object();
        
        public const int DEFAULT_PAUSE_TIMEOUT_SECONDS = 60;

        public InterceptorManager(WebSocketServer wsServer)
        {
            _wsServer = wsServer;
        }

        /// <summary>
        /// Add a PID to the intercept list
        /// </summary>
        public void AddInterceptPid(int pid)
        {
            lock (_lock)
            {
                _interceptPids.Add(pid);
            }
            Console.WriteLine($"[Interceptor] Enabled for PID {pid}");
        }

        /// <summary>
        /// Remove a PID from the intercept list
        /// </summary>
        public void RemoveInterceptPid(int pid)
        {
            lock (_lock)
            {
                _interceptPids.Remove(pid);
            }
            Console.WriteLine($"[Interceptor] Disabled for PID {pid}");
        }

        /// <summary>
        /// Check if a PID should be intercepted
        /// </summary>
        public bool ShouldIntercept(int? pid)
        {
            if (!pid.HasValue) return false;
            
            lock (_lock)
            {
                return _interceptPids.Contains(pid.Value);
            }
        }

        /// <summary>
        /// Try to pause a flow if it matches an intercepted PID
        /// Returns true if paused, false if should proceed normally
        /// </summary>
        public async Task<bool> TryPauseFlowAsync(string flowId, int? pid, SessionEventArgs session, SocketGhostFlowData flowData)
        {
            if (!ShouldIntercept(pid))
            {
                return false; // Not intercepted, proceed normally
            }

            var pausedFlow = new PausedFlow
            {
                flowId = flowId,
                pid = pid,
                session = session,
                flowData = flowData,
                receivedAt = DateTime.UtcNow
            };

            _pausedFlows[flowId] = pausedFlow;

            // Start timeout timer
            StartTimeoutTimer(flowId);

            // Broadcast flow.paused event
            await _wsServer.BroadcastFlowPausedAsync(pausedFlow);

            // Log to console
            Console.WriteLine($"{{\"type\":\"flow.paused\",\"flowId\":\"{flowId}\",\"pid\":{pid},\"method\":\"{flowData.method}\",\"url\":\"{flowData.url}\"}}");

            return true; // Flow paused
        }

        /// <summary>
        /// Forward a paused flow
        /// </summary>
        private readonly ManualResend _manualResend = new ManualResend();

        /// <summary>
        /// Forward a paused flow
        /// </summary>
        public async Task ForwardFlowAsync(string flowId)
        {
            if (!_pausedFlows.TryRemove(flowId, out var pausedFlow))
            {
                Console.WriteLine($"[Interceptor] Warning: Flow {flowId} not found for forward");
                return;
            }

            // Cancel timeout timer
            pausedFlow.timeoutTimer?.Dispose();

            bool viaUpdate = pausedFlow.update != null;
            bool viaManualResend = false;

            if (viaUpdate)
            {
                Console.WriteLine($"[Interceptor] Forwarding flow {flowId} with modifications via ManualResend");
                
                var result = await _manualResend.ManualResendAsync(pausedFlow, pausedFlow.update, CancellationToken.None);
                
                if (result.Success)
                {
                    viaManualResend = true;
                    
                    // Broadcast flow.manual_resend event
                    var info = new ManualResendInfo
                    {
                        remoteHost = new Uri(pausedFlow.flowData.url).Host,
                        bodyLength = result.BodyLength,
                        durationMs = result.DurationMs,
                        statusCode = result.StatusCode
                    };
                    
                    await _wsServer.BroadcastFlowManualResendAsync(flowId, pausedFlow.pid, info);
                    Console.WriteLine($"{{\"type\":\"flow.manual_resend\",\"flowId\":\"{flowId}\",\"pid\":{pausedFlow.pid},\"remoteHost\":\"{info.remoteHost}\",\"bodyLength\":{info.bodyLength},\"durationMs\":{info.durationMs},\"statusCode\":{info.statusCode}}}");
                }
                else
                {
                    // Manual resend failed
                    Console.WriteLine($"[Interceptor] Manual resend failed for flow {flowId}: {result.Error}");
                    
                    // Send 502 to client
                    try
                    {
                        var response = $"{{\"error\": \"SocketGhost: forward failed\", \"reason\": \"{result.Error}\"}}";
                        pausedFlow.session.GenericResponse(response, System.Net.HttpStatusCode.BadGateway);
                    }
                    catch { }

                    await _wsServer.BroadcastFlowErrorAsync(flowId, result.Error);
                    return; // Stop here, don't broadcast forwarded
                }
            }
            
            // Broadcast flow.forwarded event
            await _wsServer.BroadcastFlowActionAsync("flow.forwarded", flowId, viaUpdate, null, viaManualResend);

            Console.WriteLine($"{{\"type\":\"flow.forwarded\",\"flowId\":\"{flowId}\",\"pid\":{pausedFlow.pid},\"viaUpdate\":{(viaUpdate ? "true" : "false")},\"viaManualResend\":{(viaManualResend ? "true" : "false")},\"timestamp\":\"{DateTime.UtcNow:O}\"}}");
        }

        /// <summary>
        /// Drop a paused flow
        /// </summary>
        public async Task DropFlowAsync(string flowId)
        {
            if (!_pausedFlows.TryRemove(flowId, out var pausedFlow))
            {
                Console.WriteLine($"[Interceptor] Warning: Flow {flowId} not found for drop");
                return;
            }

            // Cancel timeout timer
            pausedFlow.timeoutTimer?.Dispose();

            // Respond with 502 Bad Gateway
            try
            {
                var response = "{\"error\": \"SocketGhost: flow dropped by user\"}";
                pausedFlow.session.GenericResponse(response, System.Net.HttpStatusCode.BadGateway);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Interceptor] Error sending drop response: {ex.Message}");
            }

            // Broadcast flow.dropped event
            await _wsServer.BroadcastFlowActionAsync("flow.dropped", flowId);

            Console.WriteLine($"{{\"type\":\"flow.dropped\",\"flowId\":\"{flowId}\",\"timestamp\":\"{DateTime.UtcNow:O}\"}}");
        }

        /// <summary>
        /// Start a timeout timer for a paused flow
        /// </summary>
        private void StartTimeoutTimer(string flowId)
        {
            var timer = new Timer(async (_) =>
            {
                await AutoForwardAsync(flowId);
            }, null, TimeSpan.FromSeconds(DEFAULT_PAUSE_TIMEOUT_SECONDS), Timeout.InfiniteTimeSpan);

            if (_pausedFlows.TryGetValue(flowId, out var pausedFlow))
            {
                pausedFlow.timeoutTimer = timer;
            }
        }

        /// <summary>
        /// Auto-forward a flow after timeout
        /// </summary>
        /// <summary>
        /// Auto-forward a flow after timeout
        /// </summary>
        private async Task AutoForwardAsync(string flowId)
        {
            if (!_pausedFlows.TryRemove(flowId, out var pausedFlow))
            {
                return; // Already handled
            }

            pausedFlow.timeoutTimer?.Dispose();

            bool viaUpdate = pausedFlow.update != null;
            bool viaManualResend = false;

            if (viaUpdate)
            {
                // Try manual resend if updated
                var result = await _manualResend.ManualResendAsync(pausedFlow, pausedFlow.update, CancellationToken.None);
                if (result.Success)
                {
                    viaManualResend = true;
                     // Broadcast flow.manual_resend event
                    var info = new ManualResendInfo
                    {
                        remoteHost = new Uri(pausedFlow.flowData.url).Host,
                        bodyLength = result.BodyLength,
                        durationMs = result.DurationMs,
                        statusCode = result.StatusCode
                    };
                    await _wsServer.BroadcastFlowManualResendAsync(flowId, pausedFlow.pid, info);
                }
                else
                {
                     // If failed, we just log and proceed to emit auto_forwarded (client will likely get error or timeout)
                     Console.WriteLine($"[Interceptor] Auto-forward manual resend failed: {result.Error}");
                }
            }

            // Broadcast flow.auto_forwarded event
            await _wsServer.BroadcastFlowActionAsync("flow.auto_forwarded", flowId, viaUpdate, "timeout", viaManualResend);

            Console.WriteLine($"{{\"type\":\"flow.auto_forwarded\",\"flowId\":\"{flowId}\",\"viaUpdate\":{(viaUpdate ? "true" : "false")},\"viaManualResend\":{(viaManualResend ? "true" : "false")},\"timestamp\":\"{DateTime.UtcNow:O}\",\"reason\":\"timeout\"}}");
        }

        /// <summary>
        /// Get the session for a paused flow (for forwarding)
        /// </summary>
        public SessionEventArgs GetPausedFlowSession(string flowId)
        {
            _pausedFlows.TryGetValue(flowId, out var pausedFlow);
            return pausedFlow?.session;
        }

        /// <summary>
        /// Store flow update modifications for a paused flow
        /// </summary>
        public async Task StoreFlowUpdateAsync(string flowId, FlowUpdate update)
        {
            if (!_pausedFlows.TryGetValue(flowId, out var pausedFlow))
            {
                Console.WriteLine($"[Interceptor] Warning: Flow {flowId} not found for update");
                return;
            }

            pausedFlow.update = update;

            // Calculate update summary for logging
            var headersChanged = update.headers != null ? new List<string>(update.headers.Keys) : new List<string>();
            var bodyLength = update.body?.Length ?? 0;

            // Broadcast flow.updated event
            await _wsServer.BroadcastFlowUpdatedAsync(flowId, update);

            Console.WriteLine($"{{\"type\":\"flow.updated\",\"flowId\":\"{flowId}\",\"pid\":{pausedFlow.pid},\"updateSummary\":{{\"headersChanged\":[{string.Join(",", headersChanged.Select(h => $"\"{h}\""))}],\"bodyLength\":{bodyLength}}},\"timestamp\":\"{DateTime.UtcNow:O}\"}}");
        }

        /// <summary>
        /// Replay a stored flow (used by FlowApi)
        /// </summary>
        public async Task ReplayFlowAsync(string originalFlowId, string method, string url, Dictionary<string, string> headers, string? body)
        {
            var replayFlowId = Guid.NewGuid().ToString();
            
            Console.WriteLine($"{{\"type\":\"flow.replay.started\",\"flowId\":\"{replayFlowId}\",\"originalFlowId\":\"{originalFlowId}\",\"method\":\"{method}\",\"url\":\"{url}\",\"timestamp\":\"{DateTime.UtcNow:O}\"}}");

            try
            {
                var (result, _) = await _manualResend.SendRequestAsync(method, url, headers, body, CancellationToken.None);
                
                if (result.Success)
                {
                    Console.WriteLine($"{{\"type\":\"flow.replay.completed\",\"flowId\":\"{replayFlowId}\",\"originalFlowId\":\"{originalFlowId}\",\"statusCode\":{result.StatusCode},\"durationMs\":{result.DurationMs},\"bodyLength\":{result.BodyLength},\"timestamp\":\"{DateTime.UtcNow:O}\"}}");
                }
                else
                {
                    Console.WriteLine($"{{\"type\":\"flow.replay.failed\",\"flowId\":\"{replayFlowId}\",\"originalFlowId\":\"{originalFlowId}\",\"error\":\"{result.Error}\",\"timestamp\":\"{DateTime.UtcNow:O}\"}}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{{\"type\":\"flow.replay.failed\",\"flowId\":\"{replayFlowId}\",\"originalFlowId\":\"{originalFlowId}\",\"error\":\"{ex.Message}\",\"timestamp\":\"{DateTime.UtcNow:O}\"}}");
                throw;
            }
        }
    }

    /// <summary>
    /// Represents a paused flow
    /// </summary>
    public class PausedFlow
    {
        public string flowId { get; set; }
        public int? pid { get; set; }
        public SessionEventArgs session { get; set; }
        public SocketGhostFlowData flowData { get; set; }
        public DateTime receivedAt { get; set; }
        public Timer timeoutTimer { get; set; }
        public FlowUpdate update { get; set; }  // NEW: Store flow modifications
    }
}
