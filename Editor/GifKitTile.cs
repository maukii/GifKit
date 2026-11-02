using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class GifKitRandom
{
    uint state;

    public GifKitRandom(int seed)
    {
        state = unchecked((uint)seed);
    }

    public double Next()
    {
        state = unchecked(state * 1664525u + 1013904223u);
        return state / 4294967296.0;
    }

    public int Range(int max) => (int)Math.Floor(Next() * max);
}

public class GifKitTile
{
    readonly GifKitTileSettings settings;
    readonly List<GifKitPainter> painters = new List<GifKitPainter>();

    public int Width { get; }
    public int Height { get; }
    public int Frames { get; }
    public Color32[] Palette { get; }
    public List<string> Warnings { get; } = new List<string>();

    public GifKitTile(GifKitTileSettings tileSettings)
    {
        settings = tileSettings;
        Width = Mathf.Clamp(settings.width, 8, 1024);
        Height = Mathf.Clamp(settings.height, 8, 1024);
        Frames = Mathf.Max(1, settings.frames);

        int colors = settings.palette != null ? Mathf.Clamp(settings.palette.Length, 1, 255) : 1;
        Palette = new Color32[colors];
        for (int i = 0; i < colors; i++)
        {
            Color32 color = settings.palette != null && settings.palette.Length > 0 ? settings.palette[i] : Color.black;
            color.a = 255;
            Palette[i] = color;
        }

        for (int i = 0; i < settings.layers.Count; i++)
        {
            var layer = settings.layers[i];
            if (!layer.enabled) continue;
            GifKitPainter painter;
            switch (layer.type)
            {
                case GifKitLayerType.Noise: painter = new GifKitNoisePainter(layer, Width, Height, Palette); break;
                case GifKitLayerType.Particles: painter = new GifKitParticlePainter(layer, Width, Height, Palette); break;
                case GifKitLayerType.Pattern: painter = new GifKitPatternPainter(layer, Width, Height, Palette); break;
                default: painter = new GifKitSpritePainter(layer, Width, Height, Palette); break;
            }
            painters.Add(painter);
            string warning = painter.Warning();
            if (warning != null) Warnings.Add($"Layer {i + 1}: {warning}");
        }
    }

    public int Scale => Mathf.Max(1, settings.pixelScale);

    public byte[] Frame(int frame)
    {
        var pixels = new byte[Width * Height];
        double t = (double)(frame % Frames) / Frames;
        foreach (var painter in painters) painter.Paint(pixels, t);
        return pixels;
    }

    public Texture2D CreatePreview()
    {
        return new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    public void Draw(Texture2D texture, int frame)
    {
        var pixels = Frame(frame);
        var colors = new Color32[pixels.Length];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                colors[(Height - 1 - y) * Width + x] = Palette[pixels[y * Width + x]];
        texture.SetPixels32(colors);
        texture.Apply(false);
    }

    public GifKitResult Save(string folder, bool animated)
    {
        string name = string.IsNullOrWhiteSpace(settings.fileName) ? "tile" : settings.fileName.Trim();
        string basePath = GifKitWriter.UniquePath(folder, name, animated ? ".gif" : ".png");
        int count = 1;
        string path;
        if (animated)
        {
            count = Frames;
            var frames = new List<byte[]>(count);
            for (int f = 0; f < count; f++) frames.Add(GifKitImage.Upscale(Frame(f), Width, Height, Scale));
            path = basePath + ".gif";
            int delay = Mathf.Max(2, Mathf.RoundToInt(100f / Mathf.Max(1, settings.fps)));
            File.WriteAllBytes(path, GifKitEncoder.Encode(Width * Scale, Height * Scale, Palette, frames, delay));
        }
        else
        {
            path = basePath + ".png";
            File.WriteAllBytes(path, Png());
        }
        return Result(Path.GetFullPath(path), count);
    }

    public GifKitResult SaveAsset(string assetPath)
    {
        File.WriteAllBytes(assetPath, Png());
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
        {
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return Result(Path.GetFullPath(assetPath), 1);
    }

    byte[] Png() => GifKitImage.EncodePng(GifKitImage.Upscale(Frame(0), Width, Height, Scale), Palette, Width * Scale, Height * Scale);

    GifKitResult Result(string path, int frames)
    {
        return new GifKitResult
        {
            path = path,
            width = Width * Scale,
            height = Height * Scale,
            frames = frames,
            colors = Palette.Length,
            bytes = new FileInfo(path).Length
        };
    }
}

abstract class GifKitPainter
{
    static readonly int[] Bayer2 = { 0, 2, 3, 1 };
    static readonly int[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    static readonly int[] Bayer8 =
    {
        0, 32, 8, 40, 2, 34, 10, 42,
        48, 16, 56, 24, 50, 18, 58, 26,
        12, 44, 4, 36, 14, 46, 6, 38,
        60, 28, 52, 20, 62, 30, 54, 22,
        3, 35, 11, 43, 1, 33, 9, 41,
        51, 19, 59, 27, 49, 17, 57, 25,
        15, 47, 7, 39, 13, 45, 5, 37,
        63, 31, 55, 23, 61, 29, 53, 21
    };

    protected readonly GifKitLayer layer;
    protected readonly int width;
    protected readonly int height;
    protected readonly byte color;
    protected readonly Color32[] palette;

    protected GifKitPainter(GifKitLayer paintLayer, int tileWidth, int tileHeight, Color32[] colors)
    {
        layer = paintLayer;
        width = tileWidth;
        height = tileHeight;
        palette = colors;
        color = (byte)Mathf.Clamp(layer.color, 0, colors.Length - 1);
    }

    public abstract void Paint(byte[] pixels, double t);

    public virtual string Warning() => null;

    protected int OffsetX(double t) => Round(t * width * layer.drift.x);

    protected int OffsetY(double t) => Round(t * height * layer.drift.y);

    protected static int Round(double value) => (int)Math.Floor(value + 0.5);

    protected static int Mod(int value, int size) => (value % size + size) % size;

    protected void Plot(byte[] pixels, int x, int y, byte value) => pixels[Mod(y, height) * width + Mod(x, width)] = value;

    protected static double Threshold(GifKitDither dither, int x, int y)
    {
        switch (dither)
        {
            case GifKitDither.Bayer2: return (Bayer2[(y & 1) * 2 + (x & 1)] + 0.5) / 4.0;
            case GifKitDither.Bayer8: return (Bayer8[(y & 7) * 8 + (x & 7)] + 0.5) / 64.0;
            default: return (Bayer4[(y & 3) * 4 + (x & 3)] + 0.5) / 16.0;
        }
    }

    protected GifKitStamp[] Stamps()
    {
        var stamps = new List<GifKitStamp>();
        if (layer.sprites != null)
            foreach (var sprite in layer.sprites)
                if (sprite != null) stamps.Add(GifKitStamp.From(sprite, layer.tint, color, palette));
        if (stamps.Count == 0) stamps.Add(GifKitStamp.Diamond(color));
        return stamps.ToArray();
    }

    protected void Stamp(byte[] pixels, GifKitStamp stamp, int x0, int y0, int scale)
    {
        for (int y = 0; y < stamp.height * scale; y++)
            for (int x = 0; x < stamp.width * scale; x++)
            {
                byte value = stamp.pixels[y / scale * stamp.width + x / scale];
                if (value != GifKitStamp.Clear) Plot(pixels, x0 + x, y0 + y, value);
            }
    }
}

class GifKitStamp
{
    public const byte Clear = 255;

    public int width;
    public int height;
    public byte[] pixels;

    public static GifKitStamp From(Texture2D texture, bool tint, byte color, Color32[] palette)
    {
        var source = GifKitImage.ReadTexture(texture, out int w, out int h);
        var stamp = new GifKitStamp { width = w, height = h, pixels = new byte[w * h] };
        for (int i = 0; i < source.Length; i++)
            stamp.pixels[i] = source[i].a < 128 ? Clear : tint ? color : Nearest(source[i], palette);
        return stamp;
    }

    public static GifKitStamp Diamond(byte color)
    {
        const int size = 7;
        var stamp = new GifKitStamp { width = size, height = size, pixels = new byte[size * size] };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                stamp.pixels[y * size + x] = Math.Abs(x - 3) + Math.Abs(y - 3) <= 3 ? color : Clear;
        return stamp;
    }

    static byte Nearest(Color32 color, Color32[] palette)
    {
        int best = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < palette.Length; i++)
        {
            int dr = palette[i].r - color.r, dg = palette[i].g - color.g, db = palette[i].b - color.b;
            int distance = dr * dr * 2 + dg * dg * 4 + db * db * 3;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }
        return (byte)best;
    }
}

class GifKitNoisePainter : GifKitPainter
{
    readonly double[] field;

    public GifKitNoisePainter(GifKitLayer paintLayer, int tileWidth, int tileHeight, Color32[] colors) : base(paintLayer, tileWidth, tileHeight, colors)
    {
        var random = new GifKitRandom(layer.seed);
        int coarseX = Mathf.Max(1, layer.scale);
        int coarseY = Mathf.Max(1, Mathf.RoundToInt(layer.scale * (float)height / width));
        int fineX = coarseX * 2 + 1;
        int fineY = coarseY * 2 + 1;
        var coarse = Lattice(random, coarseX * coarseY);
        var fine = Lattice(random, fineX * fineY);
        field = new double[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                double value = Sample(coarse, coarseX, coarseY, x, y) * 0.65 + Sample(fine, fineX, fineY, x, y) * 0.35;
                field[y * width + x] = layer.dither == GifKitDither.None
                    ? value - layer.cutoff
                    : Math.Min(layer.density, Math.Max(0, (value - layer.cutoff) * layer.ramp));
            }
    }

    public override void Paint(byte[] pixels, double t)
    {
        int ox = OffsetX(t), oy = OffsetY(t);
        for (int y = 0; y < height; y++)
        {
            int row = Mod(y - oy, height) * width;
            for (int x = 0; x < width; x++)
            {
                double value = field[row + Mod(x - ox, width)];
                bool lit = layer.dither == GifKitDither.None ? value > 0 : value > Threshold(layer.dither, x, y);
                if (lit) pixels[y * width + x] = color;
            }
        }
    }

    static double[] Lattice(GifKitRandom random, int count)
    {
        var values = new double[count];
        for (int i = 0; i < count; i++) values[i] = random.Next();
        return values;
    }

    double Sample(double[] lattice, int cellsX, int cellsY, int x, int y)
    {
        double fx = (double)x / width * cellsX;
        double fy = (double)y / height * cellsY;
        int x0 = (int)Math.Floor(fx);
        int y0 = (int)Math.Floor(fy);
        double tx = fx - x0;
        double ty = fy - y0;
        double sx = tx * tx * (3 - 2 * tx);
        double sy = ty * ty * (3 - 2 * ty);
        double At(int i, int j) => lattice[Mod(j, cellsY) * cellsX + Mod(i, cellsX)];
        double a = At(x0, y0) + (At(x0 + 1, y0) - At(x0, y0)) * sx;
        double b = At(x0, y0 + 1) + (At(x0 + 1, y0 + 1) - At(x0, y0 + 1)) * sx;
        return a + (b - a) * sy;
    }
}

class GifKitParticlePainter : GifKitPainter
{
    struct Particle
    {
        public int x;
        public int y;
        public int sway;
        public int size;
        public int stamp;
        public double phase;
        public double blink;
    }

    readonly Particle[] particles;
    readonly GifKitStamp[] stamps;

    public GifKitParticlePainter(GifKitLayer paintLayer, int tileWidth, int tileHeight, Color32[] colors) : base(paintLayer, tileWidth, tileHeight, colors)
    {
        var random = new GifKitRandom(layer.seed);
        int minSize = Mathf.Max(1, layer.minSize);
        int maxSize = Mathf.Max(minSize, layer.maxSize);
        stamps = layer.shape == GifKitShape.Sprite ? Stamps() : null;
        particles = new Particle[Mathf.Max(0, layer.count)];
        for (int i = 0; i < particles.Length; i++)
        {
            var particle = new Particle
            {
                x = random.Range(width),
                y = random.Range(height)
            };
            int swayRoll = random.Range(Mathf.Max(1, layer.sway));
            particle.sway = layer.sway > 0 ? 1 + swayRoll : 0;
            particle.phase = random.Next();
            particle.size = maxSize > minSize ? minSize + random.Range(maxSize - minSize + 1) : minSize;
            if (layer.twinkle) particle.blink = random.Next();
            if (stamps != null && stamps.Length > 1) particle.stamp = random.Range(stamps.Length);
            particles[i] = particle;
        }
    }

    public override void Paint(byte[] pixels, double t)
    {
        int cycles = Mathf.Max(1, Mathf.Max(Mathf.Abs(layer.drift.x), Mathf.Abs(layer.drift.y)));
        int ox = OffsetX(t), oy = OffsetY(t);
        foreach (var particle in particles)
        {
            if (layer.twinkle && Math.Sin((t * 3 + particle.blink) * Math.PI * 2) < -0.4) continue;
            int x = particle.x + ox + Round(Math.Sin((t * cycles + particle.phase) * Math.PI * 2) * particle.sway);
            int y = particle.y + oy;
            Draw(pixels, x, y, particle);
        }
    }

    void Draw(byte[] pixels, int x, int y, Particle particle)
    {
        int size = particle.size;
        switch (layer.shape)
        {
            case GifKitShape.Square:
                for (int dy = 0; dy < size; dy++)
                    for (int dx = 0; dx < size; dx++) Plot(pixels, x + dx, y + dy, color);
                break;
            case GifKitShape.Dot:
                double center = (size - 1) * 0.5;
                double radius = size * 0.5;
                for (int dy = 0; dy < size; dy++)
                    for (int dx = 0; dx < size; dx++)
                    {
                        double ex = dx - center, ey = dy - center;
                        if (size <= 2 || ex * ex + ey * ey <= radius * radius) Plot(pixels, x + dx, y + dy, color);
                    }
                break;
            case GifKitShape.Plus:
                Plot(pixels, x, y, color);
                for (int k = 1; k <= size; k++)
                {
                    Plot(pixels, x + k, y, color);
                    Plot(pixels, x - k, y, color);
                    Plot(pixels, x, y + k, color);
                    Plot(pixels, x, y - k, color);
                }
                break;
            case GifKitShape.Streak:
                double vx = (double)layer.drift.x * width, vy = (double)layer.drift.y * height;
                double length = Math.Sqrt(vx * vx + vy * vy);
                if (length < 0.001)
                {
                    vx = 0;
                    vy = 1;
                    length = 1;
                }
                for (int k = 0; k < Mathf.Max(1, layer.length); k++)
                {
                    int px = x - Round(vx / length * k);
                    int py = y - Round(vy / length * k);
                    for (int dy = 0; dy < size; dy++)
                        for (int dx = 0; dx < size; dx++) Plot(pixels, px + dx, py + dy, color);
                }
                break;
            case GifKitShape.Sprite:
                Stamp(pixels, stamps[particle.stamp], x, y, Mathf.Max(1, layer.spriteScale));
                break;
        }
    }
}

class GifKitPatternPainter : GifKitPainter
{
    public GifKitPatternPainter(GifKitLayer paintLayer, int tileWidth, int tileHeight, Color32[] colors) : base(paintLayer, tileWidth, tileHeight, colors)
    {
    }

    int Cell => Mathf.Max(2, layer.cell);

    public override string Warning()
    {
        int cell = Cell;
        bool fits;
        switch (layer.pattern)
        {
            case GifKitPattern.Checker: fits = width % (cell * 2) == 0 && height % (cell * 2) == 0; break;
            case GifKitPattern.HorizontalStripes: fits = height % cell == 0; break;
            case GifKitPattern.VerticalStripes: fits = width % cell == 0; break;
            case GifKitPattern.Bricks: fits = cell % 2 == 0 && width % cell == 0 && height % cell == 0; break;
            default: fits = width % cell == 0 && height % cell == 0; break;
        }
        return fits ? null : "the pattern size doesn't divide the tile size, so the edges won't line up";
    }

    public override void Paint(byte[] pixels, double t)
    {
        int cell = Cell;
        int thickness = Mathf.Max(1, layer.thickness);
        int ox = OffsetX(t), oy = OffsetY(t);
        double center = (cell - 1) * 0.5;
        double radius = thickness * 0.5;
        int brickRow = Mathf.Max(1, cell / 2);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int sx = x - ox, sy = y - oy;
                bool on;
                switch (layer.pattern)
                {
                    case GifKitPattern.Checker:
                        on = ((Divide(sx, cell) + Divide(sy, cell)) & 1) == 0;
                        break;
                    case GifKitPattern.HorizontalStripes:
                        on = Mod(sy, cell) < thickness;
                        break;
                    case GifKitPattern.VerticalStripes:
                        on = Mod(sx, cell) < thickness;
                        break;
                    case GifKitPattern.DiagonalStripes:
                        on = Mod(sx + sy, cell) < thickness;
                        break;
                    case GifKitPattern.Dots:
                        double ex = Mod(sx, cell) - center, ey = Mod(sy, cell) - center;
                        on = ex * ex + ey * ey <= radius * radius;
                        break;
                    case GifKitPattern.Grid:
                        on = Mod(sx, cell) < thickness || Mod(sy, cell) < thickness;
                        break;
                    default:
                        int row = Divide(sy, brickRow);
                        on = Mod(sy, brickRow) < thickness || Mod(sx + (row & 1) * (cell / 2), cell) < thickness;
                        break;
                }
                if (on) pixels[y * width + x] = color;
            }
    }

    static int Divide(int value, int size) => (int)Math.Floor((double)value / size);
}

class GifKitSpritePainter : GifKitPainter
{
    readonly GifKitStamp[] stamps;

    public GifKitSpritePainter(GifKitLayer paintLayer, int tileWidth, int tileHeight, Color32[] colors) : base(paintLayer, tileWidth, tileHeight, colors)
    {
        stamps = Stamps();
    }

    public override string Warning()
    {
        int cw = Mathf.Max(1, layer.spacing.x), ch = Mathf.Max(1, layer.spacing.y);
        bool fits = width % cw == 0 && height % (layer.stagger ? ch * 2 : ch) == 0;
        return fits ? null : "the spacing doesn't divide the tile size, so the edges won't line up";
    }

    public override void Paint(byte[] pixels, double t)
    {
        int cw = Mathf.Max(1, layer.spacing.x), ch = Mathf.Max(1, layer.spacing.y);
        int scale = Mathf.Max(1, layer.spriteScale);
        int ox = OffsetX(t), oy = OffsetY(t);
        int columns = (width + cw - 1) / cw;
        int rows = (height + ch - 1) / ch;
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                var stamp = stamps[(row + column) % stamps.Length];
                int x = column * cw + (layer.stagger && (row & 1) == 1 ? cw / 2 : 0) + ox + (cw - stamp.width * scale) / 2;
                int y = row * ch + oy + (ch - stamp.height * scale) / 2;
                Stamp(pixels, stamp, x, y, scale);
            }
    }
}
