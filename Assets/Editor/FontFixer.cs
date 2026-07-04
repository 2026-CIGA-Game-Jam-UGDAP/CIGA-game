using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// SmileySans 字体一键修复工具。
/// 策略：删掉坏资产 → 用 TMP API 创建新资产（动态 atlas）→ 存盘 → 修复 prefab 引用。
/// 中文 CJK 字符由动态 atlas 在运行时自动填充，不在编辑器预烘焙。
/// </summary>
public static class FontFixer
{
    const string OTF_PATH = "Assets/Font/SmileySans-Oblique.otf";
    const string SDF_ASSET_PATH = "Assets/Font/SmileySans-Oblique SDF.asset";
    const string FALLBACK_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";
    const string DIALOG_PANEL_PATH = "Assets/Prefabs/UI/dialogPanel.prefab";

    [MenuItem("Tools/一键修复SmileySans字体")]
    public static void RegenerateSmileySansFont()
    {
        // ====== 1. 加载源字体 ======
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(OTF_PATH);
        if (sourceFont == null)
        {
            Debug.LogError($"[FontFixer] ❌ 找不到源字体: {OTF_PATH}");
            return;
        }

        var fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FALLBACK_PATH);

        // ====== 2. 临时把 dialogPanel 切到 LiberationSans，避免引用丢失 ======
        if (fallbackFont != null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DIALOG_PANEL_PATH);
            if (prefab != null)
            {
                foreach (var tmp in prefab.GetComponentsInChildren<TMP_Text>(true))
                    tmp.font = fallbackFont;
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }

        // ====== 3. 删除旧的 SDF 资产 ======
        string sdfDir = Path.GetDirectoryName(SDF_ASSET_PATH);
        string sdfName = Path.GetFileNameWithoutExtension(SDF_ASSET_PATH);
        string oldMatPath = Path.Combine(sdfDir, sdfName + " Material.asset").Replace("\\", "/");

        if (File.Exists(oldMatPath)) AssetDatabase.DeleteAsset(oldMatPath);
        if (File.Exists(SDF_ASSET_PATH)) AssetDatabase.DeleteAsset(SDF_ASSET_PATH);
        AssetDatabase.Refresh();

        // ====== 4. 创建新字体资产（纯内存操作，不 TryAddCharacters） ======
        EditorUtility.DisplayProgressBar("SmileySans 修复", "创建 SDF 资产...", 0.4f);

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            36,
            8,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            1024,
            1024,
            TMPro.AtlasPopulationMode.Dynamic
        );

        if (fontAsset == null)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("[FontFixer] ❌ CreateFontAsset 返回 null！请用 Window → TextMeshPro → Font Asset Creator 手动生成。");
            return;
        }

        fontAsset.name = sdfName;

        // ====== 5. 设置 fallback ======
        if (fallbackFont != null)
        {
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallbackFont };
        }

        // ====== 6. 存盘 ======
        EditorUtility.DisplayProgressBar("SmileySans 修复", "保存资产...", 0.7f);
        AssetDatabase.CreateAsset(fontAsset, SDF_ASSET_PATH);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ====== 7. 修复 dialogPanel 引用 ======
        EditorUtility.DisplayProgressBar("SmileySans 修复", "修复 prefab 引用...", 0.9f);

        var newFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SDF_ASSET_PATH);
        if (newFont != null)
        {
            // 重建 fallback（Refresh 后可能丢失）
            if (fallbackFont != null)
            {
                newFont.fallbackFontAssetTable = new List<TMP_FontAsset> { fallbackFont };
                EditorUtility.SetDirty(newFont);
                AssetDatabase.SaveAssets();
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DIALOG_PANEL_PATH);
            if (prefab != null)
            {
                foreach (var tmp in prefab.GetComponentsInChildren<TMP_Text>(true))
                    tmp.font = newFont;
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }

        EditorUtility.ClearProgressBar();

        Debug.Log($"[FontFixer] ✅ SmileySans 字体修复完成！\n" +
                  $"  • 源: {OTF_PATH}\n" +
                  $"  • 目标: {SDF_ASSET_PATH}\n" +
                  $"  • 模式: 动态 atlas 1024×1024 SDFAA\n" +
                  $"  • CJK 字符将在运行时自动填充\n" +
                  $"  • 后备: {(fallbackFont != null ? "LiberationSans" : "未设置")}\n" +
                  $"  • Prefab: dialogPanel → SmileySans");
    }
}
