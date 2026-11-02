using System;
using UnityEngine;

public enum GifKitSource
{
    Camera,
    GameView
}

public enum GifKitPose
{
    Free,
    Hold,
    SceneView
}

public enum GifKitPaletteMode
{
    Auto,
    Fixed
}

[Serializable]
public class GifKitSettings
{
    public GifKitSource source = GifKitSource.Camera;
    public Camera camera;
    public GifKitPose pose = GifKitPose.Free;
    public Vector3 posePosition;
    public Quaternion poseRotation = Quaternion.identity;
    public int width = 960;
    public int height = 320;
    public int pixelScale = 1;
    public bool still;
    public int frames = 96;
    public int fps = 10;
    public bool loopParticles = true;
    public int blendFrames = 8;
    public GifKitPaletteMode paletteMode = GifKitPaletteMode.Auto;
    public Color[] palette = new Color[0];
    public Texture2D overlay;
    public Vector2Int overlayPosition;
    public int overlayScale = 1;
    public string outputFolder = "Captures";
    public string fileName = "capture";

    public GifKitSettings Copy()
    {
        var copy = (GifKitSettings)MemberwiseClone();
        copy.palette = palette != null ? (Color[])palette.Clone() : new Color[0];
        return copy;
    }
}

public class GifKitResult
{
    public string path;
    public string error;
    public int width;
    public int height;
    public int frames;
    public int colors;
    public long bytes;
    public int seamPixels = -1;
    public int framePixels = -1;
}
