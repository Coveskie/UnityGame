using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryPanelStyler : MonoBehaviour
{
    [Header("Layout")]
    public RectTransform grid;                         // assign your Grid
    public Vector2 panelSize = new Vector2(800, 600);
    public Vector4 gridPadding = new Vector4(24, 24, 24, 80); // L,R,B,T (T=title space)

    [Header("Colors")]
    public Color panelBg = new Color32(48, 48, 48, 204);  // #303030 @ 80%
    public Color slotEmpty = new Color32(110, 110, 110, 40);
    public Color slotFilled = new Color32(74, 74, 74, 255);
    public Color countColor = new Color32(220, 220, 220, 255);

    void OnEnable() { Apply(); }
    void OnValidate() { if (enabled) Apply(); }

    void Apply()
    {
        // Panel center & bg
        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = panelSize;
        rt.localScale = Vector3.one;

        var img = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        img.color = panelBg;

        // Grid stretch & padding
        if (grid)
        {
            grid.anchorMin = Vector2.zero;
            grid.anchorMax = Vector2.one;
            // offsets: left, right, bottom, top
            grid.offsetMin = new Vector2(gridPadding.x, gridPadding.z);
            grid.offsetMax = new Vector2(-gridPadding.y, -gridPadding.w);
        }

        // Theme all SlotViews to grey
        if (grid)
        {
            foreach (var sv in grid.GetComponentsInChildren<SlotView>(true))
            {
                sv.emptyBg = slotEmpty;
                sv.filledBg = slotFilled;

                // set count color if present
                var label = sv.transform.Find("Count")?.GetComponent<TMP_Text>();
                if (label) label.color = countColor;

                // force a visual update right away
                sv.SetEmpty();
            }
        }
    }
}
