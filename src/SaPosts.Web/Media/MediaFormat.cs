using System.Buffers.Binary;
using System.Text;

namespace SaPosts.Web.Media;

public enum MediaFormat
{
    Unknown,
    Jpeg,
    Png,
    Gif,
    Webp,
    Avif,
    Mp4,
    Webm,
}

public static class MediaFormats
{
    /// <summary>Enough bytes to read the signature, including an MP4/AVIF brand list or a WebM DocType.</summary>
    public const int SniffLength = 256;

    private static readonly string[] Mp4Brands = ["isom", "iso2", "iso3", "iso4", "iso5", "iso6", "mp41", "mp42", "avc1", "dash", "M4V ", "mmp4"];

    public static bool IsImage(this MediaFormat format) => format is MediaFormat.Jpeg or MediaFormat.Png or MediaFormat.Gif or MediaFormat.Webp or MediaFormat.Avif;

    public static bool IsVideo(this MediaFormat format) => format is MediaFormat.Mp4 or MediaFormat.Webm;

    public static string ContentType(this MediaFormat format) => format switch
    {
        MediaFormat.Jpeg => "image/jpeg",
        MediaFormat.Png => "image/png",
        MediaFormat.Gif => "image/gif",
        MediaFormat.Webp => "image/webp",
        MediaFormat.Avif => "image/avif",
        MediaFormat.Mp4 => "video/mp4",
        MediaFormat.Webm => "video/webm",
        _ => "application/octet-stream",
    };

    /// <summary>Identifies a file from its first bytes. Names and declared content types are never trusted.</summary>
    public static MediaFormat Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return MediaFormat.Jpeg;
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return MediaFormat.Png;
        }

        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
        {
            return MediaFormat.Gif;
        }

        if (header.Length >= 12 && header.StartsWith("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return MediaFormat.Webp;
        }

        if (header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8))
        {
            return DetectIsoMedia(header);
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0x1A, 0x45, 0xDF, 0xA3]))
        {
            return DetectMatroska(header);
        }

        return MediaFormat.Unknown;
    }

    // ISO base media (MP4, AVIF, HEIC, QuickTime): decided by the major and compatible brands of the ftyp box.
    private static MediaFormat DetectIsoMedia(ReadOnlySpan<byte> header)
    {
        var boxSize = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(header), (uint)header.Length);
        var major = Encoding.ASCII.GetString(header[8..12]);
        var brands = new List<string> { major };
        for (var offset = 16; offset + 4 <= boxSize; offset += 4)
        {
            brands.Add(Encoding.ASCII.GetString(header.Slice(offset, 4)));
        }

        if (brands.Contains("avif") || brands.Contains("avis"))
        {
            return MediaFormat.Avif;
        }

        // QuickTime (.mov) and audio-only MP4 are not accepted, even when they list MP4 brands.
        if (major is "qt  " or "M4A " or "M4B ")
        {
            return MediaFormat.Unknown;
        }

        return brands.Any(Mp4Brands.Contains) ? MediaFormat.Mp4 : MediaFormat.Unknown;
    }

    // EBML header: WebM is Matroska with DocType "webm"; generic Matroska (.mkv) is not accepted.
    private static MediaFormat DetectMatroska(ReadOnlySpan<byte> header)
    {
        ReadOnlySpan<byte> docTypeId = [0x42, 0x82];
        var index = header.IndexOf(docTypeId);
        if (index < 0 || index + 3 > header.Length)
        {
            return MediaFormat.Unknown;
        }

        // One-byte EBML size (0x80 | length) is what encoders write for "webm".
        var sizeByte = header[index + 2];
        var length = sizeByte & 0x7F;
        if ((sizeByte & 0x80) == 0 || index + 3 + length > header.Length)
        {
            return MediaFormat.Unknown;
        }

        return header.Slice(index + 3, length).SequenceEqual("webm"u8) ? MediaFormat.Webm : MediaFormat.Unknown;
    }
}
