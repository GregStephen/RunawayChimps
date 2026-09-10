using UnityEngine;

public class ScreenVignette : MonoBehaviour
{
    public UnityEngine.UI.Image overlay;

    public void SetGlobalProximity(float proximity)
    {
        if (overlay == null) return;
        var color = overlay.color;
        color.a = Mathf.Lerp(0f, 0.6f, proximity);
        overlay.color = color;
    }
}