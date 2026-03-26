namespace FileSorting.Sorting.Infrastructure.TempWorkspaces;

public interface ITemporaryWorkspace : IDisposable
{
    string RootPath { get; }
}