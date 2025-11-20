using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.Http;
using Newtonsoft.Json;

namespace SocketGhost.Core
{
    public class SocketGhostProxyServer
    {
        private readonly Titanium.Web.Proxy.ProxyServer _proxyServer;
        private readonly WebSocketServer _wsServer;
        private readonly PidResolver _pidResolver;
        private readonly ScriptEngineManager _scriptManager;
        private readonly InterceptorManager _interceptorManager;
        private readonly FlowStorageService _flowStorage;

        public SocketGhostProxyServer(WebSocketServer wsServer, InterceptorManager interceptorManager, PidResolver pidResolver, ScriptEngineManager scriptManager, FlowStorageService flowStorage)
        {
            _wsServer = wsServer;
            _interceptorManager = interceptorManager;
            _pidResolver = pidResolver;
            _scriptManager = scriptManager;
            _scriptManager = scriptManager;
            _flowStorage = flowStorage;
            _proxyServer = new Titanium.Web.Proxy.ProxyServer();
        }

        public void Start()
        {
            _proxyServer.CertificateManager.CreateRootCertificate();
            _proxyServer.CertificateManager.TrustRootCertificate(true);

            _proxyServer.BeforeRequest += OnRequest;
            _proxyServer.BeforeResponse += OnResponse;

            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Parse("127.0.0.1"), 8080, true);
            
            // Enable request/response body reading for both HTTP and HTTPS
            explicitEndPoint.BeforeTunnelConnectRequest += (sender, e) =>
            {
                Console.WriteLine($"[DEBUG] TunnelConnect: {e.HttpClient.Request.RequestUri}");
                return Task.CompletedTask;
            };
            
            _proxyServer.AddEndPoint(explicitEndPoint);
            _proxyServer.Start();

            Console.WriteLine("Proxy listening on 127.0.0.1:8080");
            Console.WriteLine("Root certificate generated. Please install it if prompted or check the console output for location.");
        }

        private async Task OnRequest(object sender, SessionEventArgs e)
        {
            var flowId = Guid.NewGuid().ToString();
            Console.WriteLine($"[DEBUG] OnRequest called: {e.HttpClient.Request.Method} {e.HttpClient.Request.Url}");
            
            string bodyPreview = "";
            if (e.HttpClient.Request.HasBody)
            {
                bodyPreview = await e.GetRequestBodyAsString();
            }

            // Resolve PID
            IPEndPoint clientEndPoint = e.ClientRemoteEndPoint;
            IPEndPoint proxyEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);
            int? pid = await _pidResolver.ResolvePidAsync(clientEndPoint, proxyEndPoint);

            var flowData = new SocketGhostFlowData
            {
                flowId = flowId,
                pid = pid,
                method = e.HttpClient.Request.Method,
                url = e.HttpClient.Request.Url,
                headers = e.HttpClient.Request.Headers.ToDictionary(h => h.Name, h => h.Value),
                bodyPreview = bodyPreview
            };

            e.UserData = flowData;

            // Run scripts on request
            bool scriptModified = await _scriptManager.RunOnRequest(flowData);
            if (scriptModified)
            {
                // Apply changes to request
                // Headers
                e.HttpClient.Request.Headers.Clear();
                foreach (var h in flowData.headers)
                {
                    e.HttpClient.Request.Headers.AddHeader(h.Key, h.Value);
                }
                
                // Body
                if (flowData.bodyPreview != bodyPreview)
                {
                    e.SetRequestBodyString(flowData.bodyPreview);
                }
            }

            // Check if should pause flow
            if (_interceptorManager != null && await _interceptorManager.TryPauseFlowAsync(flowId, pid, e, flowData))
            {
                // Flow paused - don't proceed with normal flow
                return;
            }

            // Normal flow - proceed
            var flowEvent = new SocketGhostFlowEvent
            {
                type = "flow.new",
                flow = flowData
            };

            // Log to console
            Console.WriteLine(JsonConvert.SerializeObject(flowEvent));

            // Broadcast to WebSocket
            await _wsServer.BroadcastAsync(flowEvent);
        }

        private async Task OnResponse(object sender, SessionEventArgs e)
        {
            Console.WriteLine($"[DEBUG] OnResponse called: status {e.HttpClient.Response.StatusCode}");
            if (e.UserData is SocketGhostFlowData flowData)
            {
                Console.WriteLine($"[DEBUG] OnResponse: flowData found, flowId={flowData.flowId}");
                // Update flowData with response info
                flowData.responseStatusCode = e.HttpClient.Response.StatusCode;
                flowData.responseHeaders = e.HttpClient.Response.Headers.ToDictionary(h => h.Name, h => h.Value);
                
                string responseBody = "";
                if (e.HttpClient.Response.HasBody)
                {
                    responseBody = await e.GetResponseBodyAsString();
                }

                // Run scripts on response
                bool scriptModified = await _scriptManager.RunOnResponse(flowData);
                if (scriptModified)
                {
                    // Apply changes to response
                    // Headers
                    e.HttpClient.Response.Headers.Clear();
                    foreach (var h in flowData.responseHeaders)
                    {
                        e.HttpClient.Response.Headers.AddHeader(h.Key, h.Value);
                    }

                    // Body
                    if (!string.IsNullOrEmpty(flowData.responseBody))
                    {
                        e.SetResponseBodyString(flowData.responseBody);
                        responseBody = flowData.responseBody;
                    }
                }
                
                // Store flow
                var storedFlow = new StoredFlow
                {
                    Id = flowData.flowId,
                    CapturedAt = DateTime.UtcNow,
                    Pid = flowData.pid ?? 0,
                    Method = flowData.method,
                    Url = flowData.url,
                    ViaUpdate = false, // Normal flow
                    ViaManualResend = false,
                    ScriptApplied = flowData.scriptApplied ?? new List<string>(),
                    Request = new StoredFlowRequest
                    {
                        Headers = flowData.headers,
                        BodyPreview = flowData.bodyPreview
                    },
                    Response = new StoredFlowResponse
                    {
                        StatusCode = flowData.responseStatusCode,
                        Headers = flowData.responseHeaders,
                        BodyPreview = responseBody
                    },
                    SizeBytes = (flowData.bodyPreview?.Length ?? 0) + (responseBody?.Length ?? 0)
                };

                Console.WriteLine($"[DEBUG] Storing flow {storedFlow.Id}: {storedFlow.Method} {storedFlow.Url}");
                await _flowStorage.StoreFlowAsync(storedFlow);
                Console.WriteLine($"[DEBUG] Flow {storedFlow.Id} stored successfully");
            }
            else
            {
                Console.WriteLine("[DEBUG] OnResponse: e.UserData is not SocketGhostFlowData");
            }
        }

        public void Stop()
        {
            _proxyServer.Stop();
        }
    }
}
