using System;
using System.Threading.Tasks;

namespace SocketGhost.Core
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting SocketGhost Core...");

            Configuration.Load();
            
            var engine = Configuration.Current.Engine.ToLower();
            Console.WriteLine($"Engine mode: {engine}");
            
            var wsServer = new WebSocketServer();
            var startWsTask = wsServer.StartAsync("http://127.0.0.1:9000/");

            var processApi = new ProcessApi();
            _ = processApi.StartAsync("http://127.0.0.1:9100/");

            var flowStorage = new FlowStorageService();
            await flowStorage.InitializeAsync();

            var scriptManager = new ScriptEngineManager(wsServer);
            await scriptManager.LoadScriptsAsync();

            var scriptApi = new ScriptApi(scriptManager);
            _ = scriptApi.StartAsync("http://127.0.0.1:9200/");

            var interceptorManager = new InterceptorManager(wsServer);
            wsServer.SetInterceptorManager(interceptorManager);
            var pidResolver = new PidResolver();

            var flowApi = new FlowApi(flowStorage, interceptorManager);
            _ = flowApi.StartAsync("http://127.0.0.1:9300/");

            // Start appropriate proxy engine
            MitmAdapterManager? mitmAdapter = null;
            SocketGhostProxyServer? titaniumProxy = null;

            if (engine == "mitm")
            {
                // mitmproxy adapter mode
                mitmAdapter = new MitmAdapterManager(wsServer, Configuration.Current.Mitm);
                await mitmAdapter.StartAsync();
            }
            else
            {
                // Default: Titanium.Web.Proxy mode
                titaniumProxy = new SocketGhostProxyServer(wsServer, interceptorManager, pidResolver, scriptManager, flowStorage);
                titaniumProxy.Start();
            }

            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();

            // Stop services
            mitmAdapter?.Stop();
            titaniumProxy?.Stop();
        }
    }
}
