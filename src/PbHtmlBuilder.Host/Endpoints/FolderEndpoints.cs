using PbHtmlBuilder.Application.Projects;

namespace PbHtmlBuilder.Host.Endpoints;

public static class FolderEndpoints
{
    public static IEndpointRouteBuilder MapFolderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/folders");

        group.MapGet("/browse", (string? path, IProjectFolderBrowser browser) =>
            Results.Ok(browser.Browse(path)));

        return endpoints;
    }
}
