using System.Text;

namespace Kyklos.Engine;

/// <summary>Reads how long a WAV file plays from its header, without decoding it.</summary>
public static class WaveFile
{
    /// <summary>The length of the sound, or null if this isn't a WAV file it can read.</summary>
    public static TimeSpan? Length(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            return Length(stream);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The same, from a stream. A WAV file is a list of chunks: "fmt " says how many bytes a second of sound takes, and
    /// "data" how many bytes of sound there are.
    /// </summary>
    public static TimeSpan? Length(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        try
        {
            if (Id(reader) != "RIFF")
            {
                return null;
            }

            reader.ReadUInt32();
            if (Id(reader) != "WAVE")
            {
                return null;
            }

            uint bytesPerSecond = 0;
            while (stream.Position + 8 <= stream.Length)
            {
                var id = Id(reader);
                var size = reader.ReadUInt32();
                var next = stream.Position + size + (size % 2);

                switch (id)
                {
                    case "fmt ":
                        // The format, the channels, the samples a second, then the bytes a second.
                        reader.ReadUInt16();
                        reader.ReadUInt16();
                        reader.ReadUInt32();
                        bytesPerSecond = reader.ReadUInt32();
                        break;
                    case "data":
                        return bytesPerSecond > 0 ? TimeSpan.FromSeconds((double)size / bytesPerSecond) : null;
                }

                // Chunks are padded to an even length.
                stream.Position = next;
            }

            return null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
    }

    private static string Id(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
}
