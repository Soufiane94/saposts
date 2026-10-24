using NetVips;

using Image = NetVips.Image;

namespace SaPosts.Web.Media;

public sealed record ProcessedImage(int Width, int Height, bool HasAlpha, bool IsAnimated, List<int> Widths);

public sealed class ImageRejectedException(string message) : Exception(message);

/// <summary>
/// Turns an uploaded image into web variants with libvips: auto-rotated, converted to sRGB, every
/// metadata block removed (EXIF, GPS, XMP, IPTC, ICC), resized to a few widths, encoded as AVIF
/// and WebP plus a JPEG/PNG (GIF if animated) fallback, and a 1200×630 JPEG for link previews.
/// </summary>
public sealed partial class ImageProcessor(IMediaStorage storage, ILogger<ImageProcessor> logger)
{
    private static readonly int[] TargetWidths = [400, 800, 1200, 1600];
    private const int AnimatedMaxWidth = 800;
    private const long MaxPixels = 50_000_000; // decompression-bomb guard, all frames included
    private const int MaxFrames = 300;

    static ImageProcessor()
    {
        // Only the loaders of the accepted formats may run on uploads: SVG, PDF, TIFF and the
        // rest of what libvips can read stay blocked. Unknown names are ignored by libvips, so
        // the upload tests cover each accepted format.
        Operation.Block("VipsForeignLoad", true);
        foreach (var loader in new[] { "VipsForeignLoadJpeg", "VipsForeignLoadPng", "VipsForeignLoadNsgif", "VipsForeignLoadWebp", "VipsForeignLoadHeif" })
        {
            Operation.Block(loader, false);
        }

        // Each upload is processed once: caching operations would only hold memory.
        Cache.Max = 0;
    }

    public async Task<ProcessedImage> ProcessAsync(Guid id, string path, MediaFormat format, CancellationToken cancellationToken)
    {
        try
        {
            return await EncodeAsync(id, path, format, cancellationToken);
        }
        catch (VipsException e)
        {
            LogRejected(logger, e.Message);
            await storage.DeleteAsync(id, CancellationToken.None);
            throw new ImageRejectedException("Image illisible ou endommagée.");
        }
        catch (ImageRejectedException)
        {
            await storage.DeleteAsync(id, CancellationToken.None);
            throw;
        }
    }

    private async Task<ProcessedImage> EncodeAsync(Guid id, string path, MediaFormat format, CancellationToken cancellationToken)
    {
        var (width, height, frames, hasAlpha) = Probe(path);
        if ((long)width * height * frames > MaxPixels || frames > MaxFrames)
        {
            throw new ImageRejectedException("Image trop grande (50 mégapixels au maximum, toutes images d’une animation comprises).");
        }

        var animated = frames > 1 && format is MediaFormat.Gif or MediaFormat.Webp;
        var source = animated ? $"{path}[n=-1]" : path;
        List<int> widths = animated
            ? [Math.Min(width, AnimatedMaxWidth)]
            : [.. TargetWidths.Where(w => w < width).Append(Math.Min(width, TargetWidths[^1])).Distinct()];
        string[] extensions = animated ? ["webp", "gif"] : ["avif", "webp", hasAlpha ? "png" : "jpg"];

        int outputWidth = 0, outputHeight = 0;
        foreach (var target in widths)
        {
            // Thumbnail reads the file sequentially, so it can be encoded only once: keep the resized
            // pixels in memory (at most 1600 px wide) for the several encodes.
            using var thumbnail = Image.Thumbnail(source, target, size: Enums.Size.Down, failOn: Enums.FailOn.Error);
            using var resized = thumbnail.CopyMemory();
            (outputWidth, outputHeight) = (resized.Width, animated ? resized.PageHeight : resized.Height);
            foreach (var extension in extensions)
            {
                await SaveAsync(resized, id, MediaFiles.Variant(target, extension), extension, cancellationToken);
            }
        }

        // Link previews: JPEG is the format every social network accepts.
        using (var preview = Image.Thumbnail(path, 1200, height: 630, crop: Enums.Interesting.Attention, failOn: Enums.FailOn.Error))
        using (var opaque = preview.HasAlpha() ? preview.Flatten(background: [255, 255, 255]) : preview.Copy())
        {
            await SaveAsync(opaque, id, MediaFiles.OpenGraph, "jpg", cancellationToken);
        }

        return new ProcessedImage(outputWidth, outputHeight, hasAlpha, animated, widths);
    }

    private static (int Width, int Height, int Frames, bool HasAlpha) Probe(string path)
    {
        // Reads the header only; the pixels are decoded later, per variant.
        using var image = Image.NewFromFile(path, failOn: Enums.FailOn.Error);
        var frames = image.Contains("n-pages") ? (int)image.Get("n-pages") : 1;
        var quarterTurn = image.Contains("orientation") && (int)image.Get("orientation") >= 5;
        var (width, height) = quarterTurn ? (image.Height, image.Width) : (image.Width, image.Height);
        return (width, height, Math.Max(frames, 1), image.HasAlpha());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image rejected by libvips: {Reason}")]
    private static partial void LogRejected(ILogger logger, string reason);

    private Task SaveAsync(Image image, Guid id, string fileName, string extension, CancellationToken cancellationToken)
    {
        // keep=none drops EXIF (including GPS), XMP, IPTC and ICC; thumbnail already produced sRGB.
        var options = extension switch
        {
            "avif" => ".avif[Q=55,effort=3,keep=none]",
            "webp" => ".webp[Q=80,keep=none]",
            "jpg" => ".jpg[Q=82,interlace=true,optimize-coding=true,keep=none]",
            "png" => ".png[keep=none]",
            "gif" => ".gif[keep=none]",
            _ => throw new ArgumentOutOfRangeException(nameof(extension), extension, null),
        };

        using var buffer = new MemoryStream(image.WriteToBuffer(options), writable: false);
        return storage.WriteAsync(id, fileName, buffer, cancellationToken);
    }
}
