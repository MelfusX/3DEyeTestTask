using Microsoft.Extensions.Logging;

namespace FileSorting.Sorting.Infrastructure.TempWorkspaces;

internal sealed class TemporaryWorkspaceFactory : ITemporaryWorkspaceFactory
{
    private readonly ILogger<TemporaryWorkspace> _logger;

    public TemporaryWorkspaceFactory(ILogger<TemporaryWorkspace> logger)
    {
        _logger = logger;
    }

    public ITemporaryWorkspace Create(string? basePath)
    {
        var rootBasePath = !string.IsNullOrWhiteSpace(basePath) ? basePath : Path.GetTempPath();

        var rootPath = Path.Combine(rootBasePath, "FileSorting_" + Guid.NewGuid().ToString("N")[..8]);

        return new TemporaryWorkspace(rootPath, _logger);
    }
}