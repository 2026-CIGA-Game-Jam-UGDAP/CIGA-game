using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 批量切换 Sprite 材质：Unlit → Lit，让 Sprite 响应 Ambient 和灯光。
/// Glow 子对象保持 Unlit（需持续高亮触发 Bloom）。
/// 菜单 Tools → Switch Sprites to Lit
/// </summary>
public static class SwitchSpriteToLit
{
    [MenuItem("Tools/Switch Sprites to Lit")]
    static void Run()
    {
        var litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (litShader == null) { Debug.LogError("未找到 Sprite-Lit-Default shader，确认 URP 已安装"); return; }

        var litMat = new Material(litShader);
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
                sr.sharedMaterial = litMat;
                dirty = true;
                switched++;
            }
            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
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
                sr.sharedMaterial = litMat;
                dirty = true;
                switched++;
            }
            if (dirty) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.Refresh();
        Debug.Log($"[SwitchSpriteToLit] ✅ 切换完成：{switched} 个 → Sprite-Lit, {skipped} 个 Glow 保持 Unlit");
    }

    /// <summary>判断是否是光晕 Sprite（父对象名含"Glow"或自身名含"Glow"）</summary>
    static bool IsGlow(SpriteRenderer sr)
    {
        return sr.name.Contains("Glow") ||
               (sr.transform.parent != null && sr.transform.parent.name.Contains("Glow"));
    }
}
