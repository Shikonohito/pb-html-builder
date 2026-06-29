using PbHtmlBuilder.Host.Configuration;
using PbHtmlBuilder.Application.Projects;
using PbHtmlBuilder.Application.Theory;
using PbHtmlBuilder.Artifacts.Renderers;
using PbHtmlBuilder.Infrastructure.Projects;

namespace PbHtmlBuilder.Host;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPbHtmlBuilderHost(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BuilderOptions>(configuration.GetSection(BuilderOptions.SectionName));
        services.Configure<WorkingDirectoryOptions>(configuration.GetSection($"{BuilderOptions.SectionName}:WorkingDirectory"));
        services.Configure<RuntimeAssetOptions>(configuration.GetSection($"{BuilderOptions.SectionName}:RuntimeAssets"));
        services.Configure<DependencyVersionOptions>(configuration.GetSection($"{BuilderOptions.SectionName}:Dependencies"));
        services.Configure<BrandDefaultsOptions>(configuration.GetSection($"{BuilderOptions.SectionName}:Defaults"));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITheoryHtmlRenderer, TheoryHtmlRenderer>();
        services.AddSingleton<IProjectFileStorage>(serviceProvider =>
        {
            var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
            var workingDirectory = configuration
                .GetSection($"{BuilderOptions.SectionName}:WorkingDirectory")
                .Get<WorkingDirectoryOptions>() ?? new WorkingDirectoryOptions();

            return new WorkingDirectoryProjectFileStorage(
                ResolveWorkingDirectoryRoot(environment, workingDirectory),
                serviceProvider.GetRequiredService<TimeProvider>());
        });
        services.AddSingleton(serviceProvider =>
        {
            var runtimeAssets = configuration
                .GetSection($"{BuilderOptions.SectionName}:RuntimeAssets")
                .Get<RuntimeAssetOptions>() ?? new RuntimeAssetOptions();
            var dependencies = configuration
                .GetSection($"{BuilderOptions.SectionName}:Dependencies")
                .Get<DependencyVersionOptions>() ?? new DependencyVersionOptions();
            var defaults = configuration
                .GetSection($"{BuilderOptions.SectionName}:Defaults")
                .Get<BrandDefaultsOptions>() ?? new BrandDefaultsOptions();

            return new TheoryDocumentFactory(
                new TheoryDocumentFactoryOptions
                {
                    BuilderVersion = typeof(ServiceCollectionExtensions).Assembly.GetName().Version?.ToString() ?? "dev",
                    Brand = new(defaults.TopicKicker, defaults.Copyright, defaults.BrandName),
                    Runtime = new(
                        runtimeAssets.RuntimeChannel,
                        runtimeAssets.ExportBaseUrl,
                        dependencies.MonacoVersion,
                        dependencies.MonacoBaseUrl,
                        dependencies.PyodideVersion,
                        dependencies.PyodideBaseUrl)
                },
                serviceProvider.GetRequiredService<TimeProvider>());
        });
        services.AddScoped<TheoryProjectSaveUseCase>();

        return services;
    }

    private static string ResolveWorkingDirectoryRoot(
        IWebHostEnvironment environment,
        WorkingDirectoryOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.RootPath)
            && !options.RootMode.Equals("AppDirectory", StringComparison.OrdinalIgnoreCase))
        {
            return options.RootPath;
        }

        return environment.ContentRootPath;
    }
}
