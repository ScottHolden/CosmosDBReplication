using Microsoft.Azure.Cosmos;

class LoadTestThread
{
	private readonly Container _c;
	private readonly int _payloadSize;
	private readonly ItemRequestOptions _options;
	private long loadCount = 0;
	private const int ContinueCount = 10000;
	public long Count => loadCount;
	public LoadTestThread(Container c, int payloadSize, ItemRequestOptions options)
	{
		_c = c;
		_payloadSize = payloadSize;
		_options = options;
	}
	private Task Insert(SampleItem item)
		=> _c.UpsertItemAsync(item, new PartitionKey(item.Id), _options).ContinueWith(x => loadCount++);
	private Task Run(int i)
		=> Insert(SampleItem.New(_payloadSize)).ContinueWith(x => i > 0 ? Run(i - 1) : Task.CompletedTask); // Dirty trick!

	public void Start()
	{
		while(true)
		{
			try
			{
				Run(ContinueCount).Wait();
			}
			catch
			{
				Console.WriteLine("Worker crash, recovering...");
			}
		}
	}
	public static LoadTestThread StartNewThread(Container c, int payloadSize, ItemRequestOptions options)
	{
		LoadTestThread a = new(c, payloadSize, options);
		Thread t = new(a.Start);
		t.Start();
		return a;
	}
}