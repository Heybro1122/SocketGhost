using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace SocketGhost.Core
{
    public class ScriptApi
    {
        private readonly ScriptEngineManager _scriptManager;
        private HttpListener _listener;

        public ScriptApi(ScriptEngineManager scriptManager)
        {
            _scriptManager = scriptManager;
        }

        public async Task StartAsync(string url)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(url);
            _listener.Start();
            Console.WriteLine($"Scripts API listening on {url}");

            while (true)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ScriptApi error: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                if (context.Request.HttpMethod == "OPTIONS")
                {
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }

                var path = context.Request.Url.AbsolutePath;
                var method = context.Request.HttpMethod;

                if (path == "/scripts" && method == "GET")
                {
                    var scripts = _scriptManager.GetAllScripts();
                    await SendJsonAsync(context, scripts);
                }
                else if (path == "/scripts" && method == "POST")
                {
                    var json = await ReadBodyAsync(context);
                    var script = JsonConvert.DeserializeObject<ScriptDefinition>(json);
                    script.Id = Guid.NewGuid().ToString();
                    script.CreatedAt = DateTime.UtcNow;
                    script.UpdatedAt = DateTime.UtcNow;
                    await _scriptManager.SaveScriptAsync(script);
                    await SendJsonAsync(context, script);
                }
                else if (path.StartsWith("/scripts/") && method == "GET")
                {
                    var id = path.Substring("/scripts/".Length);
                    var script = _scriptManager.GetScript(id);
                    if (script != null) await SendJsonAsync(context, script);
                    else context.Response.StatusCode = 404;
                }
                else if (path.StartsWith("/scripts/") && method == "PUT")
                {
                    var id = path.Substring("/scripts/".Length);
                    var json = await ReadBodyAsync(context);
                    var update = JsonConvert.DeserializeObject<ScriptDefinition>(json);
                    var existing = _scriptManager.GetScript(id);
                    if (existing != null)
                    {
                        existing.Name = update.Name;
                        existing.Code = update.Code;
                        existing.Enabled = update.Enabled;
                        existing.UpdatedAt = DateTime.UtcNow;
                        await _scriptManager.SaveScriptAsync(existing);
                        await SendJsonAsync(context, existing);
                    }
                    else context.Response.StatusCode = 404;
                }
                else if (path.StartsWith("/scripts/") && method == "DELETE")
                {
                    var id = path.Substring("/scripts/".Length);
                    await _scriptManager.DeleteScriptAsync(id);
                    context.Response.StatusCode = 204;
                }
                else if (path.StartsWith("/scripts/") && path.EndsWith("/enable") && method == "POST")
                {
                    var id = path.Substring("/scripts/".Length).Replace("/enable", "");
                    var script = _scriptManager.GetScript(id);
                    if (script != null)
                    {
                        script.Enabled = true;
                        script.UpdatedAt = DateTime.UtcNow;
                        await _scriptManager.SaveScriptAsync(script);
                        await SendJsonAsync(context, script);
                    }
                    else context.Response.StatusCode = 404;
                }
                else if (path.StartsWith("/scripts/") && path.EndsWith("/disable") && method == "POST")
                {
                    var id = path.Substring("/scripts/".Length).Replace("/disable", "");
                    var script = _scriptManager.GetScript(id);
                    if (script != null)
                    {
                        script.Enabled = false;
                        script.UpdatedAt = DateTime.UtcNow;
                        await _scriptManager.SaveScriptAsync(script);
                        await SendJsonAsync(context, script);
                    }
                    else context.Response.StatusCode = 404;
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                var error = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { error = ex.Message }));
                context.Response.OutputStream.Write(error, 0, error.Length);
            }
            finally
            {
                context.Response.Close();
            }
        }

        private async Task SendJsonAsync(HttpListenerContext context, object data)
        {
            var json = JsonConvert.SerializeObject(data);
            var buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private async Task<string> ReadBodyAsync(HttpListenerContext context)
        {
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            return await reader.ReadToEndAsync();
        }
    }
}
