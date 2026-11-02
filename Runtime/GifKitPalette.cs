using System.Collections.Generic;
using UnityEngine;

public class GifKitPalette
{
    public Color32[] Colors { get; private set; }

    readonly Dictionary<int, byte> lookup = new Dictionary<int, byte>();

    public static GifKitPalette Build(List<Color32[]> frames, Color32[] overlay, GifKitPaletteMode mode, Color[] fixedColors)
    {
        var palette = new GifKitPalette();
        if (mode == GifKitPaletteMode.Fixed && fixedColors != null && fixedColors.Length > 0)
        {
            int count = Mathf.Min(fixedColors.Length, 255);
            palette.Colors = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                Color32 color = fixedColors[i];
                color.a = 255;
                palette.Colors[i] = color;
            }
            return palette;
        }

        var counts = new Dictionary<int, int>();
        foreach (var frame in frames) Count(frame, counts, false);
        if (overlay != null) Count(overlay, counts, true);

        if (counts.Count <= 255)
        {
            palette.Colors = new Color32[counts.Count];
            int i = 0;
            foreach (var key in counts.Keys)
            {
                palette.Colors[i] = FromKey(key);
                palette.lookup[key] = (byte)i;
                i++;
            }
        }
        else
        {
            palette.Colors = MedianCut(counts, 255);
        }
        return palette;
    }

    public byte[] Map(Color32[] pixels)
    {
        var result = new byte[pixels.Length];
        int lastKey = -1;
        byte lastIndex = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            int key = Key(pixels[i]);
            if (key != lastKey)
            {
                lastIndex = Index(key);
                lastKey = key;
            }
            result[i] = lastIndex;
        }
        return result;
    }

    byte Index(int key)
    {
        if (lookup.TryGetValue(key, out byte found)) return found;
        int r = (key >> 16) & 255, g = (key >> 8) & 255, b = key & 255;
        int best = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < Colors.Length; i++)
        {
            int dr = Colors[i].r - r, dg = Colors[i].g - g, db = Colors[i].b - b;
            int distance = dr * dr * 2 + dg * dg * 4 + db * db * 3;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }
        lookup[key] = (byte)best;
        return (byte)best;
    }

    static void Count(Color32[] pixels, Dictionary<int, int> counts, bool opaqueOnly)
    {
        int runKey = -1;
        int run = 0;
        foreach (var pixel in pixels)
        {
            if (opaqueOnly && pixel.a < 128) continue;
            int key = Key(pixel);
            if (key == runKey)
            {
                run++;
                continue;
            }
            if (run > 0) Add(counts, runKey, run);
            runKey = key;
            run = 1;
        }
        if (run > 0) Add(counts, runKey, run);
    }

    static void Add(Dictionary<int, int> counts, int key, int amount)
    {
        counts.TryGetValue(key, out int current);
        counts[key] = current + amount;
    }

    static int Key(Color32 color) => (color.r << 16) | (color.g << 8) | color.b;

    static Color32 FromKey(int key) => new Color32((byte)((key >> 16) & 255), (byte)((key >> 8) & 255), (byte)(key & 255), 255);

    static Color32[] MedianCut(Dictionary<int, int> counts, int target)
    {
        var boxes = new List<List<KeyValuePair<int, int>>> { new List<KeyValuePair<int, int>>(counts) };
        while (boxes.Count < target)
        {
            int pick = -1;
            int widest = 0;
            int axis = 0;
            for (int i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].Count < 2) continue;
                Spread(boxes[i], out int range, out int boxAxis);
                if (range <= widest) continue;
                widest = range;
                pick = i;
                axis = boxAxis;
            }
            if (pick < 0) break;

            var box = boxes[pick];
            int shift = 16 - axis * 8;
            box.Sort((a, b) => ((a.Key >> shift) & 255).CompareTo((b.Key >> shift) & 255));
            long total = 0;
            foreach (var entry in box) total += entry.Value;
            long running = 0;
            int split = box.Count / 2;
            for (int i = 0; i < box.Count - 1; i++)
            {
                running += box[i].Value;
                if (running * 2 < total) continue;
                split = i + 1;
                break;
            }
            boxes[pick] = box.GetRange(0, split);
            boxes.Add(box.GetRange(split, box.Count - split));
        }

        var colors = new Color32[boxes.Count];
        for (int i = 0; i < boxes.Count; i++)
        {
            long r = 0, g = 0, b = 0, weight = 0;
            foreach (var entry in boxes[i])
            {
                r += ((entry.Key >> 16) & 255) * (long)entry.Value;
                g += ((entry.Key >> 8) & 255) * (long)entry.Value;
                b += (entry.Key & 255) * (long)entry.Value;
                weight += entry.Value;
            }
            colors[i] = new Color32((byte)(r / weight), (byte)(g / weight), (byte)(b / weight), 255);
        }
        return colors;
    }

    static void Spread(List<KeyValuePair<int, int>> box, out int range, out int axis)
    {
        int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
        foreach (var entry in box)
        {
            int r = (entry.Key >> 16) & 255, g = (entry.Key >> 8) & 255, b = entry.Key & 255;
            if (r < minR) minR = r;
            if (r > maxR) maxR = r;
            if (g < minG) minG = g;
            if (g > maxG) maxG = g;
            if (b < minB) minB = b;
            if (b > maxB) maxB = b;
        }
        int spanR = maxR - minR, spanG = maxG - minG, spanB = maxB - minB;
        axis = spanR >= spanG && spanR >= spanB ? 0 : spanG >= spanB ? 1 : 2;
        range = Mathf.Max(spanR, Mathf.Max(spanG, spanB));
    }
}
