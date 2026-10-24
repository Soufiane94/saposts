using SaPosts.Web.Infrastructure;
using SaPosts.Web.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services
    .AddOptions<SiteOptions>()
    .BindConfiguration(SiteOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseRequestLocalization("fr-FR");

app.MapStaticAssets();
app.MapHealthChecks("/healthz");
app.MapRazorPages().WithStaticAssets();

app.Run();
