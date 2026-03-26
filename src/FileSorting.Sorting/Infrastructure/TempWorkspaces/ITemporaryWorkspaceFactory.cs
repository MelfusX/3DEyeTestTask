namespace FileSorting.Sorting.Infrastructure.TempWorkspaces;

public interface ITemporaryWorkspaceFactory
{
    ITemporaryWorkspace Create(string? basePath);
}