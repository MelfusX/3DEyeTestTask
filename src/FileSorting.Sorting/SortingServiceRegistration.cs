using FileSorting.Sorting.Configuration;
using FileSorting.Sorting.Infrastructure.IO;
using FileSorting.Sorting.Infrastructure.TempWorkspaces;
using FileSorting.Sorting.Services.Merging;
using FileSorting.Sorting.Services.Splitting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileSorting.Sorting;

public static class SortingServiceRegistration
{
    public static IServiceCollection AddSortingServices(
        this IServiceCollection services,
        IConfiguration configuration,
        int? chunkSizeMb = null,
        int? mergeWayCount = null,
        string? tempDirectory = null)
    {
        var section = configuration.GetSection(SortingOptions.SectionName);

        services.AddSingleton(new SortingOptions(
            chunkSizeMb: chunkSizeMb ?? section.GetValue<int>("ChunkSizeMb"),
            mergeWayCount: mergeWayCount ?? section.GetValue<int>("MergeWayCount"),
            maxMergeBufferMb: section.GetValue<int>("MaxMergeBufferMb"),
            tempDirectory: tempDirectory ?? section.GetValue<string>("TempDirectory"),
            encodingName: section.GetValue<string>("EncodingName") ?? "utf-8"));

        services.AddSingleton<IFileWriter, AtomicFileWriter>();
        services.AddSingleton<ITemporaryWorkspaceFactory, TemporaryWorkspaceFactory>();
        services.AddSingleton<IChunkFileSplitter, ChunkFileSplitter>();
        services.AddSingleton<IChunkFileMerger, ChunkFileMerger>();
        services.AddSingleton<FileSortingService>();
        return services;
    }
}
