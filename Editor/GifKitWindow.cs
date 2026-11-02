using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class GifKitWindow : EditorWindow
{
    const string CaptureKey = "GifKit.Capture";
    const string TileKey = "GifKit.Tile.2";

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
    [SerializeField] int tilePreset;
    [SerializeField] bool playing = true;

    GifKitResult last;
    GifKitTile generator;
    Texture2D preview;
    bool previewDirty = true;
    int shownFrame = -1;
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
        if (tile.layers == null || tile.layers.Count == 0) tile = GifKitTilePresets.Create(0);
        previewDirty = true;
        EditorApplication.update += Tick;
        Undo.undoRedoPerformed += MarkDirty;
    }

    void OnDisable()
    {
        EditorApplication.update -= Tick;
        Undo.undoRedoPerformed -= MarkDirty;
        EditorPrefs.SetString(CaptureKey, EditorJsonUtility.ToJson(settings));
        EditorPrefs.SetString(TileKey, EditorJsonUtility.ToJson(tile));
        if (preview != null) DestroyImmediate(preview);
    }

    void OnInspectorUpdate()
    {
        if (GifKitCapture.Busy) Repaint();
    }

    void Tick()
    {
        if (tab == 1 && playing && generator != null && CurrentFrame() != shownFrame) Repaint();
    }

    void MarkDirty()
    {
        previewDirty = true;
        Repaint();
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
        settings.paletteMode = (GifKitPaletteMode)EditorGUILayout.EnumPopup("Palette", settings.paletteMode);
        if (settings.paletteMode == GifKitPaletteMode.Fixed) settings.palette = ColorsGUI(settings.palette, 1);

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

    void TileGUI()
    {
        Undo.RecordObject(this, "Edit tile");
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();
        tilePreset = EditorGUILayout.Popup("Preset", tilePreset, GifKitTilePresets.Names);
        if (GUILayout.Button("Load", GUILayout.Width(60)))
        {
            tile = GifKitTilePresets.Create(tilePreset);
            GUI.changed = true;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        tile.width = Mathf.Clamp(EditorGUILayout.IntField("Width", tile.width), 8, 1024);
        tile.height = Mathf.Clamp(EditorGUILayout.IntField("Height", tile.height), 8, 1024);
        tile.pixelScale = EditorGUILayout.IntSlider("Pixel scale", tile.pixelScale, 1, 8);
        tile.frames = EditorGUILayout.IntSlider("Frames", tile.frames, 2, 256);
        tile.fps = EditorGUILayout.IntSlider("FPS", tile.fps, 1, 50);
        EditorGUILayout.LabelField(" ", $"{tile.width * tile.pixelScale} x {tile.height * tile.pixelScale} px, {(float)tile.frames / tile.fps:0.0} s loop", EditorStyles.miniLabel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
        tile.palette = ColorsGUI(tile.palette, 1);
        var source = (Texture2D)EditorGUILayout.ObjectField("Colours from texture", null, typeof(Texture2D), false);
        if (source != null) tile.palette = Colors(source);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
        int move = -1, direction = 0, remove = -1, duplicate = -1;
        for (int i = 0; i < tile.layers.Count; i++) LayerGUI(i, ref move, ref direction, ref remove, ref duplicate);
        if (move >= 0)
        {
            var layer = tile.layers[move];
            tile.layers.RemoveAt(move);
            tile.layers.Insert(move + direction, layer);
        }
        if (duplicate >= 0) tile.layers.Insert(duplicate + 1, tile.layers[duplicate].Copy());
        if (remove >= 0) tile.layers.RemoveAt(remove);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add layer"))
        {
            var menu = new GenericMenu();
            foreach (GifKitLayerType type in System.Enum.GetValues(typeof(GifKitLayerType)))
            {
                var picked = type;
                menu.AddItem(new GUIContent(type.ToString()), false, () => AddLayer(picked));
            }
            menu.ShowAsContext();
        }
        if (GUILayout.Button("Shuffle all seeds"))
            foreach (var layer in tile.layers) layer.seed = NewSeed();
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck()) previewDirty = true;

        EditorGUILayout.Space();
        PreviewGUI();

        EditorGUILayout.Space();
        OutputGUI(ref tile.fileName);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save PNG", GUILayout.Height(28))) last = Generator().Save(settings.outputFolder, false);
        if (GUILayout.Button("Save GIF", GUILayout.Height(28))) last = Generator().Save(settings.outputFolder, true);
        if (GUILayout.Button("Save texture asset", GUILayout.Height(28)))
        {
            string path = EditorUtility.SaveFilePanelInProject("Save tile texture", tile.fileName, "png", "Saves the first frame as a repeating, point-filtered texture.");
            if (!string.IsNullOrEmpty(path)) last = Generator().SaveAsset(path);
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();
    }

    void LayerGUI(int index, ref int move, ref int direction, ref int remove, ref int duplicate)
    {
        var layer = tile.layers[index];
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        layer.enabled = EditorGUILayout.Toggle(layer.enabled, GUILayout.Width(16));
        layer.expanded = EditorGUILayout.Foldout(layer.expanded, $"{index + 1}. {LayerTitle(layer)}", true);
        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(index == 0))
        {
            if (GUILayout.Button("↑", EditorStyles.miniButtonLeft, GUILayout.Width(24)))
            {
                move = index;
                direction = -1;
            }
        }
        using (new EditorGUI.DisabledScope(index == tile.layers.Count - 1))
        {
            if (GUILayout.Button("↓", EditorStyles.miniButtonMid, GUILayout.Width(24)))
            {
                move = index;
                direction = 1;
            }
        }
        if (GUILayout.Button("Copy", EditorStyles.miniButtonMid, GUILayout.Width(42))) duplicate = index;
        if (GUILayout.Button("Remove", EditorStyles.miniButtonRight, GUILayout.Width(56))) remove = index;
        EditorGUILayout.EndHorizontal();

        if (layer.expanded)
        {
            EditorGUI.indentLevel++;
            layer.color = ColorIndexGUI("Colour", layer.color);
            layer.drift = EditorGUILayout.Vector2IntField(new GUIContent("Move per loop", "Whole tiles per loop, positive is right and down"), layer.drift);
            EditorGUILayout.BeginHorizontal();
            layer.seed = EditorGUILayout.IntField("Seed", layer.seed);
            if (GUILayout.Button("Shuffle", GUILayout.Width(60))) layer.seed = NewSeed();
            EditorGUILayout.EndHorizontal();

            switch (layer.type)
            {
                case GifKitLayerType.Noise: NoiseGUI(layer); break;
                case GifKitLayerType.Particles: ParticlesGUI(layer); break;
                case GifKitLayerType.Pattern: PatternGUI(layer); break;
                default: SpritesGUI(layer); break;
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
    }

    void NoiseGUI(GifKitLayer layer)
    {
        layer.scale = EditorGUILayout.IntSlider("Scale", layer.scale, 1, 16);
        layer.cutoff = DoubleSlider("Cutoff", layer.cutoff, 0f, 1f);
        layer.dither = (GifKitDither)EditorGUILayout.EnumPopup("Dither", layer.dither);
        if (layer.dither == GifKitDither.None) return;
        layer.ramp = DoubleSlider("Ramp", layer.ramp, 0.05f, 5f);
        layer.density = DoubleSlider("Max density", layer.density, 0f, 1f);
    }

    void ParticlesGUI(GifKitLayer layer)
    {
        layer.shape = (GifKitShape)EditorGUILayout.EnumPopup("Shape", layer.shape);
        layer.count = EditorGUILayout.IntSlider("Count", layer.count, 0, 1000);
        layer.minSize = EditorGUILayout.IntSlider("Min size", layer.minSize, 1, 16);
        layer.maxSize = EditorGUILayout.IntSlider("Max size", Mathf.Max(layer.minSize, layer.maxSize), layer.minSize, 16);
        layer.sway = EditorGUILayout.IntSlider("Sway", layer.sway, 0, 16);
        layer.twinkle = EditorGUILayout.Toggle("Twinkle", layer.twinkle);
        if (layer.shape == GifKitShape.Streak) layer.length = EditorGUILayout.IntSlider("Length", layer.length, 1, 64);
        if (layer.shape == GifKitShape.Sprite) SpriteListGUI(layer);
    }

    void PatternGUI(GifKitLayer layer)
    {
        layer.pattern = (GifKitPattern)EditorGUILayout.EnumPopup("Pattern", layer.pattern);
        layer.cell = EditorGUILayout.IntSlider("Size", layer.cell, 2, 256);
        if (layer.pattern != GifKitPattern.Checker)
            layer.thickness = EditorGUILayout.IntSlider("Thickness", layer.thickness, 1, layer.cell);
    }

    void SpritesGUI(GifKitLayer layer)
    {
        SpriteListGUI(layer);
        layer.spacing = EditorGUILayout.Vector2IntField("Spacing", layer.spacing);
        layer.stagger = EditorGUILayout.Toggle("Stagger rows", layer.stagger);
    }

    void SpriteListGUI(GifKitLayer layer)
    {
        var sprites = new List<Texture2D>(layer.sprites ?? new Texture2D[0]);
        int remove = -1;
        for (int i = 0; i < sprites.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            sprites[i] = (Texture2D)EditorGUILayout.ObjectField($"Sprite {i + 1}", sprites[i], typeof(Texture2D), false);
            if (GUILayout.Button("-", GUILayout.Width(22))) remove = i;
            EditorGUILayout.EndHorizontal();
        }
        if (remove >= 0) sprites.RemoveAt(remove);
        var added = (Texture2D)EditorGUILayout.ObjectField(sprites.Count == 0 ? "Sprite" : "Add sprite", null, typeof(Texture2D), false);
        if (added != null) sprites.Add(added);
        if (sprites.Count == 0) EditorGUILayout.LabelField(" ", "No sprite yet, drawing a diamond", EditorStyles.miniLabel);
        layer.sprites = sprites.ToArray();
        layer.spriteScale = EditorGUILayout.IntSlider("Sprite scale", layer.spriteScale, 1, 8);
        layer.tint = EditorGUILayout.Toggle("Tint with colour", layer.tint);
    }

    void PreviewGUI()
    {
        var current = Generator();
        foreach (var warning in current.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        playing = GUILayout.Toggle(playing, playing ? "Pause" : "Play", EditorStyles.miniButton, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        int frame = playing ? CurrentFrame() : 0;
        if (frame != shownFrame)
        {
            current.Draw(preview, frame);
            shownFrame = frame;
        }
        var rect = GUILayoutUtility.GetRect(256f, 320f, GUILayout.ExpandWidth(true));
        float scaledWidth = current.Width * current.Scale;
        float scaledHeight = current.Height * current.Scale;
        GUI.DrawTextureWithTexCoords(rect, preview, new Rect(0f, 0f, rect.width / scaledWidth, rect.height / scaledHeight));
        EditorGUILayout.LabelField(" ", "Repeated to show the seams line up", EditorStyles.miniLabel);
    }

    GifKitTile Generator()
    {
        if (!previewDirty && generator != null && preview != null) return generator;
        generator = new GifKitTile(tile);
        if (preview != null) DestroyImmediate(preview);
        preview = generator.CreatePreview();
        shownFrame = -1;
        previewDirty = false;
        return generator;
    }

    int CurrentFrame() => (int)(EditorApplication.timeSinceStartup * Mathf.Max(1, tile.fps)) % Mathf.Max(1, tile.frames);

    void AddLayer(GifKitLayerType type)
    {
        Undo.RecordObject(this, "Add layer");
        tile.layers.Add(GifKitLayer.Create(type, Mathf.Min(1, tile.palette.Length - 1), NewSeed()));
        previewDirty = true;
        Repaint();
    }

    static string LayerTitle(GifKitLayer layer)
    {
        switch (layer.type)
        {
            case GifKitLayerType.Particles: return $"Particles ({layer.shape}, {layer.count})";
            case GifKitLayerType.Pattern: return $"Pattern ({layer.pattern})";
            case GifKitLayerType.Sprites: return "Sprites";
            default: return "Noise";
        }
    }

    int ColorIndexGUI(string label, int index)
    {
        int count = Mathf.Max(1, tile.palette.Length);
        var names = new string[count];
        for (int i = 0; i < count; i++) names[i] = i == 0 ? "1 (background)" : (i + 1).ToString();
        EditorGUILayout.BeginHorizontal();
        index = EditorGUILayout.Popup(label, Mathf.Clamp(index, 0, count - 1), names);
        var swatch = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f));
        if (tile.palette.Length > 0) EditorGUI.DrawRect(swatch, tile.palette[index]);
        EditorGUILayout.EndHorizontal();
        return index;
    }

    static Color[] ColorsGUI(Color[] colors, int minimum)
    {
        var list = new List<Color>(colors ?? new Color[0]);
        int remove = -1;
        for (int i = 0; i < list.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            list[i] = EditorGUILayout.ColorField(new GUIContent($"Colour {i + 1}"), list[i], true, false, false);
            using (new EditorGUI.DisabledScope(list.Count <= minimum))
            {
                if (GUILayout.Button("-", GUILayout.Width(22))) remove = i;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (remove >= 0) list.RemoveAt(remove);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(EditorGUIUtility.labelWidth);
        if (GUILayout.Button("Add colour") && list.Count < 255) list.Add(list.Count > 0 ? list[list.Count - 1] : Color.white);
        EditorGUILayout.EndHorizontal();
        return list.ToArray();
    }

    static Color[] Colors(Texture2D texture)
    {
        var pixels = GifKitImage.ReadTexture(texture, out _, out _);
        var seen = new HashSet<int>();
        var colors = new List<Color>();
        foreach (var pixel in pixels)
        {
            if (pixel.a < 128) continue;
            int key = (pixel.r << 16) | (pixel.g << 8) | pixel.b;
            if (!seen.Add(key)) continue;
            colors.Add(new Color32(pixel.r, pixel.g, pixel.b, 255));
            if (colors.Count == 32) break;
        }
        return colors.Count > 0 ? colors.ToArray() : new[] { Color.black };
    }

    static double DoubleSlider(string label, double value, float min, float max)
    {
        float shown = (float)value;
        float picked = EditorGUILayout.Slider(label, shown, min, max);
        return picked == shown ? value : picked;
    }

    static int NewSeed() => Random.Range(1, int.MaxValue);

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
