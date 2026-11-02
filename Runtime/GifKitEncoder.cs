using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class GifKitEncoder
{
    public static byte[] Encode(int width, int height, Color32[] palette, List<byte[]> frames, int delay)
    {
        int transparent = palette.Length;
        int tableSize = 2;
        int depth = 1;
        while (tableSize < palette.Length + 1)
        {
            tableSize <<= 1;
            depth++;
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("GIF89a"));
        writer.Write((ushort)width);
        writer.Write((ushort)height);
        writer.Write((byte)(0x80 | ((depth - 1) << 4) | (depth - 1)));
        writer.Write((byte)0);
        writer.Write((byte)0);
        for (int i = 0; i < tableSize; i++)
        {
            var color = i < palette.Length ? palette[i] : new Color32(0, 0, 0, 255);
            writer.Write(color.r);
            writer.Write(color.g);
            writer.Write(color.b);
        }

        if (frames.Count > 1)
        {
            writer.Write(new byte[] { 0x21, 0xFF, 0x0B });
            writer.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
            writer.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });
        }

        int minCode = Math.Max(2, depth);
        for (int f = 0; f < frames.Count; f++)
        {
            var current = frames[f];
            var previous = f > 0 ? frames[f - 1] : null;
            int x0 = 0, y0 = 0, x1 = width - 1, y1 = height - 1;
            if (previous != null)
                ChangedArea(current, previous, width, height, out x0, out y0, out x1, out y1);

            int w = x1 - x0 + 1;
            int h = y1 - y0 + 1;
            var pixels = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                int row = (y0 + y) * width + x0;
                for (int x = 0; x < w; x++)
                {
                    byte value = current[row + x];
                    pixels[y * w + x] = previous != null && previous[row + x] == value ? (byte)transparent : value;
                }
            }

            writer.Write(new byte[] { 0x21, 0xF9, 0x04 });
            writer.Write((byte)((1 << 2) | (previous != null ? 1 : 0)));
            writer.Write((ushort)delay);
            writer.Write((byte)transparent);
            writer.Write((byte)0);

            writer.Write((byte)0x2C);
            writer.Write((ushort)x0);
            writer.Write((ushort)y0);
            writer.Write((ushort)w);
            writer.Write((ushort)h);
            writer.Write((byte)0);
            Compress(writer, pixels, minCode);
        }

        writer.Write((byte)0x3B);
        writer.Flush();
        return stream.ToArray();
    }

    static void ChangedArea(byte[] current, byte[] previous, int width, int height, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = width;
        y0 = height;
        x1 = -1;
        y1 = -1;
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (current[row + x] == previous[row + x]) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
        }

        if (x1 < 0)
        {
            x0 = 0;
            y0 = 0;
            x1 = 0;
            y1 = 0;
        }
    }

    static void Compress(BinaryWriter writer, byte[] pixels, int minCode)
    {
        int clear = 1 << minCode;
        int end = clear + 1;
        int next = end + 1;
        int size = minCode + 1;
        int buffer = 0;
        int bits = 0;
        var output = new List<byte>(pixels.Length / 2 + 16);
        var table = new Dictionary<int, int>(4096);

        void Emit(int code)
        {
            buffer |= code << bits;
            bits += size;
            while (bits >= 8)
            {
                output.Add((byte)(buffer & 0xFF));
                buffer >>= 8;
                bits -= 8;
            }
        }

        Emit(clear);
        int prefix = pixels[0];
        for (int i = 1; i < pixels.Length; i++)
        {
            int value = pixels[i];
            int key = (prefix << 8) | value;
            if (table.TryGetValue(key, out int code))
            {
                prefix = code;
                continue;
            }

            Emit(prefix);
            if (next < 4096)
            {
                table[key] = next++;
                if (next > (1 << size) && size < 12) size++;
            }
            else
            {
                Emit(clear);
                table.Clear();
                next = end + 1;
                size = minCode + 1;
            }
            prefix = value;
        }

        Emit(prefix);
        Emit(end);
        if (bits > 0) output.Add((byte)(buffer & 0xFF));

        writer.Write((byte)minCode);
        for (int i = 0; i < output.Count; i += 255)
        {
            int count = Math.Min(255, output.Count - i);
            writer.Write((byte)count);
            for (int k = 0; k < count; k++) writer.Write(output[i + k]);
        }
        writer.Write((byte)0);
    }
}
