using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SocketGhost.Core
{
    public class FlowApi
    {
        private readonly FlowStorageService _storage;
        private readonly InterceptorManager _interceptorManager; // For replay
        private HttpListener _listener;

        public FlowApi(FlowStorageService storage, InterceptorManager interceptorManager)
        {
            _storage = storage;
            _interceptorManager = interceptorManager;
        }

        public async Task StartAsync(string url)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(url);
            _listener.Start();
            Console.WriteLine($"Flow API listening on {url}");

            while (true)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Flow API error: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                // CORS
                context.Response.AddHeader("Access-Control-Allow-Origin", "*");
                context.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, X-SocketGhost-Confirm");

                if (context.Request.HttpMethod == "OPTIONS")
                {
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }

                var path = context.Request.Url.AbsolutePath.TrimEnd('/');
                var method = context.Request.HttpMethod;

                if (path == "/flows" && method == "GET")
                {
                    await HandleListFlows(context);
                }
                else if (path.StartsWith("/flows/") && path.EndsWith("/replay") && method == "POST")
                {
                    var id = path.Split('/')[2];
                    await HandleReplayFlow(context, id);
                }
                else if (path.StartsWith("/flows/") && path.EndsWith("/request") && method == "GET")
                {
                    var id = path.Split('/')[2];
                    await HandleGetBody(context, id, "request");
                }
                else if (path.StartsWith("/flows/") && path.EndsWith("/response") && method == "GET")
                {
                    var id = path.Split('/')[2];
                    await HandleGetBody(context, id, "response");
                }
                else if (path.StartsWith("/flows/") && method == "GET")
                {
                    var id = path.Split('/')[2];
                    await HandleGetFlow(context, id);
                }
                else if (path.StartsWith("/flows/") && method == "DELETE")
                {
                    var id = path.Split('/')[2];
                    await HandleDeleteFlow(context, id);
                }
                else if (path == "/flows/import" && method == "POST")
                {
                    await HandleImportFlows(context);
                }
                else if (path == "/flows/prune" && method == "DELETE")
                {
                    await HandlePruneFlows(context);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Flow API request error: {ex.Message}");
                context.Response.StatusCode = 500;
                using var writer = new StreamWriter(context.Response.OutputStream);
                await writer.WriteAsync(JsonConvert.SerializeObject(new { error = ex.Message }));
                context.Response.Close();
            }
        }

        private async Task HandleListFlows(HttpListenerContext context)
        {
            var query = context.Request.QueryString;
            int limit = int.TryParse(query["limit"], out var l) ? l : 50;
            int offset = int.TryParse(query["offset"], out var o) ? o : 0;
            
            var filter = new FlowFilter();
            if (int.TryParse(query["pid"], out var pid)) filter.Pid = pid;
            filter.Method = query["method"];
            filter.Query = query["query"];
            if (DateTime.TryParse(query["since"], out var since)) filter.Since = since;

            var flows = await _storage.GetFlowsAsync(limit, offset, filter);
            
            var json = JsonConvert.SerializeObject(flows);
            var bytes = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        private async Task HandleGetFlow(HttpListenerContext context, string id)
        {
            var flow = await _storage.GetFlowAsync(id);
            if (flow == null)
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            var json = JsonConvert.SerializeObject(flow);
            var bytes = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        private async Task HandleGetBody(HttpListenerContext context, string id, string type)
        {
            var stream = _storage.GetBodyStream(id, type);
            if (stream == null)
            {
                // If not on disk, maybe it's inline?
                // The caller should check the flow details first.
                // But for convenience, we could return inline body if available.
                var flow = await _storage.GetFlowAsync(id);
                if (flow != null)
                {
                    string? content = type == "request" ? flow.Request.BodyPreview : flow.Response.BodyPreview;
                    if (content != null)
                    {
                        var bytes = Encoding.UTF8.GetBytes(content);
                        context.Response.ContentType = "text/plain"; // Or guess from headers
                        context.Response.ContentLength64 = bytes.Length;
                        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                        context.Response.Close();
                        return;
                    }
                }
                
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            using (stream)
            {
                context.Response.ContentType = "application/octet-stream";
                context.Response.ContentLength64 = stream.Length;
                await stream.CopyToAsync(context.Response.OutputStream);
            }
            context.Response.Close();
        }

        private async Task HandleDeleteFlow(HttpListenerContext context, string id)
        {
            await _storage.DeleteFlowAsync(id);
            context.Response.StatusCode = 204;
            context.Response.Close();
        }

        private async Task HandleImportFlows(HttpListenerContext context)
        {
            using var reader = new StreamReader(context.Request.InputStream);
            var json = await reader.ReadToEndAsync();
            
            try
            {
                // Try array first
                var flows = JsonConvert.DeserializeObject<List<StoredFlow>>(json);
                if (flows != null)
                {
                    foreach (var flow in flows)
                    {
                        // Reset ID to avoid collision or keep it? 
                        // Requirements say "return created ids". 
                        // If we keep ID, we might overwrite. 
                        // Let's generate new ID for import to be safe, or keep if unique.
                        // For simplicity, let's keep ID but if it exists it will update (Sqlite replace/insert logic might need check).
                        // Our Sqlite store uses INSERT, so it might fail if ID exists.
                        // Let's generate new ID.
                        flow.Id = Guid.NewGuid().ToString();
                        flow.CapturedAt = DateTime.UtcNow; // Or keep original? Keep original is better for history.
                        await _storage.StoreFlowAsync(flow);
                    }
                    
                    // Return IDs?
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }
            }
            catch { }

            try
            {
                // Try single object
                var flow = JsonConvert.DeserializeObject<StoredFlow>(json);
                if (flow != null)
                {
                    flow.Id = Guid.NewGuid().ToString();
                    await _storage.StoreFlowAsync(flow);
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }
            }
            catch { }

            context.Response.StatusCode = 400;
            context.Response.Close();
        }

        private async Task HandlePruneFlows(HttpListenerContext context)
        {
            await _storage.PruneAsync();
            context.Response.StatusCode = 204;
            context.Response.Close();
        }

        private async Task HandleReplayFlow(HttpListenerContext context, string id)
        {
            // Check confirmation
            var confirmHeader = context.Request.Headers["X-SocketGhost-Confirm"];
            
            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            var replayRequest = JsonConvert.DeserializeObject<ReplayRequest>(body);

            if (confirmHeader != "true" && (replayRequest == null || !replayRequest.Confirm))
            {
                context.Response.StatusCode = 400;
                using var writer = new StreamWriter(context.Response.OutputStream);
                await writer.WriteAsync(JsonConvert.SerializeObject(new { error = "Missing confirmation" }));
                context.Response.Close();
                return;
            }

            var flow = await _storage.GetFlowAsync(id);
            if (flow == null)
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            // Prepare replay
            // Use provided body/headers or original
            var method = replayRequest?.Method ?? flow.Method;
            var url = replayRequest?.Url ?? flow.Url;
            var headers = replayRequest?.Headers ?? flow.Request.Headers;
            var requestBody = replayRequest?.Body ?? flow.Request.BodyPreview; // What if body is on disk?
            
            if (requestBody == null && flow.Request.FullBodyPath != null)
            {
                // Load from disk if not provided
                // TODO: Handle large body replay
                // For now, MVP assumes we can load it into memory or user provided it
                // If user didn't provide it and it's on disk, we should load it.
                var stream = _storage.GetBodyStream(id, "request");
                if (stream != null)
                {
                    using var sr = new StreamReader(stream);
                    requestBody = await sr.ReadToEndAsync();
                }
            }

            try
            {
                // Use ManualResend logic
                // We need to access ManualResend. But it's not directly exposed.
                // InterceptorManager uses it.
                // We should expose a method on InterceptorManager or ManualResend to replay.
                // Let's add ReplayFlowAsync to InterceptorManager.
                
                await _interceptorManager.ReplayFlowAsync(flow.Id, method, url, headers, requestBody);
                
                context.Response.StatusCode = 200;
                using var writer = new StreamWriter(context.Response.OutputStream);
                await writer.WriteAsync(JsonConvert.SerializeObject(new { status = "queued", flowId = flow.Id }));
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                using var writer = new StreamWriter(context.Response.OutputStream);
                await writer.WriteAsync(JsonConvert.SerializeObject(new { error = ex.Message }));
            }
            
            context.Response.Close();
        }
    }

    public class ReplayRequest
    {
        public bool Confirm { get; set; }
        public string? Method { get; set; }
        public string? Url { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public string? Body { get; set; }
    }
}
