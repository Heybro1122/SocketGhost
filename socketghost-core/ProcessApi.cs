using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SocketGhost.Core
{
    public class ProcessApi
    {
        private HttpListener _listener;

        public async Task StartAsync(string url)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(url);
            _listener.Start();
            Console.WriteLine($"Processes API listening on {url}");

            while (true)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    ProcessRequest(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ProcessApi error: {ex.Message}");
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                // Handle CORS preflight
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    context.Response.AddHeader("Access-Control-Allow-Origin", "*");
                    context.Response.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
                    context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }

                if (context.Request.Url.AbsolutePath == "/processes")
                {
                    var processes = Process.GetProcesses().Select(p => new
                    {
                        pid = p.Id,
                        exe = p.ProcessName,
                        // Cmdline is hard to get without elevation/WMI, skipping for MVP speed
                        // or could add a best-effort attempt later.
                        cmdline = "" 
                    }).ToList();

                    var json = JsonConvert.SerializeObject(processes);
                    var buffer = Encoding.UTF8.GetBytes(json);

                    context.Response.ContentType = "application/json";
                    context.Response.AddHeader("Access-Control-Allow-Origin", "*");
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
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
    }
}
