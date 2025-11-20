using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SocketGhost.Core
{
    /// <summary>
    /// Manages mitmproxy adapter subprocess and handles flow ingestion
    /// </summary>
    public class MitmAdapterManager
    {
        private readonly WebSocketServer _wsServer;
        private readonly string _pythonPath;
        private readonly string _adapterScript;
        private readonly int _mitmPort;
        private readonly int _ingestPort;
        private Process? _mitmProcess;
        private HttpListener? _ingestListener;
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        public MitmAdapterManager(WebSocketServer wsServer, MitmAdapterConfig config)
        {
            _wsServer = wsServer;
            _pythonPath = config.PythonPath ?? "python3";
            _mitmPort = config.MitmPort;
            _ingestPort = config.IngestPort;

            // Resolve adapter script path
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            _adapterScript = Path.Combine(basePath, "mitm_adapter", "adapter_addon.py");

            if (!File.Exists(_adapterScript))
            {
                Console.WriteLine($"[MitmAdapter] Warning: Adapter script not found at {_adapterScript}");
            }
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();
            _isRunning = true;

            Console.WriteLine("[MitmAdapter] Starting mitmproxy adapter mode...");

            // Start ingestion HTTP listener
            await StartIngestionListenerAsync();

            // Start mitmdump subprocess
            StartMitmdumpProcess();

            Console.WriteLine($"[MitmAdapter] Adapter ready. Proxy on port {_mitmPort}, ingestion on port {_ingestPort}");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            Console.WriteLine("[MitmAdapter] Stopping adapter...");
            _isRunning = false;
            _cts?.Cancel();

            // Stop mitmdump
            if (_mitmProcess != null && !_mitmProcess.HasExited)
            {
                try
                {
                    _mitmProcess.Kill(true);
                    _mitmProcess.WaitForExit(5000);
                }
                catch { }
                _mitmProcess?.Dispose();
            }

            // Stop ingestion listener
            _ingestListener?.Stop();
            _ingestListener?.Close();

            Console.WriteLine("[MitmAdapter] Stopped");
        }

        private async Task StartIngestionListenerAsync()
        {
            _ingestListener = new HttpListener();
            _ingestListener.Prefixes.Add($"http://127.0.0.1:{_ingestPort}/ingest/");
            _ingestListener.Prefixes.Add($"http://127.0.0.1:{_ingestPort}/");
            _ingestListener.Start();

            Console.WriteLine($"[MitmAdapter] Ingestion endpoint listening on http://127.0.0.1:{_ingestPort}/ingest");

            // Start accepting connections
            _ = Task.Run(async () => await AcceptIngestRequestsAsync(), _cts!.Token);
        }

        private async Task AcceptIngestRequestsAsync()
        {
            while (_isRunning && !_cts!.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _ingestListener!.GetContextAsync();
                    _ = HandleIngestRequestAsync(context);
                }
                catch (HttpListenerException)
                {
                    // Listener stopped
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MitmAdapter] Ingestion listener error: {ex.Message}");
                }
            }
        }

        private async Task HandleIngestRequestAsync(HttpListenerContext context)
        {
            try
            {
                // CORS headers
                context.Response.AddHeader("Access-Control-Allow-Origin", "*");
                context.Response.AddHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
                context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

                if (context.Request.HttpMethod == "OPTIONS")
                {
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }

                if (context.Request.HttpMethod != "POST")
                {
                    context.Response.StatusCode = 405;
                    context.Response.Close();
                    return;
                }

                // Read JSON body
                string json;
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    json = await reader.ReadToEndAsync();
                }

                // Parse and process flow message
                var message = JObject.Parse(json);
                await ProcessFlowMessageAsync(message);

                // Respond
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var responseBytes = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MitmAdapter] Error handling ingest request: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { }
            }
        }

        private async Task ProcessFlowMessageAsync(JObject message)
        {
            try
            {
                var type = message["type"]?.ToString();
                if (type != "flow.new")
                {
                    Console.WriteLine($"[MitmAdapter] Ignoring message type: {type}");
                    return;
                }

                // Extract flow data
                var flow = message["flow"];
                if (flow == null) return;

                Console.WriteLine($"[MitmAdapter] Flow ingested: {flow["method"]} {flow["url"]}");

                // Broadcast to WebSocket clients (UI, etc.)
                // Send the raw JSON string directly since it's already in the correct format
                await BroadcastJsonAsync(message.ToString(Formatting.None));

                // Also log to console for debugging
                Console.WriteLine(message.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MitmAdapter] Error processing flow message: {ex.Message}");
            }
        }

        private async Task BroadcastJsonAsync(string json)
        {
            try
            {
                // Access WebSocket client directly to send raw JSON
                // This replicates what BroadcastAsync does but with raw JSON
                var clientField = typeof(WebSocketServer).GetField("_client", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (clientField != null)
                {
                    var client = clientField.GetValue(_wsServer) as System.Net.WebSockets.WebSocket;
                    if (client != null && client.State == System.Net.WebSockets.WebSocketState.Open)
                    {
                        var buffer = Encoding.UTF8.GetBytes(json);
                        await client.SendAsync(new ArraySegment<byte>(buffer), 
                            System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MitmAdapter] Error broadcasting JSON: {ex.Message}");
            }
        }

        private void StartMitmdumpProcess()
        {
            try
            {
                // Check if mitmdump is available
                if (!IsMitmdumpAvailable())
                {
                    Console.WriteLine("[MitmAdapter] WARNING: mitmdump not found. Please install mitmproxy:");
                    Console.WriteLine("[MitmAdapter]   pip install mitmproxy");
                    Console.WriteLine("[MitmAdapter] Adapter will not start. Core will only provide ingestion endpoint.");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "mitmdump",
                    Arguments = $"-s \"{_adapterScript}\" --listen-host 127.0.0.1 --listen-port {_mitmPort} --set flow_detail=2",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Environment =
                    {
                        ["SOCKETGHOST_INGEST_URL"] = $"http://127.0.0.1:{_ingestPort}/ingest"
                    }
                };

                _mitmProcess = new Process { StartInfo = startInfo };

                // Capture output for logging
                _mitmProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine($"[mitmdump] {e.Data}");
                };

                _mitmProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine($"[mitmdump] ERROR: {e.Data}");
                };

                _mitmProcess.Start();
                _mitmProcess.BeginOutputReadLine();
                _mitmProcess.BeginErrorReadLine();

                Console.WriteLine($"[MitmAdapter] mitmdump started (PID: {_mitmProcess.Id})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MitmAdapter] Failed to start mitmdump: {ex.Message}");
                Console.WriteLine("[MitmAdapter] You can manually start mitmdump:");
                Console.WriteLine($"[MitmAdapter]   cd mitm_adapter && ./start_mitm.sh");
            }
        }

        private bool IsMitmdumpAvailable()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "mitmdump",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });

                process?.WaitForExit(3000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public class MitmAdapterConfig
    {
        public string? PythonPath { get; set; } = "python3";
        public int MitmPort { get; set; } = 8081;
        public int IngestPort { get; set; } = 9001;
    }
}
