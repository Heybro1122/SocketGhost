using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SocketGhost.Core
{
    public class WebSocketServer
    {
        private HttpListener _listener;
        private WebSocket _client;
        private InterceptorManager _interceptorManager;

        public void SetInterceptorManager(InterceptorManager interceptorManager)
        {
            _interceptorManager = interceptorManager;
        }

        public async Task StartAsync(string url)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(url);
            _listener.Start();
            Console.WriteLine($"WebSocket listening on {url}");

            while (true)
            {
                var context = await _listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    ProcessRequest(context);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        private async void ProcessRequest(HttpListenerContext context)
        {
            WebSocketContext wsContext = null;
            try
            {
                wsContext = await context.AcceptWebSocketAsync(null);
                _client = wsContext.WebSocket;
                Console.WriteLine("WebSocket client connected");
                
                // Listen for incoming messages
                var buffer = new byte[4096];
                while (_client.State == WebSocketState.Open)
                {
                    var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessControlMessageAsync(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
            }
            finally
            {
                if (_client != null)
                    _client.Dispose();
            }
        }

        /// <summary>
        /// Process control messages from UI
        /// </summary>
        private async Task ProcessControlMessageAsync(string message)
        {
            try
            {
                var msg = JsonConvert.DeserializeObject<ControlMessage>(message);
                
                if (msg?.type == "interceptor.set")
                {
                    if (msg.enabled)
                    {
                        _interceptorManager?.AddInterceptPid(msg.pid);
                    }
                    else
                    {
                        _interceptorManager?.RemoveInterceptPid(msg.pid);
                    }
                }
                else if (msg?.type == "flow.action")
                {
                    if (msg.action == "forward")
                    {
                        await _interceptorManager?.ForwardFlowAsync(msg.flowId);
                    }
                    else if (msg.action == "drop")
                    {
                        await _interceptorManager?.DropFlowAsync(msg.flowId);
                    }
                }
                else if (msg?.type == "flow.update")
                {
                    // Handle flow.update message
                    var update = new FlowUpdate
                    {
                        headers = msg.update?.headers,
                        body = msg.update?.body
                    };
                    await _interceptorManager?.StoreFlowUpdateAsync(msg.flowId, update);
                }
                else
                {
                    Console.WriteLine($"[WebSocket] Unknown control message type: {msg?.type}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] Error processing control message: {ex.Message}");
            }
        }

        public async Task BroadcastAsync(SocketGhostFlowEvent flowEvent)
        {
            if (_client != null && _client.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(flowEvent);
                var buffer = Encoding.UTF8.GetBytes(json);
                await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        /// <summary>
        /// Broadcast flow.paused event
        /// </summary>
        public async Task BroadcastFlowPausedAsync(PausedFlow pausedFlow)
        {
            var evt = new
            {
                v = "0.1",
                type = "flow.paused",
                flow = new
                {
                    pausedFlow.flowId,
                    pausedFlow.pid,
                    pausedFlow.flowData.method,
                    pausedFlow.flowData.url,
                    pausedFlow.flowData.headers,
                    pausedFlow.flowData.bodyPreview,
                    receivedAt = pausedFlow.receivedAt.ToString("O")
                }
            };

            if (_client != null && _client.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(evt);
                var buffer = Encoding.UTF8.GetBytes(json);
                await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        /// <summary>
        /// Broadcast flow action events (forwarded, dropped, auto_forwarded)
        /// </summary>
        /// <summary>
        /// Broadcast flow action events (forwarded, dropped, auto_forwarded)
        /// </summary>
        public async Task BroadcastFlowActionAsync(string eventType, string flowId, bool viaUpdate = false, string reason = null, bool viaManualResend = false)
        {
            var evt = new
            {
                v = "0.1",
                type = eventType,
                flowId,
                viaUpdate,
                viaManualResend,
                timestamp = DateTime.UtcNow.ToString("O"),
                reason
            };

            if (_client != null && _client.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(evt);
                var buffer = Encoding.UTF8.GetBytes(json);
                await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        /// <summary>
        /// Broadcast flow.manual_resend event
        /// </summary>
        public async Task BroadcastFlowManualResendAsync(string flowId, int? pid, ManualResendInfo info)
        {
            var evt = new
            {
                v = "0.1",
                type = "flow.manual_resend",
                flowId,
                pid,
                info,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            if (_client != null && _client.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(evt);
                var buffer = Encoding.UTF8.GetBytes(json);
                await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        /// <summary>
        /// Broadcast flow.error event
        /// </summary>
        public async Task BroadcastFlowErrorAsync(string flowId, string error)
        {
            var evt = new
            {
                v = "0.1",
                type = "flow.error",
                flowId,
                error,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            if (_client != null && _client.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(evt);
                var buffer = Encoding.UTF8.GetBytes(json);
                await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public async Task BroadcastScriptRunAsync(string scriptId, string flowId, int? pid, long durationMs, bool modified, string error)
        {
            var evt = new
            {
                v = "0.1",
                type = "script.run",
                scriptId,
                flowId,
                pid,
                durationMs,
                modified,
                error,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            if (_client != null && _client.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(evt);
                var buffer = Encoding.UTF8.GetBytes(json);
                await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public async Task BroadcastFlowScriptAppliedAsync(string flowId, string scriptId)
        {
            var evt = new
            {
                v = "0.1",
                type = "flow.script_applied",
                flowId,
                scriptId,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            if (_client != null && _client.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(evt);
                var buffer = Encoding.UTF8.GetBytes(json);
                await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        /// <summary>
        /// Broadcast flow.updated event
        /// </summary>
        public async Task BroadcastFlowUpdatedAsync(string flowId, FlowUpdate update)
        {
            var evt = new
            {
                v = "0.1",
                type = "flow.updated",
                flowId,
                update = new
                {
                    headers = update.headers,
                    body = update.body
                },
                timestamp = DateTime.UtcNow.ToString("O")
            };

            if (_client != null && _client.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(evt);
                var buffer = Encoding.UTF8.GetBytes(json);
                await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Control message from UI
    /// </summary>
    public class ControlMessage
    {
        public string v { get; set; }
        public string type { get; set; }
        
        // interceptor.set fields
        public int pid { get; set; }
        public bool enabled { get; set; }
        
        // flow.action fields
        public string action { get; set; }
        public string flowId { get; set; }
        
        // flow.update fields
        public UpdatePayload update { get; set; }
    }

    /// <summary>
    /// Update payload for flow.update messages
    /// </summary>
    public class UpdatePayload
    {
        public Dictionary<string, string> headers { get; set; }
        public string body { get; set; }
    }
}
