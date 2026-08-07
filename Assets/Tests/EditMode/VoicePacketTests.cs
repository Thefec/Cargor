using NUnit.Framework;
using NewCss.Voice.Core;

public class VoicePacketTests
{
    [Test]
    public void TryWriteHeader_TryReadHeader_RoundTrip()
    {
        byte[] buffer = new byte[4];
        var flagsIn = VoicePacketFlags.BurstStart | VoicePacketFlags.Continuation;

        Assert.IsTrue(VoicePacket.TryWriteHeader(buffer, 0, 4, flagsIn, 1234));
        Assert.IsTrue(VoicePacket.TryReadHeader(buffer, 0, 4, out var flagsOut, out var seqOut));

        Assert.AreEqual(flagsIn, flagsOut);
        Assert.AreEqual((ushort)1234, seqOut);
    }

    [Test]
    public void TryReadHeader_UnknownVersion_ReturnsFalse()
    {
        // version=99: bu build'in bilmediği bir versiyon — mixed-build kazası sessizce düşmeli
        byte[] buffer = { 99, 0, 0, 0 };

        Assert.IsFalse(VoicePacket.TryReadHeader(buffer, 0, 4, out _, out _));
    }

    [Test]
    public void TryReadHeader_ShortOrNullBuffer_ReturnsFalseNoThrow()
    {
        byte[] shortBuffer = new byte[2];

        Assert.DoesNotThrow(() =>
        {
            Assert.IsFalse(VoicePacket.TryReadHeader(shortBuffer, 0, 2, out _, out _));
            Assert.IsFalse(VoicePacket.TryReadHeader(null, 0, 0, out _, out _));
        });
    }

    [Test]
    public void TryWriteRelayHeader_TryReadRelayHeader_RoundTrip()
    {
        byte[] buffer = new byte[8];
        ulong clientId = 123456789012345UL;

        Assert.IsTrue(VoicePacket.TryWriteRelayHeader(buffer, 0, 8, clientId));
        Assert.IsTrue(VoicePacket.TryReadRelayHeader(buffer, 0, 8, out var readId));

        Assert.AreEqual(clientId, readId);
    }

    [Test]
    public void Header_SequenceWraparoundValue_RoundTripsCorrectly()
    {
        byte[] buffer = new byte[4];

        Assert.IsTrue(VoicePacket.TryWriteHeader(buffer, 0, 4, VoicePacketFlags.None, 65535));
        Assert.IsTrue(VoicePacket.TryReadHeader(buffer, 0, 4, out _, out var seq));

        Assert.AreEqual((ushort)65535, seq);
    }
}
