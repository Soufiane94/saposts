using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace SaPosts.Web.Media;

public static class MediaEndpoints
{
    public sealed record UploadResponse(Guid Id, string Kind, string Markdown, string Label, string? ThumbnailUrl, string? Warning);

    public static IEndpointRouteBuilder MapMedia(this IEndpointRouteBuilder app)
    {
        app.MapGet("/media/{id:guid}/{fileName}", ServeAsync);
        app.MapPost("/admin/api/media", UploadAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> ServeAsync(Guid id, string fileName, IMediaStorage storage, HttpContext context, CancellationToken cancellationToken)
    {
        if (!MediaFiles.TryGetContentType(fileName, out var contentType)
            || await storage.OpenReadAsync(id, fileName, cancellationToken) is not { } file)
        {
            return Results.NotFound();
        }

        // A stored file never changes: a new upload gets a new id.
        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return Results.File(
            file.Content,
            contentType,
            lastModified: file.LastModified,
            entityTag: new EntityTagHeaderValue($"\"{id:N}-{fileName}\""),
            enableRangeProcessing: true);
    }

    private static async Task<IResult> UploadAsync(HttpContext context, MediaUploader uploader, IAntiforgery antiforgery, CancellationToken cancellationToken)
    {
        // The token comes in the RequestVerificationToken header, so validating it does not read the body.
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem("Jeton de sécurité manquant ou expiré : rechargez la page.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Kestrel's default 30 MB limit is raised for this endpoint only; per-type limits apply while streaming.
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = uploader.MaxRequestBytes;
        }

        var result = await uploader.UploadAsync(context.Request, cancellationToken);
        if (result.Item is not { } item)
        {
            return Results.Problem(result.Error, statusCode: result.StatusCode);
        }

        var isImage = item.Kind == MediaKind.Image;
        return Results.Ok(new UploadResponse(
            item.Id,
            isImage ? "image" : "video",
            $"![{EscapeAlt(item.AltText)}]({MediaFiles.Reference(item.Id)})",
            item.AltText ?? item.OriginalFileName,
            isImage ? MediaMarkup.ThumbnailUrl(item) : null,
            result.Warning));
    }

    // Brackets would end the alt text early in Markdown.
    private static string EscapeAlt(string? alt) => (alt ?? string.Empty).Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal);
}
