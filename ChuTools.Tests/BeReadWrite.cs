using Be.IO;

using BmsDerg.Utility;

namespace ChuTools.Tests;

[TestFixture]
public class BeReadWrite
{
    [Test]
    [TestCase(10)]
    [TestCase(200)]
    [TestCase(20000)]
    [TestCase(20000000)]
    public static void TestVarInt(int value)
    {
        var ms = new MemoryStream();
        var writer = new BeBinaryWriter(ms);
        writer.WriteVarInt(value);

        ms.Position = 0;
        var reader = new BeBinaryReader(ms);
        var readBack = reader.ReadVarInt32();

        Assert.That(readBack, Is.EqualTo(value));
    }
}