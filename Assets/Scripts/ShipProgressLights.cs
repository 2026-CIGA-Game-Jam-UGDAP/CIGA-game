using UnityEngine;

/// <summary>显示船身通关进度灯和当前关的零件收集表情。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class ShipProgressLights : MonoBehaviour
{
    [Tooltip("初始飞船 Sprite，用于保持开门动画各帧的灯位对齐")]
    [SerializeField] Sprite referenceSprite;
    [SerializeField] Transform lightsRoot;
    [Tooltip("从左到右排列的三个灯，包含各自的柔光子物体")]
    [SerializeField] GameObject[] lights;
    [SerializeField] SpriteRenderer partFace;
    [SerializeField] Sprite completedFace;

    SpriteRenderer shipRenderer;
    Sprite incompleteFace;
    Sprite lastSprite;

    void Awake()
    {
        shipRenderer = GetComponent<SpriteRenderer>();
        if (partFace != null)
            incompleteFace = partFace.sprite;

        // 线性关卡按当前关序恢复；重开本关不累加，重新进入教学关自然清零。
        SetCompletedLevels(GetLevelNumber() - 1);
    }

    /// <summary>只在正式关卡完成、开始播放飞船动画时调用。</summary>
    public void CompleteLevel()
    {
        SetCompletedLevels(GetLevelNumber());
    }

    public void SetPartsCollected(bool complete)
    {
        if (partFace == null) return;
        partFace.enabled = true;
        partFace.sprite = complete && completedFace != null ? completedFace : incompleteFace;
    }

    void SetCompletedLevels(int completedLevels)
    {
        if (lights == null) return;

        completedLevels = Mathf.Clamp(completedLevels, 0, lights.Length);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].SetActive(i < completedLevels);
        }
    }

    int GetLevelNumber()
    {
        switch (gameObject.scene.name)
        {
            case "Level1": return 1;
            case "Level2": return 2;
            case "Level3": return 3;
            default: return 0; // 教学关和未启用的关卡不计入三盏进度灯。
        }
    }

    void LateUpdate()
    {
        Sprite currentSprite = shipRenderer.sprite;
        if (currentSprite == lastSprite || currentSprite == null || referenceSprite == null || lightsRoot == null)
            return;

        lastSprite = currentSprite;

        // 三张原画的圆点坐标一致，但开门末帧的裁切框更高，中心轴点随之变化。
        Vector2 referenceOrigin = (referenceSprite.rect.position + referenceSprite.pivot) / referenceSprite.pixelsPerUnit;
        Vector2 currentOrigin = (currentSprite.rect.position + currentSprite.pivot) / currentSprite.pixelsPerUnit;
        Vector2 offset = referenceOrigin - currentOrigin;
        lightsRoot.localPosition = new Vector3(offset.x, offset.y, 0f);
    }
}
