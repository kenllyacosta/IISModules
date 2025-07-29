using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web;

namespace GlobalRequestLogger.Services
{
    internal static class RequestLogger
    {
        private static readonly ConcurrentQueue<SafeRequestData> _queue = new ConcurrentQueue<SafeRequestData>();
        private static readonly ConcurrentQueue<LogEntrySafeResponse> _responseQueue = new ConcurrentQueue<LogEntrySafeResponse>();
        private static readonly string _connectionString = "Server=.;Database=GlobalRequests;Integrated Security=True;TrustServerCertificate=True;";

        static RequestLogger()
        {
            Task.Factory.StartNew(ProcessQueueAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(ProcessResponseQueueAsync, TaskCreationOptions.LongRunning);
        }

        public static void Enqueue(HttpRequest req)
        {
            var safeRequestData = SafeRequestData.FromHttpRequest(req);
            _queue.Enqueue(safeRequestData);
        }

        public static void EnqueueResponse(string url, string httpMethod, long responseTime, DateTime timestamp)
        {
            var logEntry = new LogEntrySafeResponse
            {
                Url = url,
                HttpMethod = httpMethod,
                ResponseTime = responseTime,
                Timestamp = timestamp
            };
            _responseQueue.Enqueue(logEntry);
        }

        private static volatile bool _isRunning = true;

        public static void Stop()
        {
            _isRunning = false;
        }

        private static async Task ProcessQueueAsync()
        {
            while (_isRunning)
            {
                while (_queue.TryDequeue(out var entry))
                {
                    try
                    {
                        // Log to SQL Server
                        using (var connection = new SqlConnection(_connectionString))
                        {
                            await connection.OpenAsync();
                            var command = new SqlCommand("InsertRequestLog", connection) { CommandType = System.Data.CommandType.StoredProcedure };

                            command.Parameters.AddWithValue("@Url", entry.Url);
                            command.Parameters.AddWithValue("@HttpMethod", entry.HttpMethod);
                            command.Parameters.AddWithValue("@Headers", entry.Headers);
                            command.Parameters.AddWithValue("@QueryString", entry.QueryString);
                            command.Parameters.AddWithValue("@UserHostAddress", entry.UserHostAddress);
                            command.Parameters.AddWithValue("@UserAgent", entry.UserAgent);
                            command.Parameters.AddWithValue("@ContentType", entry.ContentType);
                            command.Parameters.AddWithValue("@ContentLength", entry.ContentLength);
                            command.Parameters.AddWithValue("@RawUrl", entry.RawUrl);
                            command.Parameters.AddWithValue("@ApplicationPath", entry.ApplicationPath);

                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // For simplicity, we just write to console here.
                        Console.WriteLine($"Error logging request: {ex.Message}");
                    }
                }

                await Task.Delay(250); // Short pause to prevent CPU spinning
            }
        }

        private static async Task ProcessResponseQueueAsync()
        {
            while (_isRunning)
            {
                while (_responseQueue.TryDequeue(out var entry))
                {
                    try
                    {
                        // Log response data to SQL Server
                        using (var connection = new SqlConnection(_connectionString))
                        {
                            await connection.OpenAsync();
                            var command = new SqlCommand("InsertResponseLog", connection) { CommandType = System.Data.CommandType.StoredProcedure };
                            command.Parameters.AddWithValue("@Url", entry.Url);
                            command.Parameters.AddWithValue("@HttpMethod", entry.HttpMethod);
                            command.Parameters.AddWithValue("@ResponseTime", entry.ResponseTime);
                            command.Parameters.AddWithValue("@Timestamp", entry.Timestamp);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // For simplicity, we just write to console here.
                        Console.WriteLine($"Error logging response: {ex.Message}");
                    }
                }
                await Task.Delay(250); // Short pause to prevent CPU spinning
            }
        }
    }

    internal class SafeRequestData
    {
        public string Url { get; set; }
        public string HttpMethod { get; set; }
        public string Headers { get; set; }
        public string QueryString { get; set; }
        public string UserHostAddress { get; set; }
        public string UserAgent { get; set; }
        public string ContentType { get; set; }
        public int ContentLength { get; set; }
        public string RawUrl { get; set; }
        public string ApplicationPath { get; set; }

        public static SafeRequestData FromHttpRequest(HttpRequest req)
        {
            return new SafeRequestData
            {
                Url = req.Url?.ToString(),
                HttpMethod = req.HttpMethod,
                Headers = req.Headers?.ToString(),
                QueryString = req.QueryString?.ToString(),
                UserHostAddress = req.UserHostAddress,
                UserAgent = req.UserAgent,
                ContentType = req.ContentType,
                ContentLength = req.ContentLength,
                RawUrl = req.RawUrl,
                ApplicationPath = req.ApplicationPath
            };
        }
    }

    internal class LogEntrySafeResponse
    {
        public string Url { get; set; }
        public string HttpMethod { get; set; }
        public long ResponseTime { get; set; }
        public DateTime Timestamp { get; set; }
    }
}