using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace GlobalRequestLogger.Services
{
    internal static class RequestLogger
    {
        private static readonly ConcurrentQueue<SafeRequestData> _queue = new ConcurrentQueue<SafeRequestData>();
        private static readonly ConcurrentQueue<LogEntrySafeResponse> _responseQueue = new ConcurrentQueue<LogEntrySafeResponse>();
        private static string _connectionString = "";

        static RequestLogger()
        {
            Task.Factory.StartNew(ProcessQueueAsync, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(ProcessResponseQueueAsync, TaskCreationOptions.LongRunning);
        }

        internal static void Enqueue(HttpRequest req, string connectionString, string actionTaken, int? ruleTriggered)
        {
            _connectionString = connectionString;
            var safeRequestData = SafeRequestData.FromHttpRequest(req, actionTaken, ruleTriggered);
            _queue.Enqueue(safeRequestData);
        }

        internal static void EnqueueResponse(string url, string httpMethod, long responseTime, DateTime timestamp, string connectionString)
        {
            _connectionString = connectionString;
            var logEntry = new LogEntrySafeResponse
            {
                Url = url,
                HttpMethod = httpMethod,
                ResponseTime = responseTime,
                Timestamp = timestamp,
                ServerVariables = string.Join(";", HttpContext.Current.Request.ServerVariables.AllKeys.Select(key => $"{key}={HttpContext.Current.Request.ServerVariables[key]}"))
            };
            _responseQueue.Enqueue(logEntry);
        }

        private static volatile bool _isRunning = true;

        internal static void Stop()
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

                            command.Parameters.AddWithValue("@Url", entry.Url ?? "");
                            command.Parameters.AddWithValue("@HttpMethod", entry.HttpMethod ?? "");
                            command.Parameters.AddWithValue("@Headers", entry.Headers ?? "");
                            command.Parameters.AddWithValue("@QueryString", entry.QueryString ?? "");
                            command.Parameters.AddWithValue("@UserHostAddress", entry.UserHostAddress ?? "");
                            command.Parameters.AddWithValue("@UserAgent", entry.UserAgent ?? "");
                            command.Parameters.AddWithValue("@ContentType", entry.ContentType ?? "");
                            command.Parameters.AddWithValue("@ContentLength", entry.ContentLength);
                            command.Parameters.AddWithValue("@RawUrl", entry.RawUrl ?? "");
                            command.Parameters.AddWithValue("@ApplicationPath", entry.ApplicationPath ?? "");
                            command.Parameters.AddWithValue("@ActionTaken", entry.ActionTaken ?? "");
                            command.Parameters.AddWithValue("@ServerVariables", entry.ServerVariables ?? "");
                            command.Parameters.AddWithValue("@RuleTriggered", entry.RuleTriggered ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@Host", entry.Host ?? "");

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
                            command.Parameters.AddWithValue("@ServerVariables", entry.ServerVariables);
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

        internal static string Encrypt(string clearText, string key)
        {
            string Result = "";
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] dataBytes = Encoding.UTF8.GetBytes(clearText);

            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.Mode = CipherMode.CBC; //Better security
                aes.Padding = PaddingMode.PKCS7;

                aes.GenerateIV(); //Generate a random IV (Init Vector) for each encryption

                using (var encryptor = aes.CreateEncryptor())
                    Result = Convert.ToBase64String(aes.IV.Concat(encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length)).ToArray());
            }

            return Result;
        }

        internal static string Decrypt(string clearText, string key)
        {
            string Result = "";
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] encryptedBytesWithIV = Convert.FromBase64String(clearText);

            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.Mode = CipherMode.CBC; //Better security
                aes.Padding = PaddingMode.PKCS7;

                //Extract IV from the encrypted data
                aes.IV = encryptedBytesWithIV.Take(aes.BlockSize / 8).ToArray(); //Set IV for decryption
                byte[] encryptedBytes = encryptedBytesWithIV.Skip(aes.BlockSize / 8).ToArray();

                using (var decryptor = aes.CreateDecryptor())   
                    Result = Encoding.UTF8.GetString(decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length));
            }
            return Result;
        }

        internal static int GetTokenExpirationDuration(string host)
        {
            //Query the database to fetch the token expiration duration
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var command = new SqlCommand($@"SELECT TokenExpirationDurationHr FROM AppEntity WHERE Host = @host", connection);
                command.Parameters.AddWithValue("@host", host);

                int.TryParse(command.ExecuteScalar()?.ToString(), out var duration);
                return duration > 0 ? duration : 12;
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
        public string ActionTaken { get; set; }
        public string ServerVariables { get; set; }
        public int? RuleTriggered { get; set; }
        public string Host { get; set; }

        public static SafeRequestData FromHttpRequest(HttpRequest req, string actionTaken, int? ruleTriggered)
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
                ApplicationPath = req.ApplicationPath,
                ActionTaken = actionTaken,
                ServerVariables = string.Join(";", req.ServerVariables.AllKeys.Select(key => $"{key}={req.ServerVariables[key]}")),
                RuleTriggered = ruleTriggered,
                Host = req.Url?.Host
            };
        }
    }

    internal class LogEntrySafeResponse
    {
        public string Url { get; set; }
        public string HttpMethod { get; set; }
        public long ResponseTime { get; set; }
        public DateTime Timestamp { get; set; }
        public string ServerVariables { get; set; } = string.Empty;
    }
}