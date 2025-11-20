using Jint;
using Jint.Native;
using Jint.Runtime;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Text;

namespace SocketGhost.Core
{
    public class ScriptEngineManager
    {
        private readonly string _scriptsDir;
        private readonly ConcurrentDictionary<string, ScriptDefinition> _scripts = new();
        private readonly WebSocketServer _wsServer;
        private const int DefaultTimeoutMs = 50;

        public ScriptEngineManager(WebSocketServer wsServer)
        {
            _wsServer = wsServer;
            _scriptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts");
            Directory.CreateDirectory(_scriptsDir);
        }

        public async Task LoadScriptsAsync()
        {
            try
            {
                var files = Directory.GetFiles(_scriptsDir, "*.json");
                foreach (var file in files)
                {
                    if (Path.GetFileName(file) == "index.json") continue;

                    var json = await File.ReadAllTextAsync(file);
                    var script = JsonConvert.DeserializeObject<ScriptDefinition>(json);
                    if (script != null)
                    {
                        _scripts[script.Id] = script;
                    }
                }
                Console.WriteLine($"[ScriptEngine] Loaded {_scripts.Count} scripts");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptEngine] Error loading scripts: {ex.Message}");
            }
        }

        public List<ScriptDefinition> GetAllScripts() => _scripts.Values.ToList();

        public ScriptDefinition GetScript(string id) => _scripts.TryGetValue(id, out var s) ? s : null;

        public async Task SaveScriptAsync(ScriptDefinition script)
        {
            _scripts[script.Id] = script;
            var path = Path.Combine(_scriptsDir, $"{script.Id}.json");
            await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(script, Formatting.Indented));
        }

        public async Task DeleteScriptAsync(string id)
        {
            if (_scripts.TryRemove(id, out _))
            {
                var path = Path.Combine(_scriptsDir, $"{id}.json");
                if (File.Exists(path)) File.Delete(path);
            }
        }

        public async Task<bool> RunOnRequest(SocketGhostFlowData flow)
        {
            bool modified = false;
            foreach (var script in _scripts.Values.Where(s => s.Enabled))
            {
                if (await RunScriptAsync(script, flow, "onRequest"))
                {
                    modified = true;
                }
            }
            return modified;
        }

        public async Task<bool> RunOnResponse(SocketGhostFlowData flow)
        {
            bool modified = false;
            foreach (var script in _scripts.Values.Where(s => s.Enabled))
            {
                if (await RunScriptAsync(script, flow, "onResponse"))
                {
                    modified = true;
                }
            }
            return modified;
        }

        private async Task<bool> RunScriptAsync(ScriptDefinition script, SocketGhostFlowData flow, string functionName)
        {
            var engine = new Engine(options => options
                .LimitMemory(4_000_000) // ~4MB limit per script run (soft limit)
                .TimeoutInterval(TimeSpan.FromMilliseconds(DefaultTimeoutMs))
                .Strict()
            );

            bool modified = false;
            string error = null;
            long durationMs = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Setup API surface
                // flow object (read-only mostly, use setters for modification to track changes)
                var flowObj = new
                {
                    request = new
                    {
                        method = flow.method,
                        url = flow.url,
                        headers = flow.headers,
                        body = flow.bodyPreview // Note: this might be truncated or just preview
                    },
                    response = flow.responseStatusCode > 0 ? new
                    {
                        statusCode = flow.responseStatusCode,
                        headers = flow.responseHeaders,
                        body = "" // Response body not fully available in flow data yet usually, need to handle this
                    } : null
                };

                // We need to expose the raw body if possible, but flow.bodyPreview is what we have.
                // For MVP, we assume bodyPreview IS the body for small requests.
                
                engine.SetValue("flow", flowObj);
                
                // Helper functions
                engine.SetValue("setRequestBody", new Action<string>(body => {
                    // In a real implementation, we would update the actual request
                    // For MVP, we'll mark as modified and store the new body in a temporary way
                    // But wait, we need to actually modify the flow object passed in.
                    // Since SocketGhostFlowData is a class, we can modify it directly?
                    // But flow.bodyPreview is a string (immutable).
                    // We need to store the modification somewhere.
                    // Let's add a 'ModifiedRequestBody' field to SocketGhostFlowData?
                    // Or just update bodyPreview and hope the caller uses it?
                    // The caller (InterceptorManager) needs to know.
                    // We'll return 'modified' bool and maybe update the flow object properties.
                    
                    // Actually, we should update the flow object properties directly if possible, 
                    // or better, use a wrapper that writes back.
                    
                    // For this MVP, let's assume we update the flow object directly.
                    // But we need to handle the fact that we are in a loop.
                    
                    // Let's use a dictionary to track changes?
                    // Or just update the flow object.
                    
                    // flow.bodyPreview = body; // This updates the local object
                    // We need to signal to the caller.
                    
                    // Let's add a property to SocketGhostFlowData for 'PendingUpdates'?
                    // Or just modify the fields.
                    
                    // Wait, SocketGhostFlowData is just a DTO.
                    // The actual modification needs to happen on the SessionEventArgs in InterceptorManager.
                    // But InterceptorManager passes SocketGhostFlowData.
                    
                    // We need to update SocketGhostFlowData AND somehow tell InterceptorManager to apply it.
                    // If we update SocketGhostFlowData, InterceptorManager can read it back?
                    // Yes, it's a reference type.
                    
                    flow.bodyPreview = body; 
                    modified = true;
                }));

                engine.SetValue("setResponseBody", new Action<string>(body => {
                    flow.responseBody = body;
                    modified = true;
                }));

                engine.SetValue("setResponseHeader", new Action<string, string>((name, value) => {
                    if (flow.responseHeaders == null) flow.responseHeaders = new Dictionary<string, string>();
                    flow.responseHeaders[name] = value;
                    modified = true;
                }));
                
                engine.SetValue("setRequestHeader", new Action<string, string>((name, value) => {
                    if (flow.headers == null) flow.headers = new Dictionary<string, string>();
                    flow.headers[name] = value;
                    modified = true;
                }));

                engine.Execute(script.Code);
                
                // Check if function exists
                var fn = engine.GetValue(functionName);
                if (fn != JsValue.Undefined)
                {
                    engine.Invoke(functionName, engine.GetValue("flow"));
                }
            }
            catch (TimeoutException)
            {
                error = "Script execution timed out";
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                sw.Stop();
                durationMs = sw.ElapsedMilliseconds;
            }

            // Broadcast event
            await _wsServer.BroadcastScriptRunAsync(script.Id, flow.flowId, flow.pid, durationMs, modified, error);
            
            if (modified)
            {
                await _wsServer.BroadcastFlowScriptAppliedAsync(flow.flowId, script.Id);
            }

            return modified;
        }
    }

    public class ScriptDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Code { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
