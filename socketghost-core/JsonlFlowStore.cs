using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SocketGhost.Core
{
    public class JsonlFlowStore : IFlowStore
    {
        private readonly string _storagePath;
        private readonly string _indexFilePath;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public JsonlFlowStore(string storagePath)
        {
            _storagePath = storagePath;
            _indexFilePath = Path.Combine(storagePath, "index.jsonl");
        }

        public async Task InitializeAsync()
        {
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }
            await Task.CompletedTask;
        }

        public async Task StoreFlowAsync(StoredFlow flow)
        {
            var json = JsonConvert.SerializeObject(flow, Formatting.None);
            await _lock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(_indexFilePath, json + Environment.NewLine);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<StoredFlow?> GetFlowAsync(string flowId)
        {
            // Inefficient for large files, but acceptable for fallback
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(_indexFilePath)) return null;

                foreach (var line in await File.ReadAllLinesAsync(_indexFilePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        // Quick check if ID matches before full deserialize? 
                        // For now, just deserialize.
                        var flow = JsonConvert.DeserializeObject<StoredFlow>(line);
                        if (flow != null && flow.Id == flowId)
                        {
                            return flow;
                        }
                    }
                    catch { /* ignore malformed lines */ }
                }
            }
            finally
            {
                _lock.Release();
            }
            return null;
        }

        public async Task<IEnumerable<FlowMetadata>> GetFlowsAsync(int limit, int offset, FlowFilter filter)
        {
            var results = new List<FlowMetadata>();
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(_indexFilePath)) return results;

                // Read all lines, parse, filter, sort, page
                // Very inefficient for large datasets, but this is a fallback.
                var allFlows = new List<StoredFlow>();
                var lines = await File.ReadAllLinesAsync(_indexFilePath);
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var flow = JsonConvert.DeserializeObject<StoredFlow>(line);
                        if (flow != null) allFlows.Add(flow);
                    }
                    catch { }
                }

                var query = allFlows.AsEnumerable();

                if (filter.Pid.HasValue)
                    query = query.Where(f => f.Pid == filter.Pid.Value);
                
                if (!string.IsNullOrEmpty(filter.Method))
                    query = query.Where(f => f.Method == filter.Method);
                
                if (!string.IsNullOrEmpty(filter.Query))
                    query = query.Where(f => f.Url.Contains(filter.Query, StringComparison.OrdinalIgnoreCase) || f.Method.Contains(filter.Query, StringComparison.OrdinalIgnoreCase));
                
                if (filter.Since.HasValue)
                    query = query.Where(f => f.CapturedAt >= filter.Since.Value);

                results = query
                    .OrderByDescending(f => f.CapturedAt)
                    .Skip(offset)
                    .Take(limit)
                    .Select(f => new FlowMetadata
                    {
                        Id = f.Id,
                        CapturedAt = f.CapturedAt,
                        Pid = f.Pid,
                        Method = f.Method,
                        Url = f.Url,
                        StatusCode = f.Response.StatusCode,
                        SizeBytes = f.SizeBytes,
                        ViaUpdate = f.ViaUpdate,
                        ViaManualResend = f.ViaManualResend
                    })
                    .ToList();
            }
            finally
            {
                _lock.Release();
            }
            return results;
        }

        public async Task DeleteFlowAsync(string flowId)
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(_indexFilePath)) return;

                var lines = await File.ReadAllLinesAsync(_indexFilePath);
                var newLines = new List<string>();
                bool changed = false;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var flow = JsonConvert.DeserializeObject<StoredFlow>(line);
                        if (flow != null && flow.Id == flowId)
                        {
                            changed = true;
                            continue; // Skip this one
                        }
                        newLines.Add(line);
                    }
                    catch { newLines.Add(line); }
                }

                if (changed)
                {
                    await File.WriteAllLinesAsync(_indexFilePath, newLines);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task PruneAsync(int retentionDays, long maxTotalBytes)
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(_indexFilePath)) return;

                var lines = await File.ReadAllLinesAsync(_indexFilePath);
                var flows = new List<StoredFlow>();
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var flow = JsonConvert.DeserializeObject<StoredFlow>(line);
                        if (flow != null) flows.Add(flow);
                    }
                    catch { }
                }

                var initialCount = flows.Count;

                // Prune by age
                if (retentionDays > 0)
                {
                    var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
                    flows.RemoveAll(f => f.CapturedAt < cutoff);
                }

                // Prune by size (approximate)
                if (maxTotalBytes > 0)
                {
                    long currentSize = new FileInfo(_indexFilePath).Length; // Rough estimate
                    if (currentSize > maxTotalBytes)
                    {
                        // Sort by date ascending
                        flows.Sort((a, b) => a.CapturedAt.CompareTo(b.CapturedAt));
                        
                        // Remove oldest until we assume we are safe? 
                        // Since we don't know exact size of each line easily without re-serializing,
                        // let's just remove 20% of oldest flows if we are over limit.
                        int toRemove = (int)(flows.Count * 0.2);
                        if (toRemove > 0)
                        {
                            flows.RemoveRange(0, toRemove);
                        }
                    }
                }

                if (flows.Count != initialCount)
                {
                    var newLines = flows.Select(f => JsonConvert.SerializeObject(f, Formatting.None));
                    await File.WriteAllLinesAsync(_indexFilePath, newLines);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<long> GetTotalSizeAsync()
        {
            if (File.Exists(_indexFilePath))
            {
                return new FileInfo(_indexFilePath).Length;
            }
            return 0;
        }
    }
}
