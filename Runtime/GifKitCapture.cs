using System;
using UnityEngine;

public static class GifKitCapture
{
    public static bool Busy => GifKitDriver.Current != null;

    public static string Status => GifKitDriver.Current != null ? GifKitDriver.Current.Status : string.Empty;

    public static void Run(GifKitSettings settings, Action<GifKitResult> done)
    {
        if (Busy || !Application.isPlaying) return;
        var host = new GameObject("GIF Kit") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<GifKitDriver>().Begin(settings.Copy(), done);
    }

    public static void Cancel()
    {
        if (GifKitDriver.Current != null) GifKitDriver.Current.Cancel();
    }
}
