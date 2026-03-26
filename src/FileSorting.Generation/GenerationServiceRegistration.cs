using FileSorting.Generation.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileSorting.Generation;

public static class GenerationServiceRegistration
{
    public static IServiceCollection AddGenerationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        long? fileSizeMb = null,
        int? maxNumber = null)
    {
        var section = configuration.GetSection(GenerationOptions.SectionName);

        services.AddSingleton(new GenerationOptions(
            fileSizeMb: fileSizeMb ?? section.GetValue<long>("FileSizeMb"),
            maxNumber: maxNumber ?? section.GetValue<int>("MaxNumber"),
            stringPool: section.GetSection("StringPool").Get<string[]>() ?? []));

        services.AddSingleton<FileGenerationService>();

        return services;
    }
}
