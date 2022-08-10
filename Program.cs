using CosmosDBReplication;
using Microsoft.Azure.Cosmos;

var config = await Config.LoadAsync("appsettings.json");

const string ItemID = "0fb5e138-1224-40a2-a7c1-b8e02d568b3d";

Console.WriteLine($"Primary Region: {config.PrimaryRegion}");
Console.WriteLine($"Secondary Region: {config.SecondaryRegion}");
Console.WriteLine();

int loadThreadCount = ReadNumberInRangeOrDefault("Number of threads", 1, 8, 2);
int primaryPayloadSize = ReadNumberInRangeOrDefault("Size of primary payload", 0, 20 * 1024, 1024);
int loadtestPayloadSize = ReadNumberInRangeOrDefault("Size of load test payload", 0, 20 * 1024, 2048);

var writeContainer = config.GetContainer(Region.Primary);

var account = await writeContainer.Database.Client.ReadAccountAsync();
var defaultConsistency = account.Consistency.DefaultConsistencyLevel;

Console.WriteLine($"\nDefault Consistency is set to {defaultConsistency}\n");

Console.WriteLine($"Consistency options (can't be higher than default):");

ConsistencyLevel[] levels = new[] {
	ConsistencyLevel.Eventual,
	ConsistencyLevel.ConsistentPrefix,
	ConsistencyLevel.Session,
	ConsistencyLevel.BoundedStaleness,
	ConsistencyLevel.Strong
};

int minConsistencyValue = 0;
int maxConsistencyValue = Array.IndexOf(levels, defaultConsistency);
for (int i = minConsistencyValue; i <= maxConsistencyValue; i++)
{
	Console.WriteLine($"{i}: {levels[i]}");
}

Console.WriteLine();

var writeOption = new ItemRequestOptions
{
	ConsistencyLevel = levels[ReadNumberInRangeOrDefault("Write consistency level", minConsistencyValue, maxConsistencyValue, maxConsistencyValue)]
};

var readOption = new ItemRequestOptions
{
	ConsistencyLevel = levels[ReadNumberInRangeOrDefault("Read consistency level", minConsistencyValue, maxConsistencyValue, maxConsistencyValue)]
};

var loadTestOption = new ItemRequestOptions
{
	ConsistencyLevel = levels[ReadNumberInRangeOrDefault("Load test consistency level", minConsistencyValue, maxConsistencyValue, maxConsistencyValue)]
};

Console.WriteLine("\n\nNotes about console output:");

Console.WriteLine("  Wri <- The result of the write operation to the primary region");
Console.WriteLine("  Pri <- The result of the read operation to the primary region");
Console.WriteLine("  Sec <- The result of the read operation to the secondary region\n");
Console.WriteLine("  If a mismatch is found, '<<< MISMATCH' will be shown and the console paused.\n");
Console.WriteLine("  A load test will be running on seperate threads in the background,");
Console.WriteLine("  a count of writes will be shown next to 'LoadTest Wri' on each primary write.");
Console.WriteLine("  The load test will continue to run until you close the application.");
Console.WriteLine("\nReady! Press enter to start...");
Console.ReadLine();

// This starts all threads!
var loadThreads = Enumerable.Range(0, loadThreadCount)
							.Select(x => LoadTestThread.StartNewThread(config.GetContainer(Region.Primary, false), loadtestPayloadSize, loadTestOption))
							.ToArray();

ReadValidation primaryRead = new("Pri", config.GetContainer(Region.Primary), readOption);
ReadValidation secondaryRead = new("Sec", config.GetContainer(Region.Secondary), readOption);

while (true)
{
	try
	{
		SampleItem item = SampleItem.New(ItemID, primaryPayloadSize);

		writeContainer.UpsertItemAsync(item, new PartitionKey(item.Id), writeOption).Wait();
		Console.WriteLine($"---\nWri [{writeOption.ConsistencyLevel}]: {item.Value}  (LoadTest Wri: {loadThreads.Sum(x=>x.Count)})");

		var secTask = secondaryRead.Validate(item);
		var priTask = primaryRead.Validate(item);

		Task.WaitAll(secTask, priTask);

		if (priTask.Result || secTask.Result)
		{
			Console.WriteLine("Press any key to continue...");
			Console.ReadLine();
		}
	}
	catch
	{
		Console.WriteLine("Crashed, recovering...");
	}
}

static int ReadNumberInRangeOrDefault(string prompt, int min, int max, int def)
{
	Console.Write($"{prompt} ({min}-{max}) [{def}]: ");
	string? input = Console.ReadLine();
	if (string.IsNullOrWhiteSpace(input)) return def;
	if (int.TryParse(input, out int res))
	{
		if (res >= min && res <= max) return res;
		throw new Exception($"Number {res} is not in range {min}-{max} (inclusive)");
	}
	throw new Exception($"Invalid number '{input}'");
}