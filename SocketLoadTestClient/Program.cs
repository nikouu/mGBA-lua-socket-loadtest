// See https://aka.ms/new-console-template for more information
using SocketLoadTestClient;

Console.WriteLine("Hello, World!");

await Task.Delay(3000);

var httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost:5000") };
var loadTest = new LoadTest(httpClient);
await loadTest.RunLoadTest(TimeSpan.FromSeconds(30), 350);

Console.ReadLine();