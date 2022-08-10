namespace CosmosDBReplication;

public class PauseLoopTask<T>
{
	private readonly Func<T, CancellationToken, Task> _func;
	private volatile TaskCompletionSource<bool> _tcs = new();
	public PauseLoopTask(Func<T, CancellationToken, Task> func)
	{
		_func = func;
	}
	public async Task RunAsync(T config, CancellationToken cts)
	{
		cts.Register(Enable);
		while (!cts.IsCancellationRequested)
		{
			await _tcs.Task;
			if (!cts.IsCancellationRequested)
				await _func(config, cts);
		}
	}

	public void Disable()
	{
		while (true)
		{
			var tcs = _tcs;
			if (!tcs.Task.IsCompleted ||
				Interlocked.CompareExchange(ref _tcs, new TaskCompletionSource<bool>(), tcs) == tcs)
				return;
		}
	}

	public void Enable()
		=> _tcs.TrySetResult(true);
}