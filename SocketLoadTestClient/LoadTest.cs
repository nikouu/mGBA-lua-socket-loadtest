using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;

namespace SocketLoadTestClient
{
    public class LoadTest
    {
        private readonly HttpClient _httpClient;
        private readonly ConcurrentQueue<Stat> _stats = new();
        private readonly ConcurrentBag<Task> _requests = new();

        public LoadTest(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task RunLoadTest(string message, TimeSpan duration, int requestsPerSecond)
        {
            Console.WriteLine($"Running load test with {requestsPerSecond} requests per second for {duration.Seconds} seconds...");
            var totalRequestsToSend = duration.Seconds * requestsPerSecond;
            var totalRequestsSent = 0;

            // Delay between requests to crudely spread them across the second
            var delayBetweenRequests = TimeSpan.FromSeconds(1.0 / requestsPerSecond) * 0.7;
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            var start = Stopwatch.GetTimestamp();

            try
            {
                while (await timer.WaitForNextTickAsync())
                {
                    for (int i = 0; i < requestsPerSecond; i++)
                    {
                        var task = SendRequestAndRecordMetricsAsync(message);
                        _requests.Add(task);
                        totalRequestsSent++;

                        // Don't delay after the last request in the second
                        if (i < requestsPerSecond - 1)
                        {
                            await Task.Delay(delayBetweenRequests);
                        }
                    }

                    Console.WriteLine($"{totalRequestsSent}/{totalRequestsToSend} requests sent...");

                    if (totalRequestsSent >= totalRequestsToSend)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timer cancelled, test duration ended
            }

            var finishedTaskCount = _requests.Count(x => x.IsCompleted);
            Console.WriteLine($"\nTest ended. Waiting for {_requests.Count - finishedTaskCount}/{_requests.Count} requests to finish...");
            await Task.WhenAll(_requests);
            var totalTime = (Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency;
            var successfulRequestCount = _stats.Count(x => x.IsSuccessful);

            Console.WriteLine($"Total requests sent: {totalRequestsSent}");
            Console.WriteLine($"Successful requests: {successfulRequestCount} ({(int)(((double)successfulRequestCount / totalRequestsSent) * 100)}%)");
            Console.WriteLine($"Total test time (seconds): {totalTime:F2}");
            Console.WriteLine($"Requests per second handled: {successfulRequestCount / totalTime:F2}");
        }

        private async Task SendRequestAndRecordMetricsAsync(string message)
        {
            var startTime = Stopwatch.GetTimestamp();

            try
            {
                var isSuccessful = await SendMessageAsync(message);
                var duration = Stopwatch.GetElapsedTime(startTime);
                _stats.Enqueue(new Stat { Duration = duration, IsSuccessful = isSuccessful });
            }
            catch
            {
                var duration = Stopwatch.GetElapsedTime(startTime);
                _stats.Enqueue(new Stat { Duration = duration, IsSuccessful = false });
            }
        }

        private async Task<bool> SendMessageAsync(string message)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/mgbaendpoint?message={message}");

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync();

                return content == message;
            }
            catch
            {
                return false;
            }
        }

        private record struct Stat(TimeSpan Duration, bool IsSuccessful);
    }
}