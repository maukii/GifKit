using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GifKitDriver : MonoBehaviour
{
    public static GifKitDriver Current { get; private set; }

    public string Status { get; private set; } = string.Empty;

    GifKitSettings settings;
    Action<GifKitResult> done;
    Camera target;
    RenderTexture originalTarget;
    RenderTexture capture;
    Vector3 savedPosition;
    Quaternion savedRotation;
    float savedStep;
    bool timeLocked;
    bool poseLocked;
    bool cancelled;
    bool restored;
    GifKitParticleLoop particles;

    public void Begin(GifKitSettings captureSettings, Action<GifKitResult> callback)
    {
        Current = this;
        settings = captureSettings;
        done = callback;
        StartCoroutine(Record());
    }

    public void Cancel()
    {
        cancelled = true;
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeforeRender;
        Camera.onPreCull += HoldPose;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeRender;
        Camera.onPreCull -= HoldPose;
    }

    void OnDestroy()
    {
        Restore();
        if (Current == this) Current = null;
    }

    void LateUpdate()
    {
        HoldPose(target);
    }

    void BeforeRender(ScriptableRenderContext context, Camera cam)
    {
        HoldPose(cam);
    }

    void HoldPose(Camera cam)
    {
        if (poseLocked && cam != null && cam == target)
            cam.transform.SetPositionAndRotation(settings.posePosition, settings.poseRotation);
    }

    IEnumerator Record()
    {
        int scale = Mathf.Max(1, settings.pixelScale);
        int loop = settings.still ? 1 : Mathf.Max(2, settings.frames);
        int blend = settings.still ? 0 : Mathf.Clamp(settings.blendFrames, 0, loop / 2);
        int total = settings.still ? 1 : loop + Mathf.Max(1, blend);
        float step = 1f / Mathf.Clamp(settings.fps, 1, 100);

        target = settings.camera != null ? settings.camera : Camera.main;
        if (settings.source == GifKitSource.Camera && target == null)
        {
            Finish(new GifKitResult { error = "No camera to record. Pick one in the window or tag a camera MainCamera." });
            yield break;
        }

        if (target != null)
        {
            savedPosition = target.transform.position;
            savedRotation = target.transform.rotation;
            if (settings.pose == GifKitPose.Hold)
            {
                settings.posePosition = savedPosition;
                settings.poseRotation = savedRotation;
            }
            poseLocked = settings.pose != GifKitPose.Free;
        }

        int width = 0, height = 0;
        Texture2D grab = null;
        if (settings.source == GifKitSource.Camera)
        {
            width = Mathf.Max(1, settings.width / scale);
            height = Mathf.Max(1, settings.height / scale);
            originalTarget = target.targetTexture;
            capture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            target.targetTexture = capture;
            grab = new Texture2D(width, height, TextureFormat.RGBA32, false);
        }

        if (!settings.still)
        {
            savedStep = Time.captureDeltaTime;
            Time.captureDeltaTime = step;
            timeLocked = true;
            if (settings.loopParticles)
            {
                Status = "Simulating particles";
                particles = GifKitParticleLoop.Record(loop, step);
            }
        }

        var frames = new List<Color32[]>(total);
        yield return null;
        particles?.Apply(0);
        yield return null;
        for (int i = 0; i < total && !cancelled; i++)
        {
            Status = settings.still ? "Capturing" : $"Frame {i + 1} / {total}";
            if (settings.source == GifKitSource.Camera)
            {
                frames.Add(ReadCamera(grab, width, height));
            }
            else
            {
                yield return new WaitForEndOfFrame();
                frames.Add(ReadScreen(scale, out width, out height));
            }
            particles?.Apply(i + 1);
            yield return null;
        }

        if (grab != null) GifKitImage.Release(grab);
        Restore();
        if (cancelled)
        {
            Finish(new GifKitResult { error = "Recording cancelled." });
            yield break;
        }

        Status = "Encoding";
        yield return null;
        GifKitResult result;
        try
        {
            result = GifKitWriter.Write(settings, frames, width, height, scale, loop, blend);
        }
        catch (Exception exception)
        {
            result = new GifKitResult { error = exception.Message };
        }
        Finish(result);
    }

    Color32[] ReadCamera(Texture2D grab, int width, int height)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = capture;
        grab.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        grab.Apply(false);
        RenderTexture.active = previous;
        return GifKitImage.FlipRows(grab.GetPixels32(), width, height);
    }

    Color32[] ReadScreen(int scale, out int width, out int height)
    {
        var shot = ScreenCapture.CaptureScreenshotAsTexture();
        var pixels = GifKitImage.FlipRows(shot.GetPixels32(), shot.width, shot.height);
        var result = GifKitImage.Downsample(pixels, shot.width, shot.height, scale, out width, out height);
        GifKitImage.Release(shot);
        return result;
    }

    void Restore()
    {
        if (restored) return;
        restored = true;
        poseLocked = false;
        if (target != null)
        {
            if (capture != null) target.targetTexture = originalTarget;
            if (settings != null && settings.pose != GifKitPose.Free)
                target.transform.SetPositionAndRotation(savedPosition, savedRotation);
        }
        if (capture != null)
        {
            capture.Release();
            GifKitImage.Release(capture);
            capture = null;
        }
        if (timeLocked) Time.captureDeltaTime = savedStep;
        timeLocked = false;
        particles?.Release();
        particles = null;
    }

    void Finish(GifKitResult result)
    {
        Restore();
        if (Current == this) Current = null;
        var callback = done;
        done = null;
        Destroy(gameObject);
        callback?.Invoke(result);
    }
}
