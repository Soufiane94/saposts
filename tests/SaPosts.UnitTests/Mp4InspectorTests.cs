using System.Buffers.Binary;
using System.Text;

using SaPosts.Web.Media;

namespace SaPosts.UnitTests;

public sealed class Mp4InspectorTests
{
    [Fact]
    public void A_faststart_file_has_its_index_first()
    {
        using var file = Mp4("ftyp", "moov", "mdat");

        Assert.False(Mp4Inspector.MovieBoxIsAfterMediaData(file));
    }

    [Fact]
    public void Detects_an_index_written_after_the_media_data()
    {
        using var file = Mp4("ftyp", "mdat", "moov");

        Assert.True(Mp4Inspector.MovieBoxIsAfterMediaData(file));
    }

    [Fact]
    public void Follows_64_bit_box_sizes()
    {
        using var file = new MemoryStream();
        file.Write(Box("ftyp", 8));
        // mdat with size 1 and a 64-bit largesize of 16 + 8 payload bytes.
        var large = new byte[24];
        BinaryPrimitives.WriteUInt32BigEndian(large, 1);
        Encoding.ASCII.GetBytes("mdat").CopyTo(large, 4);
        BinaryPrimitives.WriteUInt64BigEndian(large.AsSpan(8), 24);
        file.Write(large);
        file.Write(Box("moov", 8));

        Assert.True(Mp4Inspector.MovieBoxIsAfterMediaData(file));
    }

    [Fact]
    public void Stops_on_a_malformed_box_instead_of_looping()
    {
        using var file = new MemoryStream();
        file.Write(Box("ftyp", 8));
        file.Write([0, 0, 0, 4, (byte)'m', (byte)'d', (byte)'a', (byte)'t']); // size smaller than its own header

        Assert.False(Mp4Inspector.MovieBoxIsAfterMediaData(file));
    }

    private static MemoryStream Mp4(params string[] boxes)
    {
        var file = new MemoryStream();
        foreach (var box in boxes)
        {
            file.Write(Box(box, 16));
        }

        return file;
    }

    private static byte[] Box(string type, int size)
    {
        var box = new byte[size];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)size);
        Encoding.ASCII.GetBytes(type).CopyTo(box, 4);
        return box;
    }
}
