using UnityEngine;

[DisallowMultipleComponent]
public class CursorSkin : MonoBehaviour
{
    [SerializeField] Texture2D normalCursor;
    [SerializeField] Vector2 normalHotspot;
    [SerializeField] Texture2D pressedCursor;
    [SerializeField] Vector2 pressedHotspot;

    bool wasPressed;

    void Start()
    {
        if (normalCursor != null)
            Cursor.SetCursor(normalCursor, normalHotspot, CursorMode.Auto);
    }

    void Update()
    {
        SetPressed(Application.isFocused && Input.GetMouseButton(0));
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            SetPressed(false);
    }

    void OnDisable()
    {
        SetPressed(false);
    }

    void SetPressed(bool pressed)
    {
        if (wasPressed == pressed || pressedCursor == null || normalCursor == null) return;

        wasPressed = pressed;
        Cursor.SetCursor(pressed ? pressedCursor : normalCursor,
            pressed ? pressedHotspot : normalHotspot, CursorMode.Auto);
    }
}
