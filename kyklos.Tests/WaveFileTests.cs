using System.Text;

namespace Kyklos.Tests;

public class WaveFileTests
{
    [Fact]
    public void ReadsTheLengthFromTheHeader()
    {
        // 48 kHz, mono, 16-bit: 96,000 bytes a second, so 345,600 bytes of sound play for 3.6 seconds, as Chime does.
        // An odd-sized chunk before the sound checks the padding.
        var wav = Wave(Chunk("fmt ", Format(96_000)), Chunk("LIST", new byte[3]), Chunk("data", new byte[345_600]));

        Assert.Equal(TimeSpan.FromSeconds(3.6), WaveFile.Length(new MemoryStream(wav)));
    }

    [Fact]
    public void AnythingElseHasNoLength()
    {
        Assert.Null(WaveFile.Length(new MemoryStream(Encoding.ASCII.GetBytes("ID3 not a wave file"))));
        Assert.Null(WaveFile.Length(new MemoryStream(Wave(Chunk("fmt ", Format(96_000))))));
        Assert.Null(WaveFile.Length(new MemoryStream(Wave(Chunk("data", new byte[100])))));
        Assert.Null(WaveFile.Length(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".wav")));
    }

    private static byte[] Wave(params byte[][] chunks)
    {
        var body = chunks.SelectMany(chunk => chunk).ToArray();
        return [.. Encoding.ASCII.GetBytes("RIFF"), .. BitConverter.GetBytes(4 + body.Length), .. Encoding.ASCII.GetBytes("WAVE"), .. body];
    }

    private static byte[] Chunk(string id, byte[] data) =>
        [.. Encoding.ASCII.GetBytes(id), .. BitConverter.GetBytes(data.Length), .. data, .. data.Length % 2 == 1 ? new byte[1] : []];

    // PCM, one channel, 48 kHz, the bytes a second given, 2 bytes a frame, 16 bits.
    private static byte[] Format(int bytesPerSecond) =>
        [.. BitConverter.GetBytes((short)1), .. BitConverter.GetBytes((short)1), .. BitConverter.GetBytes(48_000),
            .. BitConverter.GetBytes(bytesPerSecond), .. BitConverter.GetBytes((short)2), .. BitConverter.GetBytes((short)16)];
}
