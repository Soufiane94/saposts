using System.Buffers.Binary;
using System.Text;

using NetVips;

namespace SaPosts.IntegrationTests;

/// <summary>Generated test files: real images encoded by libvips, and minimal video containers.</summary>
internal static class TestMedia
{
    public const string CameraMake = "TestCam";
    public const string XmpGpsMarker = "GPSLatitude";

    public static byte[] Jpeg(int width = 2000, int height = 1200) => Encode(Solid(width, height, 3), ".jpg");

    public static byte[] Png(int width = 600, int height = 400, bool alpha = true) => Encode(Solid(width, height, alpha ? 4 : 3), ".png");

    public static byte[] Webp() => Encode(Solid(900, 600, 3), ".webp");

    public static byte[] Avif() => Encode(Solid(900, 600, 3), ".avif");

    public static byte[] Gif() => Encode(Solid(300, 200, 3), ".gif");

    public static byte[] AnimatedGif(int frames = 3, int width = 120, int height = 80)
    {
        var images = Enumerable.Range(0, frames).Select(i => Solid(width, height, 3, shade: 60 * i)).ToArray();
        using var strip = Image.Arrayjoin(images, across: 1);
        using var animated = strip.Mutate(image => image.Set(GValue.GIntType, "page-height", height));
        foreach (var frame in images)
        {
            frame.Dispose();
        }

        return animated.WriteToBuffer(".gif");
    }

    /// <summary>A JPEG carrying an EXIF block (camera make + a GPS IFD) and an XMP packet with GPS coordinates.</summary>
    public static byte[] JpegWithLocation()
    {
        var jpeg = Jpeg(1000, 700);
        using var output = new MemoryStream();
        output.Write(jpeg, 0, 2); // SOI
        WriteApp1(output, [.. "Exif\0\0"u8, .. ExifTiff()]);
        WriteApp1(output, Encoding.UTF8.GetBytes(
            "http://ns.adobe.com/xap/1.0/\0<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'>" +
            $"<rdf:Description xmlns:exif='http://ns.adobe.com/exif/1.0/' exif:{XmpGpsMarker}='48,51.5N'/></rdf:RDF></x:xmpmeta>"));
        output.Write(jpeg, 2, jpeg.Length - 2);
        return output.ToArray();
    }

    public static bool ContainsMetadata(byte[] file) =>
        Contains(file, Encoding.ASCII.GetBytes(CameraMake))
        || Contains(file, Encoding.ASCII.GetBytes(XmpGpsMarker))
        || Contains(file, "Exif\0\0"u8.ToArray());

    /// <summary>ftyp + moov + mdat, with the index before or after the media data.</summary>
    public static byte[] Mp4(bool fastStart, string majorBrand = "isom")
    {
        using var file = new MemoryStream();
        var ftyp = Box("ftyp", 16 + 8);
        Encoding.ASCII.GetBytes(majorBrand).CopyTo(ftyp, 8);
        Encoding.ASCII.GetBytes("isommp42").CopyTo(ftyp, 16);
        file.Write(ftyp);
        var boxes = fastStart ? new[] { Box("moov", 64), Box("mdat", 4096) } : [Box("mdat", 4096), Box("moov", 64)];
        foreach (var box in boxes)
        {
            file.Write(box);
        }

        return file.ToArray();
    }

    /// <summary>An EBML header with DocType "webm", padded with arbitrary data.</summary>
    public static byte[] Webm() =>
        [0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01, 0x42, 0x82, 0x84, .. "webm"u8, .. new byte[2048]];

    private static Image Solid(int width, int height, int bands, int shade = 0)
    {
        double[] colour = bands == 4 ? [180 - shade, 90, 40 + shade, 128] : [180 - shade, 90, 40 + shade];
        using var black = Image.Black(width, height, bands);
        using var filled = black + colour;
        var image = filled.Cast(Enums.BandFormat.Uchar);
        return bands == 4 ? image.Copy(interpretation: Enums.Interpretation.Srgb) : image;
    }

    private static byte[] Encode(Image image, string format)
    {
        using (image)
        {
            return image.WriteToBuffer(format);
        }
    }

    private static void WriteApp1(Stream output, byte[] payload)
    {
        Span<byte> header = [0xFF, 0xE1, 0, 0];
        BinaryPrimitives.WriteUInt16BigEndian(header[2..], (ushort)(payload.Length + 2));
        output.Write(header);
        output.Write(payload);
    }

    // Little-endian TIFF: IFD0 { Make, GPS IFD pointer } → GPS IFD { GPSLatitudeRef = "N" }, then the Make string.
    private static byte[] ExifTiff()
    {
        var tiff = new byte[64];
        "II"u8.CopyTo(tiff);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(2), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(4), 8);

        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(8), 2);
        WriteEntry(tiff, 10, tag: 0x010F, type: 2, count: 8, value: 56); // Make → offset 56
        WriteEntry(tiff, 22, tag: 0x8825, type: 4, count: 1, value: 38); // GPS IFD → offset 38
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(34), 0);

        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(38), 1);
        WriteEntry(tiff, 40, tag: 0x0001, type: 2, count: 2, value: 'N');
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(52), 0);

        Encoding.ASCII.GetBytes(CameraMake + "\0").CopyTo(tiff, 56);
        return tiff;
    }

    private static void WriteEntry(byte[] tiff, int offset, ushort tag, ushort type, uint count, uint value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(offset), tag);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(offset + 2), type);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(offset + 4), count);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(offset + 8), value);
    }

    private static byte[] Box(string type, int size)
    {
        var box = new byte[size];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)size);
        Encoding.ASCII.GetBytes(type).CopyTo(box, 4);
        return box;
    }

    private static bool Contains(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;
}
