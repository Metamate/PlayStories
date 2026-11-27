using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace PlayStories.EditorTools
{
    /// <summary>
    /// One-click (re)generation of everything that is derived from the raw art/audio:
    /// import settings, font asset, characters, books, library, skin, scene and build settings.
    ///
    /// Menu: PlayStories ▸ Build Content.  Batch: -executeMethod PlayStories.EditorTools.PlayStoriesBuilder.BuildContent
    ///
    /// Existing assets are updated in place (GUIDs kept), so it is safe to re-run after adding art.
    /// NOTE: values you tweak by hand on the generated Character/Book assets are overwritten on
    /// re-run for the fields set here – once you start hand-tuning, stop re-running or edit the tables below.
    /// </summary>
    public static class PlayStoriesBuilder
    {
        const string Root = "Assets/PlayStories";
        const string FarmArt = Root + "/Books/Farm/Art";
        const string FarmAudio = Root + "/Books/Farm/Audio";
        const string DataDir = Root + "/Data";
        const string ScenePath = Root + "/Scenes/Main.unity";
        const string TmpSettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        // ------------------------------------------------------------------ entry points
        [MenuItem("PlayStories/1. Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (File.Exists(TmpSettings)) { Debug.Log("[PlayStories] TMP essentials already present"); return; }
            var pkg = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
            if (pkg == null) { Debug.LogError("[PlayStories] TMP Essential Resources.unitypackage not found"); return; }
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
            Debug.Log("[PlayStories] Imported " + pkg);
        }

        [MenuItem("PlayStories/2. Build Content")]
        public static void BuildContent()
        {
            if (!File.Exists(TmpSettings)) ImportTmpEssentials();
            EnsureFolders();
            CopyFont();
            AssetDatabase.Refresh();
            ConfigureTextures();
            ConfigureAudio();
            var skin = BuildSkin();
            var library = BuildBooks(skin);
            BuildScene(library, skin);
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayStories] Content built.");
        }

        [MenuItem("PlayStories/3. Build Windows Test Player")]
        public static void BuildWindowsTest()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/WinTest/PlayStories.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[PlayStories] Build {report.summary.result} – {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        // ------------------------------------------------------------------ folders / font
        static void EnsureFolders()
        {
            foreach (var p in new[] { Root + "/Fonts", DataDir, DataDir + "/Characters", DataDir + "/Books", Root + "/Scenes" })
                Directory.CreateDirectory(p);
        }

        static void CopyFont()
        {
            const string src = "RawAssets/Fonts/Fredoka-SemiBold.ttf";
            string dst = Root + "/Fonts/Fredoka-SemiBold.ttf";
            if (File.Exists(src) && !File.Exists(dst)) File.Copy(src, dst);
            if (File.Exists("RawAssets/Fonts/OFL.txt") && !File.Exists(Root + "/Fonts/Fredoka-OFL.txt"))
                File.Copy("RawAssets/Fonts/OFL.txt", Root + "/Fonts/Fredoka-OFL.txt");
        }

        // ------------------------------------------------------------------ import settings
        static void ConfigureTextures()
        {
            var borders = new Dictionary<string, Vector4>
            {
                { "panel_rounded", new Vector4(72, 72, 72, 72) },
                { "panel_plain", new Vector4(66, 66, 66, 66) },
            };
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png")) continue;
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                string name = Path.GetFileNameWithoutExtension(path);
                bool bigArt = path.Contains("/Books/") || path.Contains("/Covers/");

                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = bigArt; // smoother when drawn smaller on phones
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect; // needed for the mesh deformer
                s.spriteGenerateFallbackPhysicsShape = false;
                ti.SetTextureSettings(s);
                ti.spriteBorder = borders.TryGetValue(name, out var b) ? b : Vector4.zero;

                // high quality ASTC on mobile – hand-drawn crayon texture suffers with coarse blocks
                foreach (var platform in new[] { "Android", "iPhone" })
                {
                    var ps = ti.GetPlatformTextureSettings(platform);
                    ps.overridden = true;
                    ps.maxTextureSize = 2048;
                    ps.format = TextureImporterFormat.ASTC_4x4;
                    ti.SetPlatformTextureSettings(ps);
                }
                ti.SaveAndReimport();
            }
        }

        static void ConfigureAudio()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ai = (AudioImporter)AssetImporter.GetAtPath(path);
                bool ambience = path.Contains("/Ambience/");
                var ss = ai.defaultSampleSettings;
                ss.compressionFormat = AudioCompressionFormat.Vorbis;
                ss.quality = ambience ? 0.6f : 0.75f;
                // short clips are decompressed so we can read their loudness envelope (voice sync)
                ss.loadType = ambience ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                ai.defaultSampleSettings = ss;
                ai.forceToMono = true;
                ai.loadInBackground = ambience;
                ai.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ helpers
        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static Sprite S(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogWarning("[PlayStories] missing sprite " + path);
            return s;
        }

        static LocalizedText T(string danish, string english) => new LocalizedText(danish, english);

        /// <summary>Fills any empty localized text on the skin with the built-in Danish/English defaults.</summary>
        static void FillSkinTexts(UISkin skin)
        {
            var defaults = ScriptableObject.CreateInstance<UISkin>();
            foreach (var f in typeof(UISkin).GetFields())
            {
                if (f.FieldType != typeof(LocalizedText)) continue;
                var cur = (LocalizedText)f.GetValue(skin);
                if (cur == null || cur.IsEmpty) f.SetValue(skin, f.GetValue(defaults));
            }
            Object.DestroyImmediate(defaults);
        }

        static AudioClip A(string path)
        {
            var a = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (a == null) Debug.LogWarning("[PlayStories] missing audio " + path);
            return a;
        }

        // ------------------------------------------------------------------ font
        static void BuildFont(UISkin skin)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/Fredoka-SemiBold.ttf");
            skin.font = font;
            string faPath = Root + "/Fonts/Fredoka SDF.asset";
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(faPath);
            if (fa == null && font != null)
            {
                fa = TMP_FontAsset.CreateFontAsset(font, 96, 12, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
                fa.name = "Fredoka SDF";
                fa.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZÆØÅÉabcdefghijklmnopqrstuvwxyzæøåéü0123456789!?.,:;-–—'’\"“”()+/&%* ", out _);
                AssetDatabase.CreateAsset(fa, faPath);
                foreach (var tex in fa.atlasTextures)
                {
                    tex.name = "Fredoka SDF Atlas";
                    AssetDatabase.AddObjectToAsset(tex, fa);
                }
                fa.material.name = "Fredoka SDF Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                fa.atlasPopulationMode = AtlasPopulationMode.Static; // baked: no font file needed at runtime
                EditorUtility.SetDirty(fa);
                AssetDatabase.SaveAssets();
            }
            skin.fontAsset = fa;
            if (fa == null) return;

            skin.textOutlined = MakeTextMaterial(fa, Root + "/Fonts/Fredoka Outlined.mat", 0.3f, 0.22f, true, skin.outlineColor);
            skin.textOutlinedThin = MakeTextMaterial(fa, Root + "/Fonts/Fredoka Outlined Thin.mat", 0.2f, 0.12f, false, skin.outlineColor);
        }

        static Material MakeTextMaterial(TMP_FontAsset fa, string path, float outline, float dilate, bool shadow, Color outlineColor)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(fa.material);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.CopyPropertiesFromMaterial(fa.material);
            m.shader = fa.material.shader;
            m.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
            m.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
            if (shadow)
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.35f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.15f);
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.35f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
            }
            else m.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ skin
        static UISkin BuildSkin()
        {
            var skin = LoadOrCreate<UISkin>(DataDir + "/Skin.asset");
            FillSkinTexts(skin);
            BuildFont(skin);
            string ui = Root + "/Art/UI/", fx = Root + "/Art/FX/", sfx = Root + "/Audio/UI/";
            skin.arrowLeft = S(ui + "arrow_left.png");
            skin.arrowRight = S(ui + "arrow_right.png");
            skin.back = S(ui + "back.png");
            skin.close = S(ui + "close.png");
            skin.speaker = S(ui + "speaker.png");
            skin.gear = S(ui + "gear.png");
            skin.play = S(ui + "play.png");
            skin.replay = S(ui + "replay.png");
            skin.filter = S(ui + "filter.png");
            skin.lockIcon = S(ui + "lock.png");
            skin.panel = S(ui + "panel_rounded.png");
            skin.panelPlain = S(ui + "panel_plain.png");
            skin.ring = S(ui + "ring.png");
            skin.circle = S(ui + "circle.png");
            skin.bubble = S(ui + "bubble.png");
            skin.shadow = S(fx + "shadow.png");
            skin.heart = S(fx + "heart.png");
            skin.star = S(fx + "star.png");
            skin.sparkle = S(fx + "sparkle.png");
            skin.note = S(fx + "note.png");
            skin.cloud = S(fx + "cloud.png");
            skin.butterfly = S(fx + "butterfly.png");
            skin.welcomeBackground = S(FarmArt + "/farm_background.png");
            skin.welcomeCover = S(Root + "/Art/Covers/cover_farm.png");
            skin.welcomeAmbience = A(Root + "/Audio/Ambience/farm_birds_loop.ogg");
            skin.sfxPop = A(sfx + "pop.ogg");
            skin.sfxTap = A(sfx + "tap_soft.ogg");
            skin.sfxPageTurn = A(sfx + "page_turn.ogg");
            skin.sfxSparkle = A(sfx + "sparkle.ogg");
            skin.sfxOpen = A(sfx + "chime_open.ogg");
            skin.sfxBoing = A(sfx + "boing.ogg");
            skin.sfxTick = A(sfx + "hold_tick.ogg");
            skin.sfxWhoosh = A(sfx + "whoosh.ogg");
            EditorUtility.SetDirty(skin);
            return skin;
        }

        // ------------------------------------------------------------------ characters & books
        struct CharSpec
        {
            public string id, sound;
            public LocalizedText name, word;
            public ReactionStyle reaction;
            public ParticleStyle particles;
            public SoftBone head, tail;
            public float scale, pitch;
        }

        static CharacterDefinition BuildCharacter(CharSpec c)
        {
            var d = LoadOrCreate<CharacterDefinition>($"{DataDir}/Characters/{c.id}.asset");
            d.displayName = c.name;
            d.sprite = S($"{FarmArt}/{c.id}.png");
            d.blinkSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{FarmArt}/{c.id}_blink.png");
            d.sound = A($"{FarmAudio}/Sounds/{c.sound}.ogg");
            d.soundWord = c.word;
            d.soundPitch = c.pitch <= 0 ? 1f : c.pitch;
            d.scale = c.scale;
            d.motion ??= new MotionProfile();
            d.motion.reaction = c.reaction;
            d.motion.particles = c.particles;
            d.motion.head = c.head;
            d.motion.tail = c.tail;
            d.motion.facing = -1;
            EditorUtility.SetDirty(d);
            return d;
        }

        static BookLibrary BuildBooks(UISkin skin)
        {
            // Soft-bone placement was measured on the drawings (normalised, 0,0 = bottom-left).
            var specs = new[]
            {
                new CharSpec { id = "cow", sound = "cow_moo", name = T("Ko", "Cow"), word = T("Muh!", "Moo!"), reaction = ReactionStyle.Nod, particles = ParticleStyle.Notes, scale = 0.92f,
                    head = SoftBone.Make(new Vector2(0.3f, 0.68f), 0.27f, 0.12f, new Vector2(0.38f, 0.42f)),
                    tail = SoftBone.Make(new Vector2(0.93f, 0.33f), 0.12f, 0.06f, new Vector2(0.87f, 0.45f)) },
                new CharSpec { id = "sheep", sound = "sheep_baa", name = T("Får", "Sheep"), word = T("Mæh!", "Baa!"), reaction = ReactionStyle.Hop, particles = ParticleStyle.Hearts, scale = 0.9f,
                    head = SoftBone.Make(new Vector2(0.32f, 0.7f), 0.28f, 0.12f, new Vector2(0.36f, 0.42f)),
                    tail = SoftBone.Make(new Vector2(0.92f, 0.62f), 0.09f, 0.06f, new Vector2(0.86f, 0.6f)) },
                new CharSpec { id = "pig", sound = "pig_oink", name = T("Gris", "Pig"), word = T("Øf øf!", "Oink oink!"), reaction = ReactionStyle.Wiggle, particles = ParticleStyle.Hearts, scale = 0.88f,
                    head = SoftBone.Make(new Vector2(0.27f, 0.63f), 0.3f, 0.1f, new Vector2(0.36f, 0.45f)),
                    tail = SoftBone.Make(new Vector2(0.95f, 0.66f), 0.08f, 0.06f, new Vector2(0.9f, 0.6f)) },
                new CharSpec { id = "hen", sound = "hen_cluck", name = T("Høne", "Chicken"), word = T("Gok gok!", "Cluck cluck!"), reaction = ReactionStyle.Peck, particles = ParticleStyle.Stars, scale = 0.86f,
                    head = SoftBone.Make(new Vector2(0.36f, 0.76f), 0.22f, 0.1f, new Vector2(0.42f, 0.56f)),
                    tail = SoftBone.Make(new Vector2(0.82f, 0.68f), 0.17f, 0.08f, new Vector2(0.72f, 0.55f)) },
                new CharSpec { id = "duck", sound = "duck_quack", name = T("And", "Duck"), word = T("Rap rap!", "Quack!"), reaction = ReactionStyle.Waddle, particles = ParticleStyle.Sparkles, scale = 0.86f,
                    head = SoftBone.Make(new Vector2(0.32f, 0.7f), 0.25f, 0.1f, new Vector2(0.4f, 0.48f)),
                    tail = SoftBone.Make(new Vector2(0.92f, 0.5f), 0.1f, 0.06f, new Vector2(0.86f, 0.45f)) },
            };
            var chars = specs.Select(BuildCharacter).ToArray();
            skin.welcomeCharacters = new[] { chars[0], chars[1] };
            EditorUtility.SetDirty(skin);

            var farm = LoadOrCreate<BookDefinition>(DataDir + "/Books/Farm.asset");
            farm.id = "farm";
            farm.title = T("Bondegårdsdyr", "Farm Animals");
            farm.cover = S(Root + "/Art/Covers/cover_farm.png");
            farm.background = S(FarmArt + "/farm_background.png");
            farm.ambience = A(Root + "/Audio/Ambience/farm_birds_loop.ogg");
            farm.ambienceVolume = 0.35f;
            farm.comingSoon = false;
            farm.minAgeMonths = 0; farm.maxAgeMonths = 24;
            farm.topics = new List<string> { "animals", "farm" };
            farm.themeColor = new Color(0.47f, 0.69f, 0.36f);
            LocalizedClip Voice(string id) => new LocalizedClip(A($"{FarmAudio}/Narration/da/{id}.ogg"), A($"{FarmAudio}/Narration/en/{id}.ogg"));
            farm.pages = new List<PageDefinition>
            {
                // empty label on the cover = the book title
                // narration is only the animal names – cover and end page are silent
                new PageDefinition { kind = PageKind.Cover, label = new LocalizedText(), narration = new LocalizedClip() },
            };
            for (int i = 0; i < specs.Length; i++)
                // empty label on a character page = the character's name
                farm.pages.Add(new PageDefinition { kind = PageKind.Character, label = new LocalizedText(), character = chars[i], narration = Voice(specs[i].id) });
            farm.pages.Add(new PageDefinition { kind = PageKind.End, label = T("Hurra!", "Hooray!"), narration = new LocalizedClip() });
            EditorUtility.SetDirty(farm);

            // placeholders so the library shows how it will grow
            BookDefinition Soon(string id, LocalizedText title, string cover, int min, int max, params string[] topics)
            {
                var b = LoadOrCreate<BookDefinition>($"{DataDir}/Books/{id}.asset");
                b.id = id; b.title = title; b.cover = S($"{Root}/Art/Covers/{cover}.png");
                b.comingSoon = true; b.minAgeMonths = min; b.maxAgeMonths = max;
                b.topics = topics.ToList();
                b.pages ??= new List<PageDefinition>();
                EditorUtility.SetDirty(b);
                return b;
            }

            var lib = LoadOrCreate<BookLibrary>(DataDir + "/Library.asset");
            lib.books = new List<BookDefinition>
            {
                farm,
                Soon("Bus", T("Hjulene på bussen", "The Wheels on the Bus"), "cover_bus", 12, 36, "vehicles", "song"),
                Soon("Clothes", T("Tøj", "Clothes"), "cover_clothes", 0, 12, "clothes"),
                Soon("Feelings", T("Følelser", "Feelings"), "cover_feelings", 12, 36, "feelings"),
                Soon("Weather", T("Vejret", "Weather"), "cover_weather", 6, 24, "weather"),
            };
            EditorUtility.SetDirty(lib);
            return lib;
        }

        // ------------------------------------------------------------------ scene
        static void BuildScene(BookLibrary lib, UISkin skin)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.52f, 0.78f, 0.86f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 100f;
            var urp = camGo.GetComponent<UniversalAdditionalCameraData>() ?? camGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = false;
            urp.antialiasing = AntialiasingMode.None;
            camGo.AddComponent<AudioListener>();

            var es = new GameObject("EventSystem", typeof(EventSystem));
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();

            var app = new GameObject("PlayStories");
            var ctrl = app.AddComponent<AppController>();
            ctrl.library = lib;
            ctrl.skin = skin;
            ctrl.uiCamera = cam;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "PlayStories";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            // locked mode (see KidLock.cs): immersive full screen on Android, edge-swipe deferral +
            // hidden home indicator on iOS
            PlayerSettings.Android.requestedVisibleInsets = AndroidWindowInsetsType.None; // hide status + navigation bars
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.iOS.hideHomeButton = true;
        }
    }
}
