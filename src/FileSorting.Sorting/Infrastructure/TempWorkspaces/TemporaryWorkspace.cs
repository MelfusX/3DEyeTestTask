using Microsoft.Extensions.Logging;

namespace FileSorting.Sorting.Infrastructure.TempWorkspaces;

internal sealed class TemporaryWorkspace : ITemporaryWorkspace
{
    private readonly ILogger<TemporaryWorkspace> _logger;
    private int _disposed;

    public TemporaryWorkspace(string rootPath, ILogger<TemporaryWorkspace> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        RootPath = Path.GetFullPath(rootPath);
        _logger = logger;

        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temp directory: {Path}", RootPath);
        }
    }
}