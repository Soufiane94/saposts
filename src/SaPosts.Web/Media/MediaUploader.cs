using System.Buffers;
using System.Globalization;
using System.Text;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

using SaPosts.Web.Data;

namespace SaPosts.Web.Media;

public sealed record MediaUploadResult(int StatusCode, MediaItem? Item = null, string? Warning = null, string? Error = null)
{
    public static MediaUploadResult Fail(int statusCode, string error) => new(statusCode, Error: error);
}

/// <summary>
/// Receives one file from a multipart request without buffering it: form fields first (alt text,
/// video dimensions), then the file, streamed to a staging file while its type is detected from its
/// first bytes and its size checked against the limit for that type.
/// </summary>
public sealed class MediaUploader(
    BlogDbContext db,
    IMediaStorage storage,
    MediaStaging staging,
    ImageProcessor images,
    IOptions<MediaOptions> options,
    TimeProvider clock)
{
    private const int MaxFieldLength = 1000;
    private const int MaxAltTextLength = 500;

    public long MaxRequestBytes => Math.Max(options.Value.MaxImageBytes, options.Value.MaxVideoBytes) + (1024 * 1024);

    public async Task<MediaUploadResult> UploadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType)
            || !contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || HeaderUtilities.RemoveQuotes(contentType.Boundary).Value is not { Length: > 0 } boundary)
        {
            return MediaUploadResult.Fail(StatusCodes.Status415UnsupportedMediaType, "Envoi multipart/form-data attendu.");
        }

        var reader = new MultipartReader(boundary, request.Body) { HeadersLengthLimit = 16 * 1024, BodyLengthLimit = MaxRequestBytes };
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var section = await reader.ReadNextSectionAsync(cancellationToken); section is not null; section = await reader.ReadNextSectionAsync(cancellationToken))
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
            {
                continue;
            }

            if (disposition.IsFileDisposition())
            {
                var fileName = Path.GetFileName(HeaderUtilities.RemoveQuotes(disposition.FileName).Value ?? "fichier");
                return await ReceiveFileAsync(section.Body, fileName[..Math.Min(fileName.Length, 255)], fields, cancellationToken);
            }

            if (disposition.IsFormDisposition() && fields.Count < 10)
            {
                var value = await ReadFieldAsync(section.Body, cancellationToken);
                if (value is null)
                {
                    return MediaUploadResult.Fail(StatusCodes.Status400BadRequest, "Champ de formulaire trop long.");
                }

                fields[HeaderUtilities.RemoveQuotes(disposition.Name).Value ?? string.Empty] = value;
            }
        }

        return MediaUploadResult.Fail(StatusCodes.Status400BadRequest, "Aucun fichier reçu.");
    }

    private async Task<MediaUploadResult> ReceiveFileAsync(Stream body, string fileName, Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var path = staging.CreatePath();
        try
        {
            var (format, size, error) = await StageAsync(body, path, fields, cancellationToken);
            if (error is not null)
            {
                return error;
            }

            return format.IsImage()
                ? await StoreImageAsync(path, format, fileName, size, fields["alt"].Trim(), cancellationToken)
                : await StoreVideoAsync(path, format, fileName, size, fields, cancellationToken);
        }
        finally
        {
            MediaStaging.Delete(path); // no-op once a video has been moved into storage
        }
    }

    private async Task<(MediaFormat Format, long Size, MediaUploadResult? Error)> StageAsync(
        Stream body, string path, Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        var header = new byte[MediaFormats.SniffLength];
        var headerLength = 0;
        var format = (MediaFormat?)null;
        long size = 0;
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            int read;
            while ((read = await body.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                size += read;
                if (format is null)
                {
                    var take = Math.Min(read, header.Length - headerLength);
                    buffer.AsSpan(0, take).CopyTo(header.AsSpan(headerLength));
                    headerLength += take;
                    if (headerLength == header.Length)
                    {
                        format = MediaFormats.Detect(header);
                        if (Reject(format.Value, fields) is { } rejection)
                        {
                            return (format.Value, size, rejection);
                        }
                    }
                }

                if (size > (format is { } known ? LimitFor(known) : MaxRequestBytes))
                {
                    return (format ?? MediaFormat.Unknown, size, TooLarge(format ?? MediaFormat.Unknown));
                }

                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (format is null)
            {
                // Files shorter than the sniffing window.
                format = MediaFormats.Detect(header.AsSpan(0, headerLength));
                if (Reject(format.Value, fields) is { } rejection)
                {
                    return (format.Value, size, rejection);
                }
            }

            return (format.Value, size, null);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static MediaUploadResult? Reject(MediaFormat format, Dictionary<string, string> fields)
    {
        if (format == MediaFormat.Unknown)
        {
            return MediaUploadResult.Fail(
                StatusCodes.Status415UnsupportedMediaType,
                "Format non accepté. Images : JPEG, PNG, WebP, AVIF, GIF. Vidéos : MP4 ou WebM.");
        }

        if (format.IsImage() && string.IsNullOrWhiteSpace(fields.GetValueOrDefault("alt")))
        {
            return MediaUploadResult.Fail(StatusCodes.Status400BadRequest, "Le texte alternatif est obligatoire pour une image.");
        }

        if (fields.GetValueOrDefault("alt")?.Trim().Length > MaxAltTextLength)
        {
            return MediaUploadResult.Fail(StatusCodes.Status400BadRequest, $"Le texte alternatif ne doit pas dépasser {MaxAltTextLength} caractères.");
        }

        return null;
    }

    private long LimitFor(MediaFormat format) => format.IsImage() ? options.Value.MaxImageBytes : options.Value.MaxVideoBytes;

    private MediaUploadResult TooLarge(MediaFormat format) =>
        MediaUploadResult.Fail(
            StatusCodes.Status413PayloadTooLarge,
            format.IsImage()
                ? $"Image trop lourde ({FormatSize(options.Value.MaxImageBytes)} au maximum)."
                : $"Fichier trop lourd ({FormatSize(options.Value.MaxVideoBytes)} au maximum pour une vidéo).");

    private async Task<MediaUploadResult> StoreImageAsync(
        string path, MediaFormat format, string fileName, long size, string altText, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7();
        ProcessedImage processed;
        try
        {
            processed = await images.ProcessAsync(id, path, format, cancellationToken);
        }
        catch (ImageRejectedException e)
        {
            return MediaUploadResult.Fail(StatusCodes.Status422UnprocessableEntity, e.Message);
        }

        var item = new MediaItem
        {
            Id = id,
            Kind = MediaKind.Image,
            OriginalFileName = fileName,
            ContentType = format.ContentType(),
            SizeBytes = size,
            Width = processed.Width,
            Height = processed.Height,
            Widths = processed.Widths,
            HasAlpha = processed.HasAlpha,
            IsAnimated = processed.IsAnimated,
            AltText = altText,
            UploadedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Media.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return new MediaUploadResult(StatusCodes.Status200OK, item);
    }

    private async Task<MediaUploadResult> StoreVideoAsync(
        string path, MediaFormat format, string fileName, long size, Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        string? warning = null;
        if (format == MediaFormat.Mp4)
        {
            await using var file = File.OpenRead(path);
            if (Mp4Inspector.MovieBoxIsAfterMediaData(file))
            {
                warning = "Cette vidéo n’est pas optimisée pour la lecture en continu : elle démarrera plus lentement. " +
                    "Réexportez-la avec l’option « faststart » (ou « optimisée pour le Web »).";
            }
        }

        // Reported by the browser that read the file; only used to reserve space and avoid layout shifts.
        var (width, height) = (Dimension(fields, "width"), Dimension(fields, "height"));
        if (width is null || height is null)
        {
            (width, height) = (null, null);
        }

        var item = new MediaItem
        {
            Id = Guid.CreateVersion7(),
            Kind = MediaKind.Video,
            OriginalFileName = fileName,
            ContentType = format.ContentType(),
            SizeBytes = size,
            Width = width,
            Height = height,
            AltText = fields.GetValueOrDefault("alt")?.Trim() is { Length: > 0 } label ? label : null,
            UploadedAt = clock.GetUtcNow().UtcDateTime,
        };

        await storage.ImportAsync(item.Id, MediaFiles.Original(item), path, cancellationToken);
        db.Media.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return new MediaUploadResult(StatusCodes.Status200OK, item, warning);
    }

    private static int? Dimension(Dictionary<string, string> fields, string name) =>
        int.TryParse(fields.GetValueOrDefault(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value is > 0 and <= 8192
            ? value
            : null;

    private static async Task<string?> ReadFieldAsync(Stream body, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(body, Encoding.UTF8);
        var buffer = new char[MaxFieldLength + 1];
        var length = await reader.ReadBlockAsync(buffer, cancellationToken);
        return length > MaxFieldLength ? null : new string(buffer, 0, length);
    }

    public static string FormatSize(long bytes) =>
        bytes < 1024 * 1024
            ? string.Create(CultureInfo.GetCultureInfo("fr-FR"), $"{Math.Max(1, bytes / 1024)} ko")
            : string.Create(CultureInfo.GetCultureInfo("fr-FR"), $"{bytes / (1024d * 1024):0.#} Mo");
}
