using PbHtmlBuilder.Host.Configuration;

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

        return services;
    }
}
