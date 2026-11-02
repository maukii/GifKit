using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class GifKitTileSettings
{
    public int size = 256;
    public int pixelScale = 2;
    public Color background = new Color32(0x0E, 0x0E, 0x12, 0xFF);
    public Color foreground = new Color32(0xD8, 0xD8, 0xE0, 0xFF);
    public bool fog = true;
    public float fogDensity = 0.13f;
    public int fogScale = 3;
    public int smallFlakes = 70;
    public int largeFlakes = 40;
    public int fastFlakes = 12;
    public int frames = 128;
    public int fps = 11;
    public int seed = 1337;
    public string fileName = "tile";
}

public class GifKitTile
{
    static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

    struct Flake
    {
        public int x;
        public int y;
        public int size;
        public int speed;
        public int sway;
        public float phase;
    }

    readonly GifKitTileSettings settings;
    readonly int size;
    readonly byte[] fog;
    readonly List<Flake> flakes = new List<Flake>();
    readonly Color32[] palette;

    public GifKitTile(GifKitTileSettings tileSettings)
    {
        settings = tileSettings;
        size = Mathf.Max(8, settings.size);
        palette = new Color32[] { Opaque(settings.background), Opaque(settings.foreground) };
        var random = new System.Random(settings.seed);
        fog = new byte[size * size];
        if (settings.fog)
        {
            int coarseCells = Mathf.Max(1, settings.fogScale);
            int fineCells = coarseCells * 2 + 1;
            var coarse = Lattice(random, coarseCells);
            var fine = Lattice(random, fineCells);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float value = Noise(coarse, coarseCells, x, y) * 0.65f + Noise(fine, fineCells, x, y) * 0.35f;
                    float density = Mathf.Min(settings.fogDensity, Mathf.Max(0f, (value - 0.56f) * 0.6f));
                    fog[y * size + x] = density > (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f ? (byte)1 : (byte)0;
                }
        }
        AddFlakes(random, settings.smallFlakes, 1, 1);
        AddFlakes(random, settings.largeFlakes, 2, 1);
        AddFlakes(random, settings.fastFlakes, 2, 2);
    }

    public int Width => size * Mathf.Max(1, settings.pixelScale);

    public byte[] Frame(int frame)
    {
        var pixels = (byte[])fog.Clone();
        float t = (float)frame / Mathf.Max(1, settings.frames);
        foreach (var flake in flakes)
        {
            int y = flake.y + Mathf.RoundToInt(t * size * flake.speed);
            int x = flake.x + Mathf.RoundToInt(Mathf.Sin((t * flake.speed + flake.phase) * Mathf.PI * 2f) * flake.sway);
            for (int dy = 0; dy < flake.size; dy++)
                for (int dx = 0; dx < flake.size; dx++)
                    pixels[Wrap(y + dy) * size + Wrap(x + dx)] = 1;
        }
        return pixels;
    }

    public Texture2D Preview()
    {
        var pixels = Frame(0);
        var colors = new Color32[pixels.Length];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                colors[(size - 1 - y) * size + x] = palette[pixels[y * size + x]];
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(colors);
        texture.Apply(false);
        return texture;
    }

    public GifKitResult Save(string folder, bool animated)
    {
        int scale = Mathf.Max(1, settings.pixelScale);
        string name = string.IsNullOrWhiteSpace(settings.fileName) ? "tile" : settings.fileName.Trim();
        string basePath = GifKitWriter.UniquePath(folder, name, animated ? ".gif" : ".png");
        string path;
        int count = 1;
        if (animated)
        {
            count = Mathf.Max(2, settings.frames);
            var frames = new List<byte[]>(count);
            for (int f = 0; f < count; f++) frames.Add(GifKitImage.Upscale(Frame(f), size, size, scale));
            path = basePath + ".gif";
            int delay = Mathf.Max(2, Mathf.RoundToInt(100f / Mathf.Max(1, settings.fps)));
            File.WriteAllBytes(path, GifKitEncoder.Encode(Width, Width, palette, frames, delay));
        }
        else
        {
            path = basePath + ".png";
            File.WriteAllBytes(path, GifKitImage.EncodePng(GifKitImage.Upscale(Frame(0), size, size, scale), palette, Width, Width));
        }

        path = Path.GetFullPath(path);
        return new GifKitResult
        {
            path = path,
            width = Width,
            height = Width,
            frames = count,
            colors = 2,
            bytes = new FileInfo(path).Length
        };
    }

    void AddFlakes(System.Random random, int count, int flakeSize, int speed)
    {
        for (int i = 0; i < count; i++)
        {
            flakes.Add(new Flake
            {
                x = random.Next(size),
                y = random.Next(size),
                size = flakeSize,
                speed = speed,
                sway = 1 + random.Next(3),
                phase = (float)random.NextDouble()
            });
        }
    }

    int Wrap(int value) => ((value % size) + size) % size;

    static float[] Lattice(System.Random random, int cells)
    {
        var values = new float[cells * cells];
        for (int i = 0; i < values.Length; i++) values[i] = (float)random.NextDouble();
        return values;
    }

    float Noise(float[] lattice, int cells, int x, int y)
    {
        float fx = (float)x / size * cells;
        float fy = (float)y / size * cells;
        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0;
        float ty = fy - y0;
        float sx = tx * tx * (3f - 2f * tx);
        float sy = ty * ty * (3f - 2f * ty);
        float At(int i, int j) => lattice[(j % cells + cells) % cells * cells + (i % cells + cells) % cells];
        float top = Mathf.Lerp(At(x0, y0), At(x0 + 1, y0), sx);
        float bottom = Mathf.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), sx);
        return Mathf.Lerp(top, bottom, sy);
    }

    static Color32 Opaque(Color color)
    {
        Color32 result = color;
        result.a = 255;
        return result;
    }
}
