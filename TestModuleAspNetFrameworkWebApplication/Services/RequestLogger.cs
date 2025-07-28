using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;

namespace TestModuleAspNetFrameworkWebApplication.Services
{
    internal static class RequestLogger
    {
        private static readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        static RequestLogger()
        {
            Task.Factory.StartNew(ProcessQueueAsync, TaskCreationOptions.LongRunning);
        }

        public static void Enqueue(HttpRequest req)
        {
            var safeRequestData = SafeRequestData.FromHttpRequest(req);
            _queue.Enqueue(new JavaScriptSerializer().Serialize(safeRequestData));
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
                        // Write to a text file.
                        System.IO.File.AppendAllText(@"C:\Temp\GlobalLog.txt", entry + Environment.NewLine);
                    }
                    catch (Exception ex)
                    {
                        // Log to a file
                        System.IO.File.AppendAllText(@"C:\Temp\error.log", $"{DateTime.UtcNow}: {ex}" + Environment.NewLine);
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
}