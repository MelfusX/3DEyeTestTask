using System.Text;

namespace FileSorting.Sorting.Infrastructure.IO;

public interface IFileWriter
{
    Task WriteAsync(
        string destinationPath,
        Encoding encoding,
        Func<StreamWriter, CancellationToken, Task> writeAction,
        CancellationToken cancellationToken);

    Task CopyIntoPlaceAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken);

    void TryDeleteFile(string path);
}
