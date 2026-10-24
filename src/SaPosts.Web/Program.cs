using System.Text.Encodings.Web;
using System.Text.Unicode;

using Microsoft.Extensions.WebEncoders;

using SaPosts.Web.Auth;
using SaPosts.Web.Data;
using SaPosts.Web.Infrastructure;
using SaPosts.Web.Infrastructure.Security;
using SaPosts.Web.Posts;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services
    .AddOptions<SiteOptions>()
    .BindConfiguration(SiteOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddBlogData();
builder.Services.AddAdminAuthentication();
builder.Services.AddPosts();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SiteTime>();

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
// The default encoder escapes every non-ASCII character ("é" becomes "&#xE9;"); French text should stay readable.
builder.Services.Configure<WebEncoderOptions>(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services
    .AddRazorPages(options =>
    {
        options.Conventions.AuthorizeFolder("/Admin");
        options.Conventions.AllowAnonymousToPage("/Admin/Login");
        // Home pagination as /page/2 ("page" is reserved as a route value in Razor Pages, so no ?page=2).
        options.Conventions.AddPageRoute("/Index", "page/{pageNumber:int}");
    })
    // Server-side validation only: no validation scripts, so no data-val-* attributes either.
    .AddViewOptions(options => options.HtmlHelperOptions.ClientValidationEnabled = false);

builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

var app = builder.Build();

await app.InitializeDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseRequestLocalization("fr-FR");

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/healthz");
app.MapRazorPages().WithStaticAssets();

await app.RunAsync();
