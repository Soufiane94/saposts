using System.Buffers.Binary;

namespace SaPosts.Web.Media;

/// <summary>Reads the top-level box layout of an MP4 file, without decoding anything.</summary>
public static class Mp4Inspector
{
    private const int MaxBoxes = 1000;

    /// <summary>
    /// True when the index ("moov") comes after the media data ("mdat"). Such files still play
    /// (range requests let the browser fetch the end first), but start more slowly.
    /// </summary>
    public static bool MovieBoxIsAfterMediaData(Stream file)
    {
        Span<byte> header = stackalloc byte[16];
        long position = 0;
        var sawMediaData = false;

        for (var count = 0; count < MaxBoxes && position + 8 <= file.Length; count++)
        {
            file.Position = position;
            if (file.ReadAtLeast(header[..8], 8, throwOnEndOfStream: false) < 8)
            {
                break;
            }

            long size = BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = header.Slice(4, 4);
            if (size == 1)
            {
                // 64-bit "largesize" follows the type.
                if (file.ReadAtLeast(header.Slice(8, 8), 8, throwOnEndOfStream: false) < 8)
                {
                    break;
                }

                size = (long)BinaryPrimitives.ReadUInt64BigEndian(header.Slice(8, 8));
            }
            else if (size == 0)
            {
                size = file.Length - position; // box extends to the end of the file
            }

            if (type.SequenceEqual("moov"u8))
            {
                return sawMediaData;
            }

            if (type.SequenceEqual("mdat"u8))
            {
                sawMediaData = true;
            }

            if (size < 8)
            {
                break; // malformed: stop rather than loop
            }

            position += size;
        }

        return false;
    }
}
