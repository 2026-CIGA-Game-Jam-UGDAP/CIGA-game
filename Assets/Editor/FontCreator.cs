using UnityEngine;
using UnityEditor;
using TMPro;
using TMPro.EditorUtilities;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// SmileySans 字体生成工具。
/// 使用 Unity 内置的 TMPro_FontAssetCreatorWindow（稳定可靠）。
/// </summary>
public static class FontCreator
{
    const string OTF_PATH = "Assets/Font/SmileySans-Oblique.otf";
    const string DIALOG_PANEL_PATH = "Assets/Prefabs/UI/dialogPanel.prefab";
    const string FALLBACK_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";
    const string SDF_PATH = "Assets/Font/SmileySans-Oblique SDF.asset";

    // ★ 全量游戏对话字符（已复制到剪贴板）
    const string GAME_CHARS =
        "方波拉兹这是里被砸开的人类空间站我们的飞船不是彻底陨星撞碎成渣了嗯身边只剩条破绳子和你把那个铁疙瘩捡起来看看吸附在表面上试试移动得配合作行动燃料不多了去太空站外围找找有没有备用推进器够外面有能量泄漏补充一下不用要一小会就快满了看那边好像艘废弃的它还能飞吗能推动不过需要全部零件收集齐才启动反应堆好搜刮周边区域太棒了现在俩人可以同时对接启动引擎回家完美胜利尽管历经艰险最幸运的是有你梦想我们活下去了但星光照亮前路冒险还远未结束" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
        "，。！？…—、：；\"\"''（）《》";

    [MenuItem("Tools/准备生成SmileySans")]
    public static void PrepareFontGeneration()
    {
        // 1. 清理可能损坏的旧资产
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FALLBACK_PATH);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DIALOG_PANEL_PATH);

        if (prefab != null && fallback != null)
        {
            foreach (var t in prefab.GetComponentsInChildren<TMP_Text>(true))
                t.font = fallback;
            PrefabUtility.SavePrefabAsset(prefab);
        }

        string sdfDir = Path.GetDirectoryName(SDF_PATH);
        string sdfName = Path.GetFileNameWithoutExtension(SDF_PATH);
        string oldMat = Path.Combine(sdfDir, sdfName + " Material.asset").Replace("\\", "/");
        if (File.Exists(oldMat)) AssetDatabase.DeleteAsset(oldMat);
        if (File.Exists(SDF_PATH)) AssetDatabase.DeleteAsset(SDF_PATH);
        AssetDatabase.Refresh();

        // 2. 把字符复制到剪贴板
        GUIUtility.systemCopyBuffer = GAME_CHARS;

        // 3. 打开 Font Asset Creator 窗口（预加载 SmileySans）
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(OTF_PATH);
        if (sourceFont == null)
        {
            Debug.LogError($"找不到源字体: {OTF_PATH}");
            return;
        }

        TMPro_FontAssetCreatorWindow.ShowFontAtlasCreatorWindow(sourceFont);

        Debug.Log(
            "[FontCreator] ✅ 准备工作完成！\n" +
            "  • 旧资产已清理\n" +
            "  • 字符列表已复制到剪贴板\n" +
            "  • Font Asset Creator 已打开（SmileySans 已加载）\n\n" +
            "=== 请在窗口中操作 ===\n" +
            "1. Atlas Resolution → 2048 × 2048\n" +
            "2. Character Set → Custom Characters\n" +
            "3. 在 Custom Character List 中粘贴 (Ctrl+V)\n" +
            "4. 点 Generate Font Atlas\n" +
            "5. 等渲染完点 Save → 存为 SmileySans-Oblique SDF.asset"
        );
    }

    [MenuItem("Tools/修复DialogPanel→SmileySans")]
    public static void FixDialogPanelRef()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SDF_PATH);
        if (font == null)
        {
            Debug.LogError("[FontCreator] ❌ SmileySans SDF 不存在，请先生成字体！");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DIALOG_PANEL_PATH);
        if (prefab == null)
        {
            Debug.LogError("[FontCreator] ❌ dialogPanel.prefab 不存在");
            return;
        }

        int count = 0;
        foreach (var t in prefab.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.font != font) { t.font = font; count++; }
        }
        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.Refresh();

        Debug.Log($"[FontCreator] ✅ dialogPanel.prefab → SmileySans ({count} 处修复)");
    }
}
