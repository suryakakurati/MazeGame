using System;
using System.Buffers.Binary;
using System.IO;

namespace MazeGame.AssetForge;

/// <summary>
/// Writes 8-bit RGBA PNG files with no packages and no compression.
/// </summary>
/// <remarks>
/// <para>
/// The tool may not take a dependency just to save an image, and the output is 16x16 pixel art:
/// a stored (uncompressed) deflate stream is a few hundred bytes per atlas, so the missing
/// compression costs nothing that matters. The encoder is therefore <b>exactly</b> reproducible
/// byte for byte, which is what lets a test assert that a recompile changes nothing.
/// </para>
/// <para>
/// Only one shape is produced: colour type 6 (RGBA), bit depth 8, no interlacing, filter 0 on every
/// row. That is the simplest form every decoder accepts.
/// </para>
/// </remarks>
public static class Png
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Encodes raw RGBA bytes as a PNG file.</summary>
    /// <param name="width">The image width in pixels; positive.</param>
    /// <param name="height">The image height in pixels; positive.</param>
    /// <param name="rgba">The pixels, row by row, four bytes each.</param>
    /// <returns>The complete PNG file.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a side is not positive, or when <paramref name="rgba"/> is not exactly
    /// <c>width * height * 4</c> bytes.
    /// </exception>
    public static byte[] Encode(int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A PNG needs a positive size.");
        }

        var expected = width * height * 4;
        if (rgba.Length != expected)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rgba),
                rgba.Length,
                $"A {width}x{height} image needs {expected} RGBA bytes.");
        }

        using var output = new MemoryStream();
        output.Write(Signature);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;  // bit depth
        header[9] = 6;  // colour type: RGBA
        header[10] = 0; // compression: deflate
        header[11] = 0; // filter: adaptive
        header[12] = 0; // interlace: none
        WriteChunk(output, "IHDR", header);

        WriteChunk(output, "IDAT", ZlibStore(RawScanlines(width, height, rgba)));
        WriteChunk(output, "IEND", []);

        return output.ToArray();
    }

    /// <summary>Prefixes every row with filter type 0, as PNG requires.</summary>
    private static byte[] RawScanlines(int width, int height, ReadOnlySpan<byte> rgba)
    {
        var stride = width * 4;
        var raw = new byte[(stride + 1) * height];

        for (var row = 0; row < height; row++)
        {
            raw[row * (stride + 1)] = 0;
            rgba.Slice(row * stride, stride).CopyTo(raw.AsSpan((row * (stride + 1)) + 1, stride));
        }

        return raw;
    }

    /// <summary>
    /// Wraps <paramref name="data"/> in a zlib stream built from stored deflate blocks, with the
    /// Adler-32 checksum the format requires.
    /// </summary>
    private static byte[] ZlibStore(byte[] data)
    {
        const int MaxBlock = 65535;

        using var output = new MemoryStream();

        // 0x78 0x01: deflate, 32K window, no preset dictionary, fastest compression level.
        output.WriteByte(0x78);
        output.WriteByte(0x01);

        var offset = 0;
        do
        {
            var length = Math.Min(MaxBlock, data.Length - offset);
            var last = offset + length >= data.Length;

            output.WriteByte((byte)(last ? 1 : 0));
            output.WriteByte((byte)(length & 0xFF));
            output.WriteByte((byte)(length >> 8));
            output.WriteByte((byte)(~length & 0xFF));
            output.WriteByte((byte)((~length >> 8) & 0xFF));
            output.Write(data, offset, length);

            offset += length;
        }
        while (offset < data.Length);

        var adler = Adler32(data);
        output.WriteByte((byte)(adler >> 24));
        output.WriteByte((byte)(adler >> 16));
        output.WriteByte((byte)(adler >> 8));
        output.WriteByte((byte)adler);

        return output.ToArray();
    }

    /// <summary>Writes one length-prefixed chunk with its CRC.</summary>
    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);

        var name = new byte[4];
        for (var index = 0; index < 4; index++)
        {
            name[index] = (byte)type[index];
        }

        output.Write(name);
        output.Write(data);

        var crc = Crc32(name, data);
        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, crc);
        output.Write(checksum);
    }

    /// <summary>Returns the CRC-32 of <paramref name="data"/>, the checksum PNG chunks use.</summary>
    /// <param name="data">The bytes to checksum.</param>
    /// <returns>The CRC-32 value.</returns>
    public static uint Checksum(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in type)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        const uint Modulus = 65521;

        uint low = 1;
        uint high = 0;

        foreach (var value in data)
        {
            low = (low + value) % Modulus;
            high = (high + low) % Modulus;
        }

        return (high << 16) | low;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];

        for (uint entry = 0; entry < 256; entry++)
        {
            var value = entry;

            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[entry] = value;
        }

        return table;
    }
}
