record SampleItem(string Id, string Value, string Payload)
{
	private static readonly Random _r = new(123);
	
	private static string GetRandomString(int length)
	{
		byte[] buffer = new byte[length / 2];
		_r.NextBytes(buffer);
		return Convert.ToHexString(buffer);
	}

	public static SampleItem New(string id)
		=> new(id, Guid.NewGuid().ToString(), "");

	public static SampleItem New(string id, int payloadLength)
		=> new(id, Guid.NewGuid().ToString(), GetRandomString(payloadLength));

	public static SampleItem New(int payloadLength)
		=> new(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), GetRandomString(payloadLength));
}