using System;
using System.Collections.Generic;
using UnityEngine;

public enum GifKitLayerType
{
    Noise,
    Particles,
    Pattern,
    Sprites
}

public enum GifKitDither
{
    None,
    Bayer2,
    Bayer4,
    Bayer8
}

public enum GifKitShape
{
    Square,
    Dot,
    Plus,
    Streak,
    Sprite
}

public enum GifKitPattern
{
    Checker,
    HorizontalStripes,
    VerticalStripes,
    DiagonalStripes,
    Dots,
    Grid,
    Bricks
}

[Serializable]
public class GifKitLayer
{
    public GifKitLayerType type;
    public bool enabled = true;
    public bool expanded = true;
    public int color = 1;
    public int seed = 1;
    public Vector2Int drift;

    public int scale = 3;
    public double cutoff = 0.55;
    public double ramp = 0.6;
    public double density = 0.15;
    public GifKitDither dither = GifKitDither.Bayer4;

    public GifKitShape shape = GifKitShape.Square;
    public int count = 60;
    public int minSize = 1;
    public int maxSize = 1;
    public int sway;
    public int length = 5;
    public bool twinkle;

    public GifKitPattern pattern = GifKitPattern.Checker;
    public int cell = 16;
    public int thickness = 4;

    public Texture2D[] sprites = new Texture2D[0];
    public Vector2Int spacing = new Vector2Int(32, 32);
    public bool stagger = true;
    public int spriteScale = 1;
    public bool tint = true;

    public static GifKitLayer Create(GifKitLayerType type, int color, int seed)
    {
        var layer = new GifKitLayer { type = type, color = color, seed = seed };
        switch (type)
        {
            case GifKitLayerType.Particles:
                layer.drift = new Vector2Int(0, 1);
                layer.sway = 2;
                break;
            case GifKitLayerType.Pattern:
                layer.drift = new Vector2Int(1, 1);
                break;
            case GifKitLayerType.Sprites:
                layer.drift = new Vector2Int(1, 1);
                break;
        }
        return layer;
    }

    public GifKitLayer Copy()
    {
        var copy = (GifKitLayer)MemberwiseClone();
        copy.sprites = sprites != null ? (Texture2D[])sprites.Clone() : new Texture2D[0];
        return copy;
    }
}

[Serializable]
public class GifKitTileSettings
{
    public int width = 256;
    public int height = 256;
    public int pixelScale = 2;
    public int frames = 96;
    public int fps = 12;
    public Color[] palette = { new Color32(0x10, 0x10, 0x18, 0xFF), new Color32(0xE8, 0xE8, 0xF0, 0xFF) };
    public List<GifKitLayer> layers = new List<GifKitLayer>();
    public string fileName = "tile";

    public GifKitTileSettings Copy()
    {
        var copy = (GifKitTileSettings)MemberwiseClone();
        copy.palette = (Color[])palette.Clone();
        copy.layers = new List<GifKitLayer>();
        foreach (var layer in layers) copy.layers.Add(layer.Copy());
        return copy;
    }
}
