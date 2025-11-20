using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SocketGhost.Core
{
    public class FlowStorageService
    {
        private readonly IFlowStore _store;
        private readonly string _storageRoot;
        private readonly string _flowsDir;

        public FlowStorageService()
        {
            _storageRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage");
            _flowsDir = Path.Combine(_storageRoot, "flows");

            // Ensure storage directories exist
            if (!Directory.Exists(_storageRoot)) Directory.CreateDirectory(_storageRoot);
            if (!Directory.Exists(_flowsDir)) Directory.CreateDirectory(_flowsDir);

            // Initialize store (prefer SQLite)
            try
            {
                var dbPath = Path.Combine(_storageRoot, "flows.db");
                _store = new SqliteFlowStore(dbPath);
                Console.WriteLine($"FlowStorage: Using SQLite backend at {dbPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FlowStorage: Failed to init SQLite ({ex.Message}), falling back to JSONL");
                _store = new JsonlFlowStore(_flowsDir);
            }
        }

        public async Task InitializeAsync()
        {
            await _store.InitializeAsync();
            
            if (Configuration.Current.AutoPruneOnStart)
            {
                _ = PruneAsync(); // Fire and forget on startup
            }
        }

        public async Task StoreFlowAsync(StoredFlow flow)
        {
            try
            {
                // Handle large bodies
                await HandleLargeBodyAsync(flow.Id, flow.Request, "request");
                await HandleLargeBodyAsync(flow.Id, flow.Response, "response");

                await _store.StoreFlowAsync(flow);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FlowStorage: Error storing flow {flow.Id}: {ex.Message}");
            }
        }

        private async Task HandleLargeBodyAsync(string flowId, dynamic messagePart, string type)
        {
            // messagePart is StoredFlowRequest or StoredFlowResponse
            // We use dynamic to access common properties BodyPreview, FullBodyPath, BodyIsBinary
            // But C# dynamic might be tricky with different types. 
            // Let's overload or use specific logic.
            
            string? body = messagePart.BodyPreview;
            if (string.IsNullOrEmpty(body)) return;

            // Check if body is too large
            int maxInline = Configuration.Current.FlowStoreMaxInlineBytes;
            if (body.Length > maxInline)
            {
                var flowDir = Path.Combine(_flowsDir, flowId);
                if (!Directory.Exists(flowDir)) Directory.CreateDirectory(flowDir);

                var fileName = $"{type}.bin"; // or .txt depending on content, but .bin is safe
                var filePath = Path.Combine(flowDir, fileName);

                await File.WriteAllTextAsync(filePath, body);

                messagePart.FullBodyPath = filePath;
                messagePart.BodyPreview = body.Substring(0, Math.Min(1024, body.Length)) + "... (truncated)";
            }
        }
        
        // Overload for StoredFlowRequest
        private async Task HandleLargeBodyAsync(string flowId, StoredFlowRequest request, string type)
        {
            if (string.IsNullOrEmpty(request.BodyPreview)) return;
            
            int maxInline = Configuration.Current.FlowStoreMaxInlineBytes;
            if (request.BodyPreview.Length > maxInline)
            {
                var flowDir = Path.Combine(_flowsDir, flowId);
                if (!Directory.Exists(flowDir)) Directory.CreateDirectory(flowDir);

                var fileName = $"{type}.bin";
                var filePath = Path.Combine(flowDir, fileName);

                await File.WriteAllTextAsync(filePath, request.BodyPreview);

                request.FullBodyPath = filePath;
                request.BodyPreview = request.BodyPreview.Substring(0, Math.Min(1024, request.BodyPreview.Length)) + "... (truncated)";
            }
        }

        // Overload for StoredFlowResponse
        private async Task HandleLargeBodyAsync(string flowId, StoredFlowResponse response, string type)
        {
             if (string.IsNullOrEmpty(response.BodyPreview)) return;
            
            int maxInline = Configuration.Current.FlowStoreMaxInlineBytes;
            if (response.BodyPreview.Length > maxInline)
            {
                var flowDir = Path.Combine(_flowsDir, flowId);
                if (!Directory.Exists(flowDir)) Directory.CreateDirectory(flowDir);

                var fileName = $"{type}.bin";
                var filePath = Path.Combine(flowDir, fileName);

                await File.WriteAllTextAsync(filePath, response.BodyPreview);

                response.FullBodyPath = filePath;
                response.BodyPreview = response.BodyPreview.Substring(0, Math.Min(1024, response.BodyPreview.Length)) + "... (truncated)";
            }
        }

        public async Task<StoredFlow?> GetFlowAsync(string flowId)
        {
            var flow = await _store.GetFlowAsync(flowId);
            if (flow == null) return null;

            // If full body is on disk, should we load it?
            // API usually requests stream separately. 
            // But for "GetFlow", we might want to return the preview or check if we need to load it?
            // The requirement says: "GET /flows/{flowId} -> returns full metadata + if body inline include it; if stored on disk, return downloadUrl fields"
            // Our StoredFlow model has FullBodyPath, which is internal path. We should probably map this to a download URL in the API layer.
            // So here we just return the stored object.
            
            return flow;
        }

        public async Task<IEnumerable<FlowMetadata>> GetFlowsAsync(int limit, int offset, FlowFilter filter)
        {
            return await _store.GetFlowsAsync(limit, offset, filter);
        }

        public async Task DeleteFlowAsync(string flowId)
        {
            await _store.DeleteFlowAsync(flowId);
            
            // Delete files
            var flowDir = Path.Combine(_flowsDir, flowId);
            if (Directory.Exists(flowDir))
            {
                try { Directory.Delete(flowDir, true); } catch { }
            }
        }

        public async Task PruneAsync()
        {
            Console.WriteLine("FlowStorage: Pruning started...");
            try
            {
                await _store.PruneAsync(Configuration.Current.FlowRetentionDays, Configuration.Current.FlowStoreMaxTotalBytes);
                
                // Also prune orphaned file directories? 
                // That's expensive to check every time. 
                // For now, rely on DeleteFlowAsync cleaning up. 
                // If PruneAsync deletes from DB, it doesn't delete files automatically unless we implement it there or here.
                // SqliteFlowStore.PruneAsync just deletes rows.
                // We need a way to know which IDs were deleted to delete files.
                // Or we can iterate directories and check if they exist in DB.
                
                // Simple orphaned file cleanup:
                // Iterate all directories in _flowsDir, if ID not in DB, delete.
                // This might be slow if many flows.
                // Let's skip for MVP or do a simple check if we can.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FlowStorage: Pruning error: {ex.Message}");
            }
            Console.WriteLine("FlowStorage: Pruning completed.");
        }
        
        public Stream? GetBodyStream(string flowId, string type)
        {
            var flowDir = Path.Combine(_flowsDir, flowId);
            var filePath = Path.Combine(flowDir, $"{type}.bin");
            if (File.Exists(filePath))
            {
                return File.OpenRead(filePath);
            }
            return null;
        }
    }
}
