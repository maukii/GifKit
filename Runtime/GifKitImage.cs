using System.Collections.Generic;
using UnityEngine;

public static class GifKitImage
{
    static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

    public static Color32[] FlipRows(Color32[] pixels, int width, int height)
    {
        var flipped = new Color32[pixels.Length];
        for (int y = 0; y < height; y++)
            System.Array.Copy(pixels, (height - 1 - y) * width, flipped, y * width, width);
        return flipped;
    }

    public static Color32[] Downsample(Color32[] pixels, int width, int height, int scale, out int outWidth, out int outHeight)
    {
        outWidth = Mathf.Max(1, width / scale);
        outHeight = Mathf.Max(1, height / scale);
        if (scale == 1) return pixels;
        var result = new Color32[outWidth * outHeight];
        int offset = scale / 2;
        for (int y = 0; y < outHeight; y++)
            for (int x = 0; x < outWidth; x++)
                result[y * outWidth + x] = pixels[(y * scale + offset) * width + x * scale + offset];
        return result;
    }

    public static byte[] Upscale(byte[] pixels, int width, int height, int scale)
    {
        if (scale == 1) return pixels;
        int outWidth = width * scale;
        var result = new byte[outWidth * height * scale];
        for (int y = 0; y < height * scale; y++)
        {
            int row = y / scale * width;
            int outRow = y * outWidth;
            for (int x = 0; x < outWidth; x++) result[outRow + x] = pixels[row + x / scale];
        }
        return result;
    }

    public static int Diff(Color32[] a, Color32[] b)
    {
        int count = 0;
        for (int i = 0; i < a.Length; i++)
            if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b) count++;
        return count;
    }

    public static void Blend(List<Color32[]> frames, int loop, int blend, int width)
    {
        for (int j = 0; j < blend; j++)
        {
            var into = frames[j];
            var from = frames[loop + j];
            float weight = (j + 0.5f) / blend;
            for (int i = 0; i < into.Length; i++)
            {
                int x = i % width;
                int y = i / width;
                if ((Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f >= weight) into[i] = from[i];
            }
        }
    }

    public static void Stamp(byte[] pixels, int width, int height, byte[] overlay, Color32[] source, int overlayWidth, int overlayHeight, Vector2Int position, int scale)
    {
        for (int y = 0; y < overlayHeight * scale; y++)
        {
            int py = position.y + y;
            if (py < 0 || py >= height) continue;
            for (int x = 0; x < overlayWidth * scale; x++)
            {
                int px = position.x + x;
                if (px < 0 || px >= width) continue;
                int o = y / scale * overlayWidth + x / scale;
                if (source[o].a >= 128) pixels[py * width + px] = overlay[o];
            }
        }
    }

    public static Color32[] ReadTexture(Texture2D texture, out int width, out int height)
    {
        width = texture.width;
        height = texture.height;
        if (texture.isReadable) return FlipRows(texture.GetPixels32(), width, height);

        var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(texture, temporary);
        var previous = RenderTexture.active;
        RenderTexture.active = temporary;
        var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        copy.Apply(false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        var pixels = copy.GetPixels32();
        Release(copy);
        return FlipRows(pixels, width, height);
    }

    public static byte[] EncodePng(byte[] pixels, Color32[] palette, int width, int height)
    {
        var colors = new Color32[pixels.Length];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                colors[(height - 1 - y) * width + x] = palette[pixels[y * width + x]];
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(colors);
        texture.Apply(false);
        var png = texture.EncodeToPNG();
        Release(texture);
        return png;
    }

    public static void Release(Object target)
    {
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }
}
