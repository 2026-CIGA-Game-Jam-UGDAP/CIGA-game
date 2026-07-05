using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 一键搭建 URP 后处理体系：Volume Profile + 光晕材质 + 星球 Glow + 场景 Volume。
/// 菜单 Tools → Setup Post Processing，运行一次即可。
/// </summary>
public static class PostProcessSetup
{
    const string ProfilesDir = "Assets/Settings/Profiles";
    const string VFXDir = "Assets/Art/VFX";

    // ── 场景色调预设 ──
    static readonly (string name, Color filter, Color lift, Color gain, string desc)[] ScenePresets =
    {
        ("Profile_Level1", new Color(0.33f, 0.33f, 0.67f), new Color(0.10f, 0.13f, 0.27f), new Color(1f, 0.93f, 0.80f), "蓝紫 · 开局探索"),
        ("Profile_Level2", new Color(0.33f, 0.47f, 0.53f), new Color(0.10f, 0.15f, 0.22f), new Color(0.90f, 0.95f, 1f), "青蓝 · 深入太空"),
        ("Profile_Level3", new Color(0.33f, 0.47f, 0.53f), new Color(0.10f, 0.15f, 0.22f), new Color(0.90f, 0.95f, 1f), "青蓝 · 深入太空"),
        ("Profile_Level4", new Color(0.53f, 0.27f, 0.33f), new Color(0.22f, 0.08f, 0.13f), new Color(1f, 0.85f, 0.80f), "红紫 · Boss 紧张"),
        ("Profile_Lobby", new Color(0.27f, 0.40f, 0.40f), new Color(0.12f, 0.16f, 0.18f), new Color(0.95f, 0.97f, 1f), "中性 · 大厅安全区"),
    };

    // ── 星球颜色（对应球1~球6的 PNG）──
    static readonly Color[] PlanetGlowColors =
    {
        new Color(1f,    0.95f, 0.80f), // 球1 暖黄
        new Color(0.70f, 0.85f, 1f),    // 球2 淡蓝
        new Color(1f,    0.80f, 0.70f), // 球3 暖橙
        new Color(0.85f, 0.75f, 1f),    // 球4 淡紫
        new Color(0.70f, 1f,   0.85f),  // 球5 青绿
        new Color(1f,    0.70f, 0.75f), // 球6 粉红
    };

    [MenuItem("Tools/Setup Post Processing")]
    public static void RunAll()
    {
        AssetDatabase.DisallowAutoRefresh();
        try
        {
            EnsureDirectories();
            var glowTex = CreateGlowTexture();
            var glowMat = CreateGlowMaterial(glowTex);
            var profiles = CreateVolumeProfiles();
            AddGlowToPlanets(glowMat);
            AddVolumeToScenes(profiles);
            Debug.Log("[PostProcessSetup] ✅ 全部完成！打开 Level1 跑起来看效果，在 Volume Profile 里微调参数。");
        }
        finally
        {
            AssetDatabase.AllowAutoRefresh();
            AssetDatabase.Refresh();
        }
    }

    // ═══════════════════════════════════════════════════
    // 目录
    // ═══════════════════════════════════════════════════
    static void EnsureDirectories()
    {
        foreach (var d in new[] { ProfilesDir, VFXDir })
            if (!AssetDatabase.IsValidFolder(d))
            {
                var parts = d.Split('/');
                AssetDatabase.CreateFolder(string.Join("/", parts, 0, parts.Length - 1), parts[^1]);
            }
    }

    // ═══════════════════════════════════════════════════
    // 光晕贴图（程序化径向渐变，用户后补 PNG）
    // ═══════════════════════════════════════════════════
    static Texture2D CreateGlowTexture()
    {
        string path = $"{VFXDir}/PlanetGlow.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
        {
            Debug.Log($"[PostProcessSetup] 光晕贴图已存在: {path}，跳过生成");
            return existing;
        }

        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color[size * size];
        float center = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float alpha = 1f - Mathf.Clamp01(dist);
                alpha = Mathf.Pow(alpha, 1.5f); // 中心更实，边缘更柔
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, png);
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 100;
        importer.SaveAndReimport();

        Debug.Log($"[PostProcessSetup] 生成光晕贴图: {path}");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ═══════════════════════════════════════════════════
    // 光晕材质（Sprite-Unlit，高亮输出触发 Bloom）
    // ═══════════════════════════════════════════════════
    static Material CreateGlowMaterial(Texture2D tex)
    {
        string path = $"{VFXDir}/PlanetGlow.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
        {
            Debug.LogWarning("[PostProcessSetup] 未找到 Sprite-Unlit-Default shader，用默认 Sprite 材质");
            shader = Shader.Find("Sprites/Default");
        }

        var mat = new Material(shader);
        mat.mainTexture = tex;
        mat.color = Color.white;
        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[PostProcessSetup] 创建光晕材质: {path}");
        return mat;
    }

    // ═══════════════════════════════════════════════════
    // Volume Profile（每场景一个）
    // ═══════════════════════════════════════════════════
    static VolumeProfile[] CreateVolumeProfiles()
    {
        var profiles = new VolumeProfile[ScenePresets.Length];

        for (int i = 0; i < ScenePresets.Length; i++)
        {
            var preset = ScenePresets[i];
            string path = $"{ProfilesDir}/{preset.name}.asset";

            // 已存在则复用
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null)
            {
                profiles[i] = existing;
                Debug.Log($"[PostProcessSetup] Profile 已存在: {preset.name}，跳过");
                continue;
            }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // ── Bloom ──
            var bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.7f);
            bloom.highQualityFiltering.Override(false);
            bloom.tint.Override(Color.white);

            // ── Color Adjustments ──
            var ca = profile.Add<ColorAdjustments>();
            ca.active = true;
            ca.contrast.Override(10f);
            ca.saturation.Override(5f);
            ca.colorFilter.Override(preset.filter);
            ca.postExposure.Override(0f);

            // ── Vignette ──
            var vig = profile.Add<Vignette>();
            vig.active = true;
            vig.intensity.Override(0.35f);
            vig.smoothness.Override(0.3f);
            vig.color.Override(new Color(0.04f, 0.05f, 0.09f)); // 深蓝黑 #0A0C18
            vig.rounded.Override(false);

            // ── Lift Gamma Gain ──
            var lgg = profile.Add<LiftGammaGain>();
            lgg.active = true;
            lgg.lift.Override(preset.lift);
            lgg.gamma.Override(new Color(1f, 1f, 1f, 1f));
            lgg.gain.Override(preset.gain);

            AssetDatabase.CreateAsset(profile, path);
            profiles[i] = profile;
            Debug.Log($"[PostProcessSetup] 创建 Volume Profile: {preset.name} ({preset.desc})");
        }

        return profiles;
    }

    // ═══════════════════════════════════════════════════
    // 星球 Prefab 加 Glow 子对象
    // ═══════════════════════════════════════════════════
    static void AddGlowToPlanets(Material glowMat)
    {
        for (int i = 1; i <= 6; i++)
        {
            string prefabPath = $"Assets/Prefabs/球{i}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[PostProcessSetup] 未找到星球 Prefab: {prefabPath}");
                continue;
            }

            // 检查是否已有 Glow 子对象
            var existingGlow = prefab.transform.Find("Glow");
            if (existingGlow != null)
            {
                Debug.Log($"[PostProcessSetup] 球{i} 已有 Glow，跳过");
                continue;
            }

            // 用 PrefabUtility 编辑 Prefab
            var root = PrefabUtility.LoadPrefabContents(prefabPath);

            // 读取星球 SpriteRenderer 获取尺寸
            var sr = root.GetComponent<SpriteRenderer>();
            float planetSize = sr != null ? Mathf.Max(sr.size.x, sr.size.y) : 10f;

            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(root.transform);
            glowGO.transform.localPosition = Vector3.zero;
            glowGO.transform.localRotation = Quaternion.identity;

            // Glow 比例：让光晕比星球大约 40-50%
            float glowScale = (planetSize * 1.45f) / 10f; // 归一化：10px/unit sprite，我们需要 ~1.45x
            glowGO.transform.localScale = Vector3.one * glowScale;

            var glowSR = glowGO.AddComponent<SpriteRenderer>();
            var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{VFXDir}/PlanetGlow.png");
            glowSR.sprite = glowSprite;
            glowSR.material = glowMat;
            glowSR.color = PlanetGlowColors[i - 1];
            glowSR.sortingOrder = -1;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log($"[PostProcessSetup] 球{i} 已添加 Glow 子对象 (颜色: {PlanetGlowColors[i - 1]})");
        }
    }

    // ═══════════════════════════════════════════════════
    // 场景加 Global Volume + EnvironmentLight
    // ═══════════════════════════════════════════════════
    static void AddVolumeToScenes(VolumeProfile[] profiles)
    {
        // 场景 → Profile 映射（Level0 共享 Level1 蓝紫）
        (string path, int profileIdx)[] sceneMap =
        {
            ("Assets/Scenes/Level0.unity", 0),
            ("Assets/Scenes/Level1.unity", 0),
            ("Assets/Scenes/Level2.unity", 1),
            ("Assets/Scenes/Level3.unity", 2),
            ("Assets/Scenes/Level4.unity", 3),
            ("Assets/Scenes/Lobby.unity",  4),
        };

        var envLightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnvironmentLight.prefab");

        for (int i = 0; i < sceneMap.Length; i++)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(sceneMap[i].path,
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            // ── PostProcessVolume ──
            var existing = GameObject.Find("PostProcessVolume");
            if (existing == null)
            {
                var volumeGO = new GameObject("PostProcessVolume");
                volumeGO.transform.position = Vector3.zero;

                var volume = volumeGO.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 0;
                volume.profile = profiles[sceneMap[i].profileIdx];
                volume.weight = 1f;

                // 加闪白控制器
                volumeGO.AddComponent<PostProcessController>();
            }

            // ── EnvironmentLight ──
            var existingLight = GameObject.Find("EnvironmentLight");
            if (existingLight == null && envLightPrefab != null)
            {
                var lightGO = (GameObject)PrefabUtility.InstantiatePrefab(envLightPrefab);
                lightGO.name = "EnvironmentLight";
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PostProcessSetup] {System.IO.Path.GetFileName(sceneMap[i].path)}: Volume + EnvLight OK");
        }
    }
}
