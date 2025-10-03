using UnityEngine;
using UnityEngine.UI;

public class RectImageExample : MonoBehaviour
{
    public Sprite mySprite;   // assign in Inspector
    public Transform parentUI; // e.g. your Grid or Panel

    void Start()
    {
        // 1. make new GameObject with required UI components
        GameObject go = new GameObject("MyImage",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        // 2. parent it under a UI object (important: keep worldPositionStays = false!)
        go.transform.SetParent(parentUI, false);

        // 3. configure RectTransform
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(128, 128);       // width & height
        rt.anchoredPosition = Vector2.zero;         // center in parent
        rt.anchorMin = new Vector2(0.5f, 0.5f);     // middle anchors
        rt.anchorMax = new Vector2(0.5f, 0.5f);

        // 4. assign sprite to Image
        Image img = go.GetComponent<Image>();
        img.sprite = mySprite;
        img.preserveAspect = true;
        img.color = Color.white; // tint (can set alpha here too)
    }
}
