using Microsoft.Azure.Cosmos;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CosmosDBReplication;

public record Config(string ConnectionString, string DatabaseId, string ContainerId, string? PrimaryRegion, string? SecondaryRegion)
{
	public static async Task<Config> LoadAsync(string path)
	{
		using Stream input = File.OpenRead(path);
		JsonSerializerOptions options = new()
		{
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		};
		Config config = await JsonSerializer.DeserializeAsync<Config>(input, options) ?? throw new Exception($"Error loading {path}");

		ThrowIfNullOrEmpty(config.ConnectionString);
		ThrowIfNullOrEmpty(config.DatabaseId);
		ThrowIfNullOrEmpty(config.ContainerId);

		// Read the primary or secondary regions if not filled in
		if (string.IsNullOrWhiteSpace(config.PrimaryRegion) || string.IsNullOrWhiteSpace(config.SecondaryRegion))
		{
			using CosmosClient tempClient = new(config.ConnectionString);

			AccountProperties account = await tempClient.ReadAccountAsync();

			string primaryRegion = account.WritableRegions.First().Name;
			string secondaryRegion = account.ReadableRegions.Where(x => !primaryRegion.Equals(x.Name, StringComparison.OrdinalIgnoreCase))
															.First().Name;
			if (!string.IsNullOrWhiteSpace(config.PrimaryRegion))
				primaryRegion = config.PrimaryRegion;

			if (!string.IsNullOrWhiteSpace(config.SecondaryRegion))
				secondaryRegion = config.SecondaryRegion;

			config = new Config(config)
			{
				PrimaryRegion = primaryRegion,
				SecondaryRegion = secondaryRegion
			};
		}
		return config;
	}

	private static void ThrowIfNullOrEmpty(string? value, [CallerArgumentExpression("value")] string name = "")
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new Exception($"Missing '{name}' in config!");
	}

	public Container GetContainer(Region region, bool allowBulk = false)
		=> new CosmosClient(ConnectionString, new CosmosClientOptions
		{
			AllowBulkExecution = allowBulk,
			ConnectionMode = ConnectionMode.Direct,
			ApplicationPreferredRegions = new[]{
				region switch {
					Region.Primary => PrimaryRegion,
					Region.Secondary => SecondaryRegion,
					_ => throw new Exception("Unknown region option")
				}
			},
			SerializerOptions = new CosmosSerializationOptions {
				PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
			}
		}).GetContainer(DatabaseId, ContainerId);
}

public enum Region
{
	Primary,
	Secondary
}