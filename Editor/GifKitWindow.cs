using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class GifKitWindow : EditorWindow
{
    const string CaptureKey = "GifKit.Capture";
    const string TileKey = "GifKit.Tile";

    static readonly string[] Tabs = { "Capture", "Tile" };
    static readonly string[] PresetNames =
    {
        "Custom",
        "itch.io banner (960 x 320)",
        "itch.io cover (630 x 500)",
        "Screenshot (1920 x 1080)",
        "Screenshot (1280 x 720)",
        "Square (512 x 512)"
    };
    static readonly Vector2Int[] PresetSizes =
    {
        Vector2Int.zero,
        new Vector2Int(960, 320),
        new Vector2Int(630, 500),
        new Vector2Int(1920, 1080),
        new Vector2Int(1280, 720),
        new Vector2Int(512, 512)
    };

    [SerializeField] int tab;
    [SerializeField] GifKitSettings settings = new GifKitSettings();
    [SerializeField] GifKitTileSettings tile = new GifKitTileSettings();

    GifKitResult last;
    Texture2D preview;
    bool previewDirty = true;
    Vector2 scroll;

    [MenuItem("Window/GIF Kit")]
    static void Open()
    {
        GetWindow<GifKitWindow>("GIF Kit");
    }

    void OnEnable()
    {
        if (settings == null) settings = new GifKitSettings();
        if (tile == null) tile = new GifKitTileSettings();
        if (EditorPrefs.HasKey(CaptureKey)) EditorJsonUtility.FromJsonOverwrite(EditorPrefs.GetString(CaptureKey), settings);
        if (EditorPrefs.HasKey(TileKey)) EditorJsonUtility.FromJsonOverwrite(EditorPrefs.GetString(TileKey), tile);
        previewDirty = true;
    }

    void OnDisable()
    {
        EditorPrefs.SetString(CaptureKey, EditorJsonUtility.ToJson(settings));
        EditorPrefs.SetString(TileKey, EditorJsonUtility.ToJson(tile));
        if (preview != null) DestroyImmediate(preview);
    }

    void OnInspectorUpdate()
    {
        if (GifKitCapture.Busy) Repaint();
    }

    void OnGUI()
    {
        tab = GUILayout.Toolbar(tab, Tabs);
        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (tab == 0) CaptureGUI();
        else TileGUI();
        ResultGUI();
        EditorGUILayout.EndScrollView();
    }

    void CaptureGUI()
    {
        settings.source = (GifKitSource)EditorGUILayout.EnumPopup("Source", settings.source);
        if (settings.source == GifKitSource.Camera)
        {
            settings.camera = (Camera)EditorGUILayout.ObjectField("Camera", settings.camera, typeof(Camera), true);
            if (settings.camera == null) EditorGUILayout.LabelField(" ", "Main camera", EditorStyles.miniLabel);
            settings.pose = (GifKitPose)EditorGUILayout.EnumPopup("Camera pose", settings.pose);

            int preset = EditorGUILayout.Popup("Size", PresetIndex(), PresetNames);
            if (preset > 0)
            {
                settings.width = PresetSizes[preset].x;
                settings.height = PresetSizes[preset].y;
            }
            settings.width = Mathf.Clamp(EditorGUILayout.IntField("Width", settings.width), 1, 4096);
            settings.height = Mathf.Clamp(EditorGUILayout.IntField("Height", settings.height), 1, 4096);
            settings.pixelScale = EditorGUILayout.IntSlider("Pixel scale", settings.pixelScale, 1, 8);
            if (settings.pixelScale > 1)
                EditorGUILayout.LabelField(" ", $"Renders at {settings.width / settings.pixelScale} x {settings.height / settings.pixelScale}, scaled up {settings.pixelScale}x", EditorStyles.miniLabel);
        }
        else
        {
            settings.pixelScale = EditorGUILayout.IntSlider("Pixel scale", settings.pixelScale, 1, 8);
            EditorGUILayout.HelpBox("Captures the Game view at its current size, UI included. Pixel scale samples one pixel from each block, for games drawn at an integer scale.", MessageType.None);
        }

        EditorGUILayout.Space();
        settings.still = EditorGUILayout.Toggle("Still image", settings.still);
        if (!settings.still)
        {
            settings.frames = EditorGUILayout.IntSlider("Frames", settings.frames, 2, 600);
            settings.fps = EditorGUILayout.IntSlider("FPS", settings.fps, 1, 50);
            EditorGUILayout.LabelField(" ", $"{(float)settings.frames / settings.fps:0.0} s loop", EditorStyles.miniLabel);
            settings.loopParticles = EditorGUILayout.Toggle("Loop particles", settings.loopParticles);
            settings.blendFrames = EditorGUILayout.IntSlider("Blend frames", settings.blendFrames, 0, Mathf.Max(0, settings.frames / 2));
        }

        EditorGUILayout.Space();
        PaletteGUI();

        EditorGUILayout.Space();
        settings.overlay = (Texture2D)EditorGUILayout.ObjectField("Overlay", settings.overlay, typeof(Texture2D), false);
        if (settings.overlay != null)
        {
            settings.overlayPosition = EditorGUILayout.Vector2IntField("Position", settings.overlayPosition);
            settings.overlayScale = EditorGUILayout.IntSlider("Overlay scale", settings.overlayScale, 1, 16);
        }

        EditorGUILayout.Space();
        OutputGUI(ref settings.fileName);

        EditorGUILayout.Space();
        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Enter play mode, set up the moment you want, then record.", MessageType.Info);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || GifKitCapture.Busy))
        {
            if (GUILayout.Button(settings.still ? "Take still" : "Record GIF", GUILayout.Height(28))) Record();
        }

        if (GifKitCapture.Busy)
        {
            EditorGUILayout.LabelField(GifKitCapture.Status);
            if (GUILayout.Button("Cancel")) GifKitCapture.Cancel();
        }
    }

    void PaletteGUI()
    {
        settings.paletteMode = (GifKitPaletteMode)EditorGUILayout.EnumPopup("Palette", settings.paletteMode);
        if (settings.paletteMode != GifKitPaletteMode.Fixed) return;

        var colors = new List<Color>(settings.palette ?? new Color[0]);
        int remove = -1;
        for (int i = 0; i < colors.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            colors[i] = EditorGUILayout.ColorField(new GUIContent($"Colour {i + 1}"), colors[i], true, false, false);
            if (GUILayout.Button("-", GUILayout.Width(22))) remove = i;
            EditorGUILayout.EndHorizontal();
        }
        if (remove >= 0) colors.RemoveAt(remove);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(EditorGUIUtility.labelWidth);
        if (GUILayout.Button("Add colour") && colors.Count < 255) colors.Add(Color.white);
        EditorGUILayout.EndHorizontal();
        settings.palette = colors.ToArray();
    }

    void OutputGUI(ref string fileName)
    {
        EditorGUILayout.BeginHorizontal();
        settings.outputFolder = EditorGUILayout.TextField("Output folder", settings.outputFolder);
        if (GUILayout.Button("...", GUILayout.Width(28)))
        {
            string picked = EditorUtility.OpenFolderPanel("Output folder", settings.outputFolder, string.Empty);
            if (!string.IsNullOrEmpty(picked)) settings.outputFolder = picked;
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();
        fileName = EditorGUILayout.TextField("File name", fileName);
    }

    void TileGUI()
    {
        EditorGUI.BeginChangeCheck();
        tile.size = EditorGUILayout.IntSlider("Tile size", tile.size, 16, 512);
        tile.pixelScale = EditorGUILayout.IntSlider("Pixel scale", tile.pixelScale, 1, 8);
        tile.background = EditorGUILayout.ColorField(new GUIContent("Background"), tile.background, true, false, false);
        tile.foreground = EditorGUILayout.ColorField(new GUIContent("Foreground"), tile.foreground, true, false, false);

        EditorGUILayout.Space();
        tile.fog = EditorGUILayout.Toggle("Dithered fog", tile.fog);
        if (tile.fog)
        {
            tile.fogDensity = EditorGUILayout.Slider("Fog density", tile.fogDensity, 0f, 0.5f);
            tile.fogScale = EditorGUILayout.IntSlider("Fog scale", tile.fogScale, 1, 12);
        }

        EditorGUILayout.Space();
        tile.smallFlakes = EditorGUILayout.IntSlider("Small flakes", tile.smallFlakes, 0, 500);
        tile.largeFlakes = EditorGUILayout.IntSlider("Large flakes", tile.largeFlakes, 0, 300);
        tile.fastFlakes = EditorGUILayout.IntSlider("Fast flakes", tile.fastFlakes, 0, 300);
        tile.frames = EditorGUILayout.IntSlider("Frames", tile.frames, 2, 256);
        tile.fps = EditorGUILayout.IntSlider("FPS", tile.fps, 1, 50);
        tile.seed = EditorGUILayout.IntField("Seed", tile.seed);
        if (EditorGUI.EndChangeCheck()) previewDirty = true;

        int pixels = tile.size * tile.pixelScale;
        EditorGUILayout.LabelField(" ", $"{pixels} x {pixels} px, {(float)tile.frames / tile.fps:0.0} s loop", EditorStyles.miniLabel);

        if (previewDirty || preview == null)
        {
            if (preview != null) DestroyImmediate(preview);
            preview = new GifKitTile(tile).Preview();
            previewDirty = false;
        }
        var rect = GUILayoutUtility.GetRect(256f, 256f, GUILayout.ExpandWidth(true));
        float texel = tile.size * tile.pixelScale;
        GUI.DrawTextureWithTexCoords(rect, preview, new Rect(0f, 0f, rect.width / texel, rect.height / texel));

        EditorGUILayout.Space();
        OutputGUI(ref tile.fileName);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save PNG", GUILayout.Height(28))) last = new GifKitTile(tile).Save(settings.outputFolder, false);
        if (GUILayout.Button("Save GIF", GUILayout.Height(28))) last = new GifKitTile(tile).Save(settings.outputFolder, true);
        EditorGUILayout.EndHorizontal();
    }

    void ResultGUI()
    {
        if (last == null) return;
        EditorGUILayout.Space();
        if (!string.IsNullOrEmpty(last.error))
        {
            EditorGUILayout.HelpBox(last.error, MessageType.Error);
            return;
        }

        string text = $"{Path.GetFileName(last.path)}\n{last.width} x {last.height}, {last.frames} frame{(last.frames == 1 ? "" : "s")}, {last.colors} colours, {last.bytes / 1024} KB";
        if (last.seamPixels >= 0)
            text += $"\nLoop seam: {last.seamPixels} px differ between the last and first frame (a normal step changes {last.framePixels} px)";
        EditorGUILayout.HelpBox(text, MessageType.Info);
        if (GUILayout.Button("Show in folder")) EditorUtility.RevealInFinder(last.path);
    }

    void Record()
    {
        var run = settings.Copy();
        if (run.source == GifKitSource.Camera && run.pose == GifKitPose.SceneView)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                last = new GifKitResult { error = "Open a Scene view to frame the shot from." };
                return;
            }
            run.posePosition = view.camera.transform.position;
            run.poseRotation = view.camera.transform.rotation;
        }
        last = null;
        GifKitCapture.Run(run, result =>
        {
            last = result;
            Repaint();
        });
    }

    int PresetIndex()
    {
        for (int i = 1; i < PresetSizes.Length; i++)
            if (PresetSizes[i].x == settings.width && PresetSizes[i].y == settings.height) return i;
        return 0;
    }
}
