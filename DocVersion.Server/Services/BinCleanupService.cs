namespace DocVersion.Server.Services;

public class BinCleanupService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer? _timer;

    public BinCleanupService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(DoCleanup, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        return Task.CompletedTask;
    }

    private void DoCleanup(object? state)
    {
        _ = CleanupAsync();
    }

    private async Task CleanupAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var fileService = scope.ServiceProvider.GetRequiredService<FileService>();
        await fileService.CleanExpiredBinItemsAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}