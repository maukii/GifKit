using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class GifKitWriter
{
    public static GifKitResult Write(GifKitSettings settings, List<Color32[]> frames, int width, int height, int scale, int loop, int blend)
    {
        var result = new GifKitResult { width = width * scale, height = height * scale };
        if (frames.Count == 0)
        {
            result.error = "Nothing was captured.";
            return result;
        }

        if (!settings.still && frames.Count > loop)
        {
            result.seamPixels = GifKitImage.Diff(frames[loop], frames[0]);
            result.framePixels = GifKitImage.Diff(frames[1], frames[0]);
            if (blend > 0) GifKitImage.Blend(frames, loop, blend, width);
            frames.RemoveRange(loop, frames.Count - loop);
        }

        Color32[] overlay = null;
        int overlayWidth = 0, overlayHeight = 0;
        if (settings.overlay != null) overlay = GifKitImage.ReadTexture(settings.overlay, out overlayWidth, out overlayHeight);

        var palette = GifKitPalette.Build(frames, overlay, settings.paletteMode, settings.palette);
        byte[] overlayIndices = overlay != null ? palette.Map(overlay) : null;
        var indexed = new List<byte[]>(frames.Count);
        foreach (var frame in frames)
        {
            var pixels = GifKitImage.Upscale(palette.Map(frame), width, height, scale);
            if (overlay != null)
                GifKitImage.Stamp(pixels, result.width, result.height, overlayIndices, overlay, overlayWidth, overlayHeight, settings.overlayPosition, Mathf.Max(1, settings.overlayScale));
            indexed.Add(pixels);
        }

        string name = string.IsNullOrWhiteSpace(settings.fileName) ? "capture" : settings.fileName.Trim();
        string basePath = settings.still ? UniquePath(settings.outputFolder, name, ".png") : UniquePath(settings.outputFolder, name, ".png", ".gif");
        string pngPath = basePath + ".png";
        File.WriteAllBytes(pngPath, GifKitImage.EncodePng(indexed[0], palette.Colors, result.width, result.height));
        result.path = pngPath;

        if (!settings.still)
        {
            string gifPath = basePath + ".gif";
            int delay = Mathf.Max(2, Mathf.RoundToInt(100f / Mathf.Max(1, settings.fps)));
            File.WriteAllBytes(gifPath, GifKitEncoder.Encode(result.width, result.height, palette.Colors, indexed, delay));
            result.path = gifPath;
        }

        result.path = Path.GetFullPath(result.path);
        result.bytes = new FileInfo(result.path).Length;
        result.frames = indexed.Count;
        result.colors = palette.Colors.Length;
        return result;
    }

    public static string UniquePath(string folder, string name, params string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(folder)) folder = "Captures";
        Directory.CreateDirectory(folder);
        string basePath = Path.Combine(folder, name);
        int number = 2;
        while (Taken(basePath, extensions)) basePath = Path.Combine(folder, $"{name} {number++}");
        return basePath;
    }

    static bool Taken(string basePath, string[] extensions)
    {
        foreach (var extension in extensions)
            if (File.Exists(basePath + extension)) return true;
        return false;
    }
}
