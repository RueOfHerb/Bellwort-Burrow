using System.IO;
using System.Text;
using NUnit.Framework;
using BellwortBurrow.EditorTools;

namespace BellwortBurrow.Tests.Editor
{
    /// <summary>Reads a tiny Aseprite file built in memory: an Art layer, a Glow layer, a hidden layer and one slice.</summary>
    public class AsepriteSheetTests
    {
        static readonly byte[] Red = { 255, 0, 0, 255 };
        static readonly byte[] Yellow = { 255, 220, 80, 255 };
        static readonly byte[] Blue = { 0, 0, 255, 255 };

        static byte[] BuildFile()
        {
            var chunks = new MemoryStream();
            var w = new BinaryWriter(chunks);
            int count = 0;
            Chunk(w, 0x2004, Layer("Art", visible: true)); count++;
            Chunk(w, 0x2004, Layer("Glow (unlit)", visible: true)); count++;
            Chunk(w, 0x2004, Layer("Sketch", visible: false)); count++;
            Chunk(w, 0x2005, Cel(0, 0, 0, 2, 2, Red)); count++;    // Art: 2x2 red at the top left
            Chunk(w, 0x2005, Cel(1, 1, 1, 1, 1, Yellow)); count++; // Glow: one yellow pixel at (1, 1)
            Chunk(w, 0x2005, Cel(2, 3, 3, 1, 1, Blue)); count++;   // hidden layer: must not show up
            Chunk(w, 0x2022, Slice("thing", 0, 0, 4, 4, 2, 4)); count++;

            var frame = new MemoryStream();
            var f = new BinaryWriter(frame);
            f.Write((uint)(16 + chunks.Length));
            f.Write((ushort)0xF1FA);
            f.Write((ushort)count);
            f.Write((ushort)100);
            f.Write(new byte[2]);
            f.Write((uint)count);
            f.Write(chunks.ToArray());

            var file = new MemoryStream();
            var h = new BinaryWriter(file);
            h.Write((uint)(128 + frame.Length));
            h.Write((ushort)0xA5E0);
            h.Write((ushort)1);   // frames
            h.Write((ushort)4);   // width
            h.Write((ushort)4);   // height
            h.Write((ushort)32);  // RGBA
            h.Write((uint)1);     // layer opacity is valid
            h.Write(new byte[128 - 18]);
            h.Write(frame.ToArray());
            return file.ToArray();
        }

        static void Chunk(BinaryWriter w, ushort type, byte[] data)
        {
            w.Write((uint)(6 + data.Length));
            w.Write(type);
            w.Write(data);
        }

        static byte[] Layer(string name, bool visible)
        {
            var s = new MemoryStream();
            var w = new BinaryWriter(s);
            w.Write((ushort)(visible ? 1 : 0)); // flags
            w.Write((ushort)0);                 // normal layer
            w.Write((ushort)0);                 // child level
            w.Write((ushort)0); w.Write((ushort)0);
            w.Write((ushort)0);                 // normal blend
            w.Write((byte)255);                 // opacity
            w.Write(new byte[3]);
            Name(w, name);
            return s.ToArray();
        }

        static byte[] Cel(int layer, int x, int y, int width, int height, byte[] color)
        {
            var s = new MemoryStream();
            var w = new BinaryWriter(s);
            w.Write((ushort)layer);
            w.Write((short)x); w.Write((short)y);
            w.Write((byte)255);   // opacity
            w.Write((ushort)0);   // raw pixels
            w.Write((short)0);    // z-index
            w.Write(new byte[5]);
            w.Write((ushort)width); w.Write((ushort)height);
            for (int i = 0; i < width * height; i++) w.Write(color);
            return s.ToArray();
        }

        static byte[] Slice(string name, int x, int y, int width, int height, int pivotX, int pivotY)
        {
            var s = new MemoryStream();
            var w = new BinaryWriter(s);
            w.Write((uint)1);   // one key
            w.Write((uint)2);   // has a pivot
            w.Write((uint)0);
            Name(w, name);
            w.Write((uint)0);   // from frame 0
            w.Write(x); w.Write(y);
            w.Write((uint)width); w.Write((uint)height);
            w.Write(pivotX); w.Write(pivotY);
            return s.ToArray();
        }

        static void Name(BinaryWriter w, string name)
        {
            var bytes = Encoding.UTF8.GetBytes(name);
            w.Write((ushort)bytes.Length);
            w.Write(bytes);
        }

        static byte[] Pixel(byte[] rgba, int x, int y) =>
            new[] { rgba[(y * 4 + x) * 4], rgba[(y * 4 + x) * 4 + 1], rgba[(y * 4 + x) * 4 + 2], rgba[(y * 4 + x) * 4 + 3] };

        [Test]
        public void Read_SizeAndSlice()
        {
            var sheet = AsepriteSheet.Read(BuildFile());
            Assert.AreEqual(4, sheet.Width);
            Assert.AreEqual(4, sheet.Height);
            Assert.AreEqual(1, sheet.Slices.Count);
            var slice = sheet.Slices[0];
            Assert.AreEqual("thing", slice.Name);
            Assert.AreEqual(4, slice.Width);
            Assert.IsTrue(slice.HasPivot);
            Assert.AreEqual(2, slice.PivotX);
            Assert.AreEqual(4, slice.PivotY);
        }

        [Test]
        public void Read_ArtAndGlowGoToTheirOwnImages()
        {
            var sheet = AsepriteSheet.Read(BuildFile());
            CollectionAssert.AreEqual(Red, Pixel(sheet.Art, 1, 1), "the glow pixel must not be drawn into the art");
            CollectionAssert.AreEqual(Yellow, Pixel(sheet.Glow, 1, 1));
            Assert.AreEqual(0, Pixel(sheet.Glow, 0, 0)[3], "the art must not be drawn into the glow");
            Assert.IsTrue(sheet.HasGlow);
        }

        [Test]
        public void Read_HiddenLayersAreLeftOut()
        {
            var sheet = AsepriteSheet.Read(BuildFile());
            Assert.AreEqual(0, Pixel(sheet.Art, 3, 3)[3]);
        }

        [Test]
        public void Read_NotAnAsepriteFile_Throws()
        {
            Assert.Throws<InvalidDataException>(() => AsepriteSheet.Read(new byte[200]));
        }
    }
}
