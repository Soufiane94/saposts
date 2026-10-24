using System.Buffers.Binary;
using System.Text;

using SaPosts.Web.Media;

namespace SaPosts.UnitTests;

public sealed class MediaFormatTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10 }, MediaFormat.Jpeg)]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, MediaFormat.Png)]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0 }, MediaFormat.Gif)]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 0x56 }, MediaFormat.Webp)]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x41, 0x56, 0x45 }, MediaFormat.Unknown)] // RIFF WAVE
    public void Detects_images_from_their_signature(byte[] header, MediaFormat expected) =>
        Assert.Equal(expected, MediaFormats.Detect(header));

    [Theory]
    [InlineData("avif", new[] { "mif1", "avif" }, MediaFormat.Avif)]
    [InlineData("mif1", new[] { "mif1", "avif", "miaf" }, MediaFormat.Avif)]
    [InlineData("isom", new[] { "isom", "iso2", "avc1", "mp41" }, MediaFormat.Mp4)]
    [InlineData("mp42", new[] { "mp42", "isom" }, MediaFormat.Mp4)]
    [InlineData("qt  ", new[] { "qt  " }, MediaFormat.Unknown)] // QuickTime .mov
    [InlineData("M4A ", new[] { "M4A ", "mp42", "isom" }, MediaFormat.Unknown)] // audio only
    [InlineData("heic", new[] { "mif1", "heic" }, MediaFormat.Unknown)] // iPhone photos: no HEVC decoder
    public void Tells_iso_media_files_apart_by_brand(string major, string[] compatible, MediaFormat expected) =>
        Assert.Equal(expected, MediaFormats.Detect(FtypBox(major, compatible)));

    [Theory]
    [InlineData("webm", MediaFormat.Webm)]
    [InlineData("matroska", MediaFormat.Unknown)]
    public void Accepts_webm_but_not_other_matroska_files(string docType, MediaFormat expected)
    {
        byte[] header = [0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01, 0x42, 0x82, (byte)(0x80 | docType.Length), .. Encoding.ASCII.GetBytes(docType)];

        Assert.Equal(expected, MediaFormats.Detect(header));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("%PDF-1.7")]
    [InlineData("MZ\u0090\0")]
    [InlineData("")]
    public void Rejects_everything_else(string content) =>
        Assert.Equal(MediaFormat.Unknown, MediaFormats.Detect(Encoding.UTF8.GetBytes(content)));

    internal static byte[] FtypBox(string major, string[] compatible)
    {
        var box = new byte[16 + (4 * compatible.Length)];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        Encoding.ASCII.GetBytes("ftyp").CopyTo(box, 4);
        Encoding.ASCII.GetBytes(major).CopyTo(box, 8);
        for (var i = 0; i < compatible.Length; i++)
        {
            Encoding.ASCII.GetBytes(compatible[i]).CopyTo(box, 16 + (4 * i));
        }

        return box;
    }
}
