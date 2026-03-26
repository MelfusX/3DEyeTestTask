using System.CommandLine;
using System.Diagnostics;
using FileSorting.Generation;
using FileSorting.Generation.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var outputOption = new Option<string>(
    aliases: ["--output", "-o"],
    description: "Output file path") { IsRequired = true };

var sizeOption = new Option<long?>(
    aliases: ["--size", "-s"],
    description: "Target file size in MB");

var maxNumberOption = new Option<int?>(
    aliases: ["--max-number", "-n"],
    description: "Maximum number value");

var rootCommand = new RootCommand("Test File Generator - generates test files for sorting")
{
    outputOption, sizeOption, maxNumberOption
};

rootCommand.SetHandler(async (output, size, maxNumber) =>
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });

    builder.Services.AddGenerationServices(builder.Configuration, fileSizeMb: size, maxNumber: maxNumber);

    using var host = builder.Build();
    var generationService = host.Services.GetRequiredService<FileGenerationService>();
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GeneratorApp");
    var generationOptions = host.Services.GetRequiredService<GenerationOptions>();

    var targetBytes = generationOptions.FileSizeMb * 1024L * 1024L;
    logger.LogInformation("Starting file generation: {Path}, target size: {Size}MB", output, generationOptions.FileSizeMb);

    var sw = Stopwatch.StartNew();
    await generationService.GenerateAsync(output, targetBytes, cts.Token);
    sw.Stop();

    logger.LogInformation("File generated in {Elapsed}", sw.Elapsed);
}, outputOption, sizeOption, maxNumberOption);

await rootCommand.InvokeAsync(args);
