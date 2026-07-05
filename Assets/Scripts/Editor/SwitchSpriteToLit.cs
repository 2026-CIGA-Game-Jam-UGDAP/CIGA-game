using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 批量切换 Sprite 材质：Unlit ↔ ForwardLit（自定义 shader，响应 ambient + 主方向光）。
/// Glow 子对象始终保持 Unlit（需持续高亮触发 Bloom）。
/// </summary>
public static class SwitchSpriteToLit
{
    // 自定义 ForwardLit 材质（响应 ambient + 灯光）
    const string LitMatPath   = "Assets/Art/VFX/SpriteLit.mat";
    // URP 内置 Unlit 材质（回滚用）
    const string UnlitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    [MenuItem("Tools/Switch Sprites to Lit")]
    static void SwitchToLit()
    {
        // 确保自定义材质存在
        var mat = GetOrCreateLitMaterial();
        if (mat == null) return;
        SwitchAll(mat, "Lit");
    }

    [MenuItem("Tools/Switch Sprites to Unlit (回滚)")]
    static void SwitchToUnlit()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(UnlitMatPath);
        if (mat == null)
        {
            Debug.LogError($"[SwitchSprite] 未找到 Unlit 材质: {UnlitMatPath}");
            return;
        }
        SwitchAll(mat, "Unlit");
    }

    static Material GetOrCreateLitMaterial()
    {
        // 已有则复用
        var existing = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);
        if (existing != null) return existing;

        var shader = Shader.Find("Custom/2D/Sprite-ForwardLit");
        if (shader == null)
        {
            Debug.LogError("[SwitchSprite] 未找到 Custom/2D/Sprite-ForwardLit shader！确认 Assets/Shaders/Sprite-ForwardLit.shader 存在且编译无报错");
            return null;
        }

        var mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, LitMatPath);
        Debug.Log($"[SwitchSprite] 创建材质: {LitMatPath}");
        return mat;
    }

    static void SwitchAll(Material mat, string label)
    {
        int switched = 0, skipped = 0;

        // ── 1. 所有 .prefab ──
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
            bool dirty = false;
            foreach (var sr in srs)
            {
                if (IsGlow(sr)) { skipped++; continue; }
                sr.sharedMaterial = mat;
                dirty = true;
                switched++;
            }
            if (dirty)
                PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ── 2. 所有 .unity ──
        var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        foreach (var guid in sceneGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            bool dirty = false;
            foreach (var sr in Object.FindObjectsOfType<SpriteRenderer>(true))
            {
                if (IsGlow(sr)) { skipped++; continue; }
                sr.sharedMaterial = mat;
                dirty = true;
                switched++;
            }
            if (dirty) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.Refresh();
        Debug.Log($"[SwitchSprite] ✅ 切换完成：{switched} 个 → {label}, {skipped} 个 Glow 保持 Unlit");
    }

    // ──────────────────────────────────────
    // Rope：MeshRenderer 需要 URP Lit 材质（响应 ambient + 灯光）
    // ──────────────────────────────────────
    const string RopeLitMatPath = "Assets/Art/VFX/RopeLit.mat";
    const string RopePrefabPath = "Assets/Prefabs/Rope.prefab";

    [MenuItem("Tools/Fix Rope Material")]
    static void FixRopeMaterial()
    {
        // 1. 创建 URP Lit 材质（如不存在）
        var ropeMat = AssetDatabase.LoadAssetAtPath<Material>(RopeLitMatPath);
        if (ropeMat == null)
        {
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                Debug.LogError("[FixRope] 未找到 URP Lit shader！");
                return;
            }
            ropeMat = new Material(litShader);
            ropeMat.color = new Color(0.55f, 0.55f, 0.6f); // 暗灰色绳子
            AssetDatabase.CreateAsset(ropeMat, RopeLitMatPath);
            Debug.Log($"[FixRope] 创建 RopeLit 材质: {RopeLitMatPath}");
        }

        // 2. 修复 Rope prefab
        var ropePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RopePrefabPath);
        if (ropePrefab == null)
        {
            Debug.LogError($"[FixRope] 未找到 Rope prefab: {RopePrefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(RopePrefabPath);
        var mr = root.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = ropeMat;
            PrefabUtility.SaveAsPrefabAsset(root, RopePrefabPath);
            Debug.Log("[FixRope] ✅ Rope prefab MeshRenderer → URP Lit");
        }
        PrefabUtility.UnloadPrefabContents(root);

        // 3. 修复各场景中的 Rope 实例（可能有材质覆盖）
        var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        foreach (var guid in sceneGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            bool dirty = false;
            foreach (var mr2 in Object.FindObjectsOfType<MeshRenderer>(true))
            {
                if (mr2.name != "Rope") continue;
                if (mr2.sharedMaterial == null || mr2.sharedMaterial == ropeMat) continue;
                mr2.sharedMaterial = ropeMat;
                dirty = true;
            }
            if (dirty) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.Refresh();
        Debug.Log("[FixRope] ✅ 全部场景 Rope 材质已修复");
    }

    static bool IsGlow(SpriteRenderer sr)
    {
        return sr.name.Contains("Glow") ||
               (sr.transform.parent != null && sr.transform.parent.name.Contains("Glow"));
    }
}
