const int clientCount = 500;
const string url = "https://localhost:7152"; // Update to your Puzzle78 home page URL
TimeSpan duration = TimeSpan.FromMinutes(1);

using CancellationTokenSource cts = new(duration);
Task[] tasks = new Task[clientCount];

for (int i = 0; i < clientCount; i++)
{
	tasks[i] = RunLoadTestAsync(i, url, cts.Token);
}

Console.WriteLine($"Starting load test with {clientCount} clients for {duration.TotalMinutes} minutes...");
await Task.WhenAll(tasks);
Console.WriteLine("Load test completed.");


static async Task RunLoadTestAsync(int clientId, string url, CancellationToken token)
{
	using HttpClient client = new();
	int requestNumber = 0;
	while (!token.IsCancellationRequested)
	{
		try
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();
			var response = await client.GetAsync(url, token);
			sw.Stop();
			response.EnsureSuccessStatusCode();
			Console.WriteLine($"[Client {clientId}] Request #{++requestNumber} succeeded in {sw.ElapsedMilliseconds} ms");
		}
		catch (OperationCanceledException)
		{
			// Test duration ended
			break;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[Client {Task.CurrentId}] Request #{++requestNumber} failed: {ex.Message}");
		}
	}
}