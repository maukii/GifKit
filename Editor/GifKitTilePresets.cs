using UnityEngine;

public static class GifKitTilePresets
{
    public static readonly string[] Names =
    {
        "Snow",
        "Rain",
        "Starfield",
        "Embers",
        "Bubbles",
        "Clouds",
        "Checker",
        "Stripes",
        "Wallpaper",
        "Neon grid"
    };

    public static GifKitTileSettings Create(int index)
    {
        switch (index)
        {
            case 0: return Snow();
            case 1: return Rain();
            case 2: return Starfield();
            case 3: return Embers();
            case 4: return Bubbles();
            case 5: return Clouds();
            case 6: return Checker();
            case 7: return Stripes();
            case 8: return Wallpaper();
            default: return NeonGrid();
        }
    }

    static GifKitTileSettings Snow()
    {
        var settings = Tile(128, 11, "snow", 0x090303, 0x420F02);
        settings.layers.Add(Noise(1, 1337, 3, 0.56, 0.6, 0.13, GifKitDither.Bayer4, Vector2Int.zero));
        settings.layers.Add(Particles(1, 1142702859, GifKitShape.Square, 70, 1, 1, new Vector2Int(0, 1), 3));
        settings.layers.Add(Particles(1, 939883043, GifKitShape.Square, 40, 2, 2, new Vector2Int(0, 1), 3));
        settings.layers.Add(Particles(1, 347637955, GifKitShape.Square, 12, 2, 2, new Vector2Int(0, 2), 3));
        return settings;
    }

    static GifKitTileSettings Rain()
    {
        var settings = Tile(48, 16, "rain", 0x0B1020, 0x1D2A44, 0x6F8FC0);
        settings.layers.Add(Noise(1, 11, 2, 0.5, 0.8, 0.25, GifKitDither.Bayer4, new Vector2Int(1, 0)));
        var far = Particles(1, 12, GifKitShape.Streak, 70, 1, 1, new Vector2Int(1, 4), 0);
        far.length = 6;
        settings.layers.Add(far);
        var near = Particles(2, 13, GifKitShape.Streak, 40, 1, 1, new Vector2Int(1, 6), 0);
        near.length = 10;
        settings.layers.Add(near);
        return settings;
    }

    static GifKitTileSettings Starfield()
    {
        var settings = Tile(96, 12, "stars", 0x05060F, 0x2A2D55, 0x8A8FD6, 0xF4F1FF);
        var far = Particles(1, 21, GifKitShape.Square, 160, 1, 1, new Vector2Int(1, 0), 0);
        far.twinkle = true;
        settings.layers.Add(far);
        var mid = Particles(2, 22, GifKitShape.Square, 50, 1, 1, new Vector2Int(2, 0), 0);
        mid.twinkle = true;
        settings.layers.Add(mid);
        settings.layers.Add(Particles(3, 23, GifKitShape.Plus, 8, 1, 1, new Vector2Int(3, 0), 0));
        return settings;
    }

    static GifKitTileSettings Embers()
    {
        var settings = Tile(96, 12, "embers", 0x140806, 0x4A1409, 0xC2461B, 0xFFB347);
        settings.layers.Add(Noise(1, 31, 3, 0.5, 0.8, 0.3, GifKitDither.Bayer4, new Vector2Int(0, -1)));
        var dim = Particles(2, 32, GifKitShape.Square, 60, 1, 1, new Vector2Int(0, -1), 3);
        dim.twinkle = true;
        settings.layers.Add(dim);
        var bright = Particles(3, 33, GifKitShape.Square, 20, 1, 2, new Vector2Int(0, -2), 2);
        bright.twinkle = true;
        settings.layers.Add(bright);
        return settings;
    }

    static GifKitTileSettings Bubbles()
    {
        var settings = Tile(96, 12, "bubbles", 0x03141C, 0x0B3346, 0x2A7F9E, 0x9FE6F5);
        settings.layers.Add(Noise(1, 41, 4, 0.55, 1.2, 0.4, GifKitDither.Bayer8, new Vector2Int(1, 0)));
        settings.layers.Add(Particles(2, 42, GifKitShape.Dot, 30, 2, 4, new Vector2Int(0, -1), 2));
        settings.layers.Add(Particles(3, 43, GifKitShape.Dot, 25, 1, 2, new Vector2Int(0, -2), 1));
        return settings;
    }

    static GifKitTileSettings Clouds()
    {
        var settings = Tile(96, 12, "clouds", 0x5F9FE0, 0xA9D2F5, 0xFFFFFF);
        settings.layers.Add(Noise(1, 51, 4, 0.6, 1, 1, GifKitDither.None, new Vector2Int(1, 0)));
        settings.layers.Add(Noise(2, 52, 3, 0.66, 1, 1, GifKitDither.None, new Vector2Int(2, 0)));
        return settings;
    }

    static GifKitTileSettings Checker()
    {
        var settings = Tile(64, 16, "checker", 0x1B1A2E, 0x262443);
        settings.layers.Add(Pattern(1, GifKitPattern.Checker, 16, 4, new Vector2Int(1, 1)));
        return settings;
    }

    static GifKitTileSettings Stripes()
    {
        var settings = Tile(64, 16, "stripes", 0xFFC2D1, 0xFFDBE5);
        settings.layers.Add(Pattern(1, GifKitPattern.DiagonalStripes, 32, 16, new Vector2Int(1, 0)));
        return settings;
    }

    static GifKitTileSettings Wallpaper()
    {
        var settings = Tile(96, 12, "wallpaper", 0x16131F, 0x2A2340, 0xF2C94C);
        settings.layers.Add(Pattern(1, GifKitPattern.Dots, 32, 4, new Vector2Int(1, 1)));
        var sprites = GifKitLayer.Create(GifKitLayerType.Sprites, 2, 61);
        sprites.spacing = new Vector2Int(64, 64);
        sprites.spriteScale = 2;
        settings.layers.Add(sprites);
        return settings;
    }

    static GifKitTileSettings NeonGrid()
    {
        var settings = Tile(64, 16, "grid", 0x0D0221, 0x261447, 0xFF3864, 0x2DE2E6);
        settings.layers.Add(Pattern(1, GifKitPattern.Grid, 32, 1, new Vector2Int(0, 1)));
        var stars = Particles(3, 71, GifKitShape.Square, 40, 1, 1, Vector2Int.zero, 0);
        stars.twinkle = true;
        settings.layers.Add(stars);
        settings.layers.Add(Particles(2, 72, GifKitShape.Plus, 6, 1, 1, new Vector2Int(1, 0), 0));
        return settings;
    }

    static GifKitTileSettings Tile(int frames, int fps, string name, params int[] colors)
    {
        var settings = new GifKitTileSettings { frames = frames, fps = fps, fileName = name, palette = new Color[colors.Length] };
        for (int i = 0; i < colors.Length; i++)
            settings.palette[i] = new Color32((byte)(colors[i] >> 16), (byte)(colors[i] >> 8), (byte)colors[i], 255);
        return settings;
    }

    static GifKitLayer Noise(int color, int seed, int scale, double cutoff, double ramp, double density, GifKitDither dither, Vector2Int drift)
    {
        var layer = GifKitLayer.Create(GifKitLayerType.Noise, color, seed);
        layer.scale = scale;
        layer.cutoff = cutoff;
        layer.ramp = ramp;
        layer.density = density;
        layer.dither = dither;
        layer.drift = drift;
        return layer;
    }

    static GifKitLayer Particles(int color, int seed, GifKitShape shape, int count, int minSize, int maxSize, Vector2Int drift, int sway)
    {
        var layer = GifKitLayer.Create(GifKitLayerType.Particles, color, seed);
        layer.shape = shape;
        layer.count = count;
        layer.minSize = minSize;
        layer.maxSize = maxSize;
        layer.drift = drift;
        layer.sway = sway;
        return layer;
    }

    static GifKitLayer Pattern(int color, GifKitPattern pattern, int cell, int thickness, Vector2Int drift)
    {
        var layer = GifKitLayer.Create(GifKitLayerType.Pattern, color, 1);
        layer.pattern = pattern;
        layer.cell = cell;
        layer.thickness = thickness;
        layer.drift = drift;
        return layer;
    }
}
