using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;

namespace SocketLoadTestClient
{
    public class LoadTest
    {
        private readonly HttpClient _httpClient;
        private readonly ConcurrentQueue<Stat> _stats = new();
        private readonly ConcurrentBag<Task> _individualRequests = new();
        private readonly ConcurrentBag<Task> _perSecondRequests = new();

        private static readonly string _longMessage = new string('a', 5000);

        public LoadTest(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task RunLoadTest(TimeSpan duration, int requestsPerSecond)
        {
            Console.WriteLine($"Running load test with {requestsPerSecond} requests per second for {duration.Seconds} seconds...");
            var totalRequestsToSend = duration.Seconds * requestsPerSecond;
            var batchRequestsCount = 0;
            var totalRequestsSent = 0;

            // Delay between requests to crudely spread them across the second
            var delayBetweenRequests = TimeSpan.FromMilliseconds(1);
            //var delayBetweenRequests = TimeSpan.FromSeconds(1.0 / requestsPerSecond);
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            var start = Stopwatch.GetTimestamp();

            try
            {
                while (await timer.WaitForNextTickAsync() && batchRequestsCount < duration.TotalSeconds)
                {
                    batchRequestsCount++;
                    _perSecondRequests.Add(Task.Run(async () =>
                    {

                        for (int i = 0; i < requestsPerSecond; i++)
                        {
                            var task = SendRequestAndRecordMetricsAsync(Guid.NewGuid().ToString());
                            _individualRequests.Add(task);
                            Interlocked.Increment(ref totalRequestsSent);

                            // Don't delay after the last request in the second
                            if (i < requestsPerSecond - 1)
                            {
                                await Task.Delay(delayBetweenRequests);
                            }
                        }

                        Console.WriteLine($"{totalRequestsSent}/{totalRequestsToSend} requests sent... {DateTime.Now.Second}");


                    }));

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

            var finishedTaskCount = _individualRequests.Count(x => x.IsCompleted);
            Console.WriteLine($"\nTest ended. Waiting for {_individualRequests.Count - finishedTaskCount}/{_individualRequests.Count} requests to finish...");
            await Task.WhenAll(_perSecondRequests);
            await Task.WhenAll(_individualRequests);            
            var totalTime = (Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency;
            var successfulRequestCount = _stats.Count(x => x.IsSuccessful);

            Console.WriteLine($"\nTotal requests sent: {totalRequestsSent}");
            Console.WriteLine($"Successful requests: {successfulRequestCount} ({(int)(((double)successfulRequestCount / totalRequestsSent) * 100)}%)");
            Console.WriteLine($"Average latency (ms): {_stats.Average(x => x.Duration.TotalMilliseconds)}");
            Console.WriteLine($"95th percentile latency (ms): {_stats.Select(x => x.Duration.TotalMilliseconds).OrderBy(x => x).ElementAt((int)(0.95 * _stats.Count) - 1)}");
            Console.WriteLine($"Largest latency (ms): {_stats.Max(x => x.Duration.TotalMilliseconds)}");
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
                    Console.WriteLine("bad http status");
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync();

                if (content != message)
                {
                    Console.WriteLine($"content doesnt equal message: {content.Length} vs {message.Length}");
                }

                return content == message;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        private record struct Stat(TimeSpan Duration, bool IsSuccessful);
    }
}