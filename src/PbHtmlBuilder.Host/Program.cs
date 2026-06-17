using PbHtmlBuilder.Host;
using PbHtmlBuilder.Host.Endpoints;
using PbHtmlBuilder.UI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPbHtmlBuilderHost(builder.Configuration);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthEndpoints();
app.MapFolderEndpoints();
app.MapImportEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
