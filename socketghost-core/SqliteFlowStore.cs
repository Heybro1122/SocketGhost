using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace SocketGhost.Core
{
    public class SqliteFlowStore : IFlowStore
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        public SqliteFlowStore(string dbPath)
        {
            _dbPath = dbPath;
            _connectionString = $"Data Source={dbPath}";
        }

        public async Task InitializeAsync()
        {
            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS flows (
                    id TEXT PRIMARY KEY,
                    captured_at TEXT NOT NULL,
                    pid INTEGER NOT NULL,
                    method TEXT NOT NULL,
                    url TEXT NOT NULL,
                    status_code INTEGER NOT NULL,
                    size_bytes INTEGER NOT NULL,
                    json_content TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_captured_at ON flows(captured_at);
                CREATE INDEX IF NOT EXISTS idx_pid ON flows(pid);
            ";
            await command.ExecuteNonQueryAsync();
        }

        public async Task StoreFlowAsync(StoredFlow flow)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO flows (id, captured_at, pid, method, url, status_code, size_bytes, json_content)
                VALUES ($id, $captured_at, $pid, $method, $url, $status_code, $size_bytes, $json_content)
            ";

            command.Parameters.AddWithValue("$id", flow.Id);
            command.Parameters.AddWithValue("$captured_at", flow.CapturedAt.ToString("O"));
            command.Parameters.AddWithValue("$pid", flow.Pid);
            command.Parameters.AddWithValue("$method", flow.Method);
            command.Parameters.AddWithValue("$url", flow.Url);
            command.Parameters.AddWithValue("$status_code", flow.Response.StatusCode);
            command.Parameters.AddWithValue("$size_bytes", flow.SizeBytes);
            command.Parameters.AddWithValue("$json_content", JsonConvert.SerializeObject(flow));

            await command.ExecuteNonQueryAsync();
        }

        public async Task<StoredFlow?> GetFlowAsync(string flowId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT json_content FROM flows WHERE id = $id";
            command.Parameters.AddWithValue("$id", flowId);

            var json = (string?)await command.ExecuteScalarAsync();
            return json != null ? JsonConvert.DeserializeObject<StoredFlow>(json) : null;
        }

        public async Task<IEnumerable<FlowMetadata>> GetFlowsAsync(int limit, int offset, FlowFilter filter)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var whereClauses = new List<string>();
            var parameters = new List<SqliteParameter>();

            if (filter.Pid.HasValue)
            {
                whereClauses.Add("pid = $pid");
                parameters.Add(new SqliteParameter("$pid", filter.Pid.Value));
            }

            if (!string.IsNullOrEmpty(filter.Method))
            {
                whereClauses.Add("method = $method");
                parameters.Add(new SqliteParameter("$method", filter.Method));
            }

            if (!string.IsNullOrEmpty(filter.Query))
            {
                whereClauses.Add("(url LIKE $query OR method LIKE $query)");
                parameters.Add(new SqliteParameter("$query", $"%{filter.Query}%"));
            }

            if (filter.Since.HasValue)
            {
                whereClauses.Add("captured_at >= $since");
                parameters.Add(new SqliteParameter("$since", filter.Since.Value.ToString("O")));
            }

            var whereSql = whereClauses.Any() ? "WHERE " + string.Join(" AND ", whereClauses) : "";
            
            var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT id, captured_at, pid, method, url, status_code, size_bytes, json_content
                FROM flows
                {whereSql}
                ORDER BY captured_at DESC
                LIMIT $limit OFFSET $offset
            ";

            command.Parameters.AddWithValue("$limit", limit);
            command.Parameters.AddWithValue("$offset", offset);
            command.Parameters.AddRange(parameters);

            var results = new List<FlowMetadata>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var json = reader.GetString(7);
                var flow = JsonConvert.DeserializeObject<StoredFlow>(json);
                if (flow != null)
                {
                    results.Add(new FlowMetadata
                    {
                        Id = flow.Id,
                        CapturedAt = flow.CapturedAt,
                        Pid = flow.Pid,
                        Method = flow.Method,
                        Url = flow.Url,
                        StatusCode = flow.Response.StatusCode,
                        SizeBytes = flow.SizeBytes,
                        ViaUpdate = flow.ViaUpdate,
                        ViaManualResend = flow.ViaManualResend
                    });
                }
            }

            return results;
        }

        public async Task DeleteFlowAsync(string flowId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM flows WHERE id = $id";
            command.Parameters.AddWithValue("$id", flowId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task PruneAsync(int retentionDays, long maxTotalBytes)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Prune by age
            if (retentionDays > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
                var cmdAge = connection.CreateCommand();
                cmdAge.CommandText = "DELETE FROM flows WHERE captured_at < $cutoff";
                cmdAge.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
                await cmdAge.ExecuteNonQueryAsync();
            }

            // Prune by size (approximate)
            if (maxTotalBytes > 0)
            {
                var currentSize = await GetTotalSizeAsync();
                if (currentSize > maxTotalBytes)
                {
                    // Delete oldest until we are under limit
                    // This is a simple approach; for very large DBs we might want to delete in chunks
                    var bytesToDelete = currentSize - maxTotalBytes;
                    
                    // Find cutoff date to delete enough bytes
                    // This is tricky in SQL efficiently without iterating. 
                    // Simple strategy: Delete oldest 100 flows until size is under limit
                    
                    while (currentSize > maxTotalBytes)
                    {
                        var cmdDelete = connection.CreateCommand();
                        cmdDelete.CommandText = @"
                            DELETE FROM flows 
                            WHERE id IN (
                                SELECT id FROM flows ORDER BY captured_at ASC LIMIT 100
                            )";
                        var deleted = await cmdDelete.ExecuteNonQueryAsync();
                        if (deleted == 0) break;

                        currentSize = await GetTotalSizeAsync();
                    }
                }
            }
            
            // Vacuum to reclaim space
            var cmdVacuum = connection.CreateCommand();
            cmdVacuum.CommandText = "VACUUM";
            await cmdVacuum.ExecuteNonQueryAsync();
        }

        public async Task<long> GetTotalSizeAsync()
        {
            // Check file size of DB
            if (File.Exists(_dbPath))
            {
                return new FileInfo(_dbPath).Length;
            }
            return 0;
        }
    }
}
