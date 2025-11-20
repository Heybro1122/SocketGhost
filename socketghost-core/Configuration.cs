using System;
using System.IO;
using Newtonsoft.Json;

namespace SocketGhost.Core
{
    public class AppConfig
    {
        // Flow storage settings
        public int FlowRetentionDays { get; set; } = 30;
        public long FlowStoreMaxTotalBytes { get; set; } = 1024 * 1024 * 500; // 500 MB
        public int FlowStoreMaxInlineBytes { get; set; } = 1024 * 128; // 128 KB
        public bool AutoPruneOnStart { get; set; } = true;

        // Proxy engine settings
        public string Engine { get; set; } = "titanium";  // "titanium" or "mitm"
        
        // mitmproxy adapter settings (only used if Engine = "mitm")
        public MitmAdapterConfig Mitm { get; set; } = new MitmAdapterConfig();
    }

    public static class Configuration
    {
        private static readonly string ConfigPath = "config.json";
        public static AppConfig Current { get; private set; } = new AppConfig();

        public static void Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    Current = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading config: {ex.Message}. Using defaults.");
                }
            }
            else
            {
                Save();
            }
        }

        public static void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(Current, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }
    }
}
