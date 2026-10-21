using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using MazeGame.AssetForge;

namespace MazeGame.Tests.Assets;

/// <summary>
/// Verifies the dependency-free PNG encoder by reading its output back with the platform's own
/// zlib decoder, so the test cannot share a bug with the writer.
/// </summary>
public sealed class PngTests
{
    [Fact]
    public void TheSignature_IsAPngSignature()
    {
        var bytes = Png.Encode(1, 1, [1, 2, 3, 255]);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes[..8]);
    }

    [Fact]
    public void TheChunks_AreWellFormedAndThePixelsSurvive()
    {
        var width = 3;
        var height = 2;
        var pixels = new byte[width * height * 4];

        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)(index * 4);
        }

        var chunks = ReadChunks(Png.Encode(width, height, pixels));

        Assert.Equal(["IHDR", "IDAT", "IEND"], chunks.ConvertAll(chunk => chunk.Type));

        var header = chunks[0].Data;
        Assert.Equal(width, BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0)));
        Assert.Equal(height, BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4)));
        Assert.Equal(8, header[8]);   // bit depth
        Assert.Equal(6, header[9]);   // colour type RGBA
        Assert.Equal(0, header[12]);  // no interlacing

        var raw = Inflate(chunks[1].Data);

        Assert.Equal((width * 4 + 1) * height, raw.Length);

        for (var row = 0; row < height; row++)
        {
            var stride = (width * 4) + 1;
            Assert.Equal(0, raw[row * stride]); // filter type: none

            for (var column = 0; column < width * 4; column++)
            {
                Assert.Equal(pixels[(row * width * 4) + column], raw[(row * stride) + 1 + column]);
            }
        }
    }

    [Fact]
    public void ATransparentPixel_EncodesWithZeroAlpha()
    {
        var canvas = new PixelCanvas(2, 1);
        canvas[1, 0] = (byte)PaletteRole.Glow;

        var bytes = canvas.ToRgba(Palette.Derive(ThemeCatalog.ById("shallows")!));
        var raw = Inflate(ReadChunks(Png.Encode(2, 1, bytes))[1].Data);

        Assert.Equal(0, raw[4]);    // alpha of pixel 0
        Assert.Equal(255, raw[8]);  // alpha of pixel 1
    }

    [Fact]
    public void Encode_RejectsAWrongBufferSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Png.Encode(2, 2, new byte[15]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Png.Encode(0, 2, new byte[8]));
    }

    [Fact]
    public void Checksum_IsStable()
    {
        // The manifest records this value, so it has to be the standard CRC-32 of the bytes.
        Assert.Equal(0xCBF43926u, Png.Checksum(Encoding.ASCII.GetBytes("123456789")));
    }

    private static List<(string Type, byte[] Data)> ReadChunks(byte[] png)
    {
        var chunks = new List<(string, byte[])>();

        Assert.Equal(0x89, png[0]);
        var offset = 8;

        while (offset < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            var data = png.AsSpan(offset + 8, length).ToArray();
            var crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8 + length));

            var typeBytes = Encoding.ASCII.GetBytes(type);
            var actual = Png.Checksum([.. typeBytes, .. data]);

            Assert.Equal(actual, crc);
            chunks.Add((type, data));
            offset += 12 + length;
        }

        return chunks;
    }

    private static byte[] Inflate(byte[] zlib)
    {
        using var input = new MemoryStream(zlib);
        using var inflater = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflater.CopyTo(output);
        return output.ToArray();
    }
}
