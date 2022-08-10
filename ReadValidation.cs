using Microsoft.Azure.Cosmos;

class ReadValidation
{
	private readonly string _name;
	private readonly Container _container;
	private readonly ItemRequestOptions _options;

	public ReadValidation(string name, Container container, ItemRequestOptions options)
	{
		_name = name;
		_container = container;
		_options = options;
	}

	public async Task<bool> Validate(SampleItem item)
	{
		bool issue = false;
		while(true)
		{
			var result = await _container.ReadItemAsync<SampleItem>(item.Id, new PartitionKey(item.Id), _options);
			if (result.Resource.Value == item.Value)
			{
				Console.WriteLine($"{_name} [{_options.ConsistencyLevel}]: {result.Resource.Value}");
				return issue;
			}
			else
			{
				Console.WriteLine($"{_name} [{_options.ConsistencyLevel}]: {result.Resource.Value}  <<< MISMATCH");
				issue = true;
			}
		}
	}
}
