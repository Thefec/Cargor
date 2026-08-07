using NUnit.Framework;
using NewCss.Voice.Core;

public class PooledByteStreamTests
{
    [Test]
    public void Write_ExceedsCapacity_TruncatesWithoutThrow()
    {
        var stream = new PooledByteStream(new byte[4]);
        byte[] data = { 1, 2, 3, 4, 5, 6 };

        Assert.DoesNotThrow(() => stream.Write(data, 0, 6));
        Assert.IsTrue(stream.Truncated);
        Assert.AreEqual(4, stream.Length);
    }

    [Test]
    public void Reset_ClearsPositionLengthAndTruncatedFlag()
    {
        var stream = new PooledByteStream(new byte[4]);
        stream.Write(new byte[] { 1, 2, 3, 4, 5 }, 0, 5); // Truncated=true üretir

        stream.Reset();

        Assert.AreEqual(0, stream.Position);
        Assert.AreEqual(0, stream.Length);
        Assert.IsFalse(stream.Truncated);
    }

    [Test]
    public void Read_PartialRead_ReturnsOnlyAvailableBytes()
    {
        var stream = new PooledByteStream(new byte[8]);
        stream.Write(new byte[] { 1, 2, 3 }, 0, 3);
        stream.Position = 0;

        byte[] dest = new byte[10];
        int read = stream.Read(dest, 0, 10);

        Assert.AreEqual(3, read);
        Assert.AreEqual(1, dest[0]);
        Assert.AreEqual(2, dest[1]);
        Assert.AreEqual(3, dest[2]);
    }

    [Test]
    public void SetLength_BeyondCapacity_ClampsAndMarksTruncated_NoThrow()
    {
        // MemoryStream(fixedBuffer).SetLength(...) burada NotSupportedException fırlatırdı —
        // PooledByteStream'in var olma sebebi tam bu.
        var stream = new PooledByteStream(new byte[4]);

        Assert.DoesNotThrow(() => stream.SetLength(100));
        Assert.AreEqual(4, stream.Length);
        Assert.IsTrue(stream.Truncated);
    }

    [Test]
    public void CanReadWriteSeek_AlwaysTrue()
    {
        var stream = new PooledByteStream(new byte[1]);

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsTrue(stream.CanSeek);
    }
}
