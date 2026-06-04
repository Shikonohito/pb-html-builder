using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using PbHtmlBuilder.Components;
using PbHtmlBuilder.Services;

var builder = WebApplication.CreateBuilder(args);

StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var dataProtectionDirectory = Directory.CreateDirectory(
    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys"));

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(dataProtectionDirectory);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<FolderDialogService>();
builder.Services.AddSingleton<ProjectDraftStore>();
builder.Services.AddHostedService<LocalBrowserLauncher>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
