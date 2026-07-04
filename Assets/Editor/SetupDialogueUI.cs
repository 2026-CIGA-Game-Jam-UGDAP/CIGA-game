using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 一键在选中的 UI 父节点下生成 BigPortrait/LeftGroup/RightGroup，
/// 并自动连线到场景中的 DialogueManager。
/// 用法：在 Hierarchy 选中 DialoguePanel，然后 Tools → 生成对话 UI 子节点。
/// </summary>
public static class SetupDialogueUI
{
    [MenuItem("Tools/生成对话 UI 子节点")]
    public static void Setup()
    {
        GameObject target = Selection.activeGameObject;
        if (target == null)
        {
            Debug.LogError("请先在 Hierarchy 选中 DialoguePanel");
            return;
        }

        DialogueManager dm = Object.FindObjectOfType<DialogueManager>();
        if (dm == null)
        {
            Debug.LogError("场景中没有 DialogueManager！请先创建 DialogueSystem 并挂 DialogueManager。");
            return;
        }

        Undo.SetCurrentGroupName("生成对话 UI 子节点");
        int undoGroup = Undo.GetCurrentGroup();

        // ====== BigPortrait ======
        GameObject bigGo = MakeImage(target, "BigPortrait", out Image bigImg);
        bigImg.preserveAspect = true;
        bigImg.raycastTarget = false;
        bigGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 180);
        Undo.RecordObject(dm, "连线 BigPortrait");
        dm.bigPortrait = bigImg;
        dm.bigPortraitGo = bigGo;

        // ====== LeftGroup ======
        GameObject leftGroup = MakeImage(target, "LeftGroup", out Image _);
        DestroyImmediate(leftGroup.GetComponent<Image>()); // 纯容器不需要 Image
        Undo.RecordObject(dm, "连线 LeftGroup");
        dm.leftGroup = leftGroup;

        GameObject leftAvatar = MakeImage(leftGroup, "Avatar", out Image leftImg);
        leftImg.preserveAspect = true;
        leftImg.raycastTarget = false;
        Undo.RecordObject(dm, "连线 LeftPortrait");
        dm.leftPortrait = leftImg;

        GameObject leftLabel = MakeTMP(leftGroup, "NameLabel", out TMP_Text leftTmp);
        leftTmp.text = "方波";
        leftTmp.fontSize = 20;
        leftTmp.alignment = TextAlignmentOptions.Center;
        leftTmp.raycastTarget = false;

        // ====== RightGroup ======
        GameObject rightGroup = MakeImage(target, "RightGroup", out Image _);
        DestroyImmediate(rightGroup.GetComponent<Image>());
        Undo.RecordObject(dm, "连线 RightGroup");
        dm.rightGroup = rightGroup;

        GameObject rightAvatar = MakeImage(rightGroup, "Avatar", out Image rightImg);
        rightImg.preserveAspect = true;
        rightImg.raycastTarget = false;
        Undo.RecordObject(dm, "连线 RightPortrait");
        dm.rightPortrait = rightImg;

        GameObject rightLabel = MakeTMP(rightGroup, "NameLabel", out TMP_Text rightTmp);
        rightTmp.text = "拉兹";
        rightTmp.fontSize = 20;
        rightTmp.alignment = TextAlignmentOptions.Center;
        rightTmp.raycastTarget = false;

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.SetDirty(dm);
        EditorUtility.SetDirty(target);

        Debug.Log("✅ 对话 UI 子节点已生成！\n"
            + "  BigPortrait / LeftGroup(Avatar+NameLabel) / RightGroup(Avatar+NameLabel)\n"
            + "  已自动连线到 DialogueManager。位置请自行调整。");
    }

    static GameObject MakeImage(GameObject parent, string name, out Image img)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "创建 " + name);
        go.transform.SetParent(parent.transform, false);
        img = go.GetComponent<Image>();
        img.color = Color.white;
        return go;
    }

    static GameObject MakeTMP(GameObject parent, string name, out TMP_Text tmp)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TMP_Text));
        Undo.RegisterCreatedObjectUndo(go, "创建 " + name);
        go.transform.SetParent(parent.transform, false);
        tmp = go.GetComponent<TMP_Text>();
        tmp.color = Color.white;
        return go;
    }

    [MenuItem("Tools/生成对话 UI 子节点", true)]
    public static bool SetupValidate()
    {
        return Selection.activeGameObject != null;
    }
}
