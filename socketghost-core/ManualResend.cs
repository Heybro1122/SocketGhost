using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;

namespace SocketGhost.Core
{
    public class ManualResendResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public long BodyLength { get; set; }
        public double DurationMs { get; set; }
        public string Error { get; set; } = "";
    }

    public class ManualResend
    {
        private static readonly HttpClient _httpClient;
        
        private static readonly HashSet<string> _hopByHopHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
            "TE", "Trailers", "Transfer-Encoding", "Upgrade"
        };

        static ManualResend()
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false
            };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            Console.WriteLine($"[ManualResend] Ready; timeout={_httpClient.Timeout.TotalSeconds}s");
        }

        public async Task<ManualResendResult> ManualResendAsync(PausedFlow pausedFlow, FlowUpdate update, CancellationToken ct)
        {
            var method = pausedFlow.flowData.method;
            var url = pausedFlow.flowData.url;
            var headers = new Dictionary<string, string>(pausedFlow.flowData.headers ?? new Dictionary<string, string>());
            
            if (update?.headers != null)
            {
                foreach (var kvp in update.headers)
                {
                    headers[kvp.Key] = kvp.Value;
                }
            }

            headers["X-SocketGhost-Forwarded"] = "true";
            if (!string.IsNullOrEmpty(pausedFlow.flowId))
            {
                headers["X-SocketGhost-Original-Flow"] = pausedFlow.flowId;
            }

            var body = update?.body ?? pausedFlow.flowData.bodyPreview;

            var (result, responseMessage) = await SendRequestAsync(method, url, headers, body, ct);

            if (result.Success && responseMessage != null)
            {
                try
                {
                    var responseBytes = await responseMessage.Content.ReadAsByteArrayAsync();
                    var response = new Response(responseBytes);
                    response.StatusCode = (int)responseMessage.StatusCode;
                    response.HttpVersion = responseMessage.Version;

                    foreach (var header in responseMessage.Headers)
                    {
                        response.Headers.AddHeader(header.Key, header.Value.First());
                    }
                    foreach (var header in responseMessage.Content.Headers)
                    {
                        response.Headers.AddHeader(header.Key, header.Value.First());
                    }

                    pausedFlow.session.Respond(response);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = $"Error piping response: {ex.Message}";
                }
            }

            return result;
        }

        public async Task<(ManualResendResult, HttpResponseMessage?)> SendRequestAsync(string method, string url, Dictionary<string, string> headers, string? body, CancellationToken ct)
        {
            var start = DateTime.UtcNow;
            var result = new ManualResendResult();
            HttpResponseMessage? responseMessage = null;

            try
            {
                var requestMessage = new HttpRequestMessage
                {
                    Method = new HttpMethod(method),
                    RequestUri = new Uri(url),
                    Version = new Version(1, 1)
                };

                foreach (var kvp in headers)
                {
                    if (_hopByHopHeaders.Contains(kvp.Key)) continue;
                    if (kvp.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        requestMessage.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(body))
                {
                    requestMessage.Content = new StringContent(body, Encoding.UTF8);
                    if (headers.TryGetValue("Content-Type", out var contentType))
                    {
                        try { requestMessage.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType); } catch { }
                    }
                }

                responseMessage = await _httpClient.SendAsync(requestMessage, ct);
                
                var duration = (DateTime.UtcNow - start).TotalMilliseconds;
                var bodyLength = responseMessage.Content.Headers.ContentLength ?? 0;

                result.Success = true;
                result.StatusCode = (int)responseMessage.StatusCode;
                result.BodyLength = bodyLength;
                result.DurationMs = duration;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                result.DurationMs = (DateTime.UtcNow - start).TotalMilliseconds;
            }

            return (result, responseMessage);
        }
    }
}
