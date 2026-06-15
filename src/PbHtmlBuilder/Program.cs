using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Pb.Builder.Application.Commands;
using Pb.Builder.Application.Extensibility;
using Pb.Builder.Application.Ports;
using Pb.Builder.Application.Services;
using Pb.Builder.Application.Session;
using Pb.Builder.Application.UseCases;
using Pb.Builder.Infrastructure.Windows.Configuration;
using Pb.Builder.Infrastructure.Windows.Dialogs;
using Pb.Builder.Infrastructure.Windows.FileSystem;
using PbHtmlBuilder.Components;
using PbHtmlBuilder.Services;

var builder = WebApplication.CreateBuilder(args);

StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

var localAppOptions = new LocalAppOptions();
builder.Configuration.GetSection(LocalAppOptions.SectionName).Bind(localAppOptions);
LocalAppOptions.ApplyLegacyConfiguration(localAppOptions, builder.Configuration);
LocalAppOptions.ApplyDefaults(localAppOptions, builder.Environment);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var dataProtectionDirectory = Directory.CreateDirectory(
    Path.Combine(localAppOptions.DataRoot, "DataProtectionKeys"));

builder.Services.AddOptions<LocalAppOptions>()
    .Bind(builder.Configuration.GetSection(LocalAppOptions.SectionName))
    .PostConfigure(options =>
    {
        LocalAppOptions.ApplyLegacyConfiguration(options, builder.Configuration);
        LocalAppOptions.ApplyDefaults(options, builder.Environment);
    });
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(dataProtectionDirectory);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton(localAppOptions);
builder.Services.AddSingleton(DocumentTypeRegistry.CreateDefault());
builder.Services.AddSingleton<DocumentStore>();
builder.Services.AddSingleton<CommandDispatcher>();
builder.Services.AddSingleton<NewDocumentUseCase>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IIdGenerator, GuidIdGenerator>();
builder.Services.AddSingleton<IFileDialogService, WindowsFileDialogService>();
builder.Services.AddSingleton<IProjectPathService, WindowsProjectPathService>();
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
    .AddAdditionalAssemblies(typeof(Pb.Builder.UI.Components.Pages.Home).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
