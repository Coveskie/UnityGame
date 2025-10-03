// Assets/Game/Inventory/UI/SlotView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class SlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Refs (optional; auto-wired)")]
    [SerializeField] Image background;
    [SerializeField] Image icon;
    [SerializeField] TMP_Text countText;
    [SerializeField] Button button;

    [Header("Style")]
    public Color emptyBg = new Color(1f, 1f, 1f, 0.12f); // faint frame
    public Color filledBg = Color.white;
    public bool showCountForSingles = false;

    // Callbacks (InventoryUI hooks these)
    public int slotIndex = -1;
    public System.Action<int> onClick;
    public System.Action<int, Vector2> onHoverEnter;
    public System.Action onHoverExit;

    // ---------- Editor-time: only FIND refs, don't ADD components ----------
    void Reset() { FindRefsOnly(); }
    void OnValidate() { FindRefsOnly(); }

    // ---------- Runtime: create anything missing safely ----------
    void Awake()
    {
        // keep transforms sane
        var rt = (RectTransform)transform;
        if (rt.sizeDelta == Vector2.zero) rt.sizeDelta = new Vector2(96, 96);
        rt.localScale = Vector3.one;
    }

    void Start()
    {
        EnsureRuntimeRefs();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => { if (slotIndex >= 0) onClick?.Invoke(slotIndex); });

        // default to empty visual until InventoryUI sets data
        SetEmpty();
    }

    // ---------- Public API ----------
    public void SetEmpty()
    {
        if (icon) { icon.sprite = null; icon.enabled = false; }
        if (countText) countText.text = "";
        if (background) { background.enabled = true; background.color = emptyBg; }
    }

    public void Set(Sprite sprite, int count, bool isStackable)
    {
        if (icon)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (countText)
        {
            bool show = isStackable ? count > 1 : (showCountForSingles && count > 0);
            countText.text = show ? count.ToString() : "";
        }

        if (background) { background.enabled = true; background.color = filledBg; }
    }

    // ---------- Hover (tooltip) ----------
    public void OnPointerEnter(PointerEventData e)
    {
        if (slotIndex >= 0) onHoverEnter?.Invoke(slotIndex, e.position);
    }

    public void OnPointerExit(PointerEventData e)
    {
        onHoverExit?.Invoke();
    }

    // ---------- Helpers ----------
    void FindRefsOnly()
    {
        if (!background) TryGetComponent(out background);
        if (!button) TryGetComponent(out button);

        if (!icon)
        {
            var t = transform.Find("Icon");
            if (t) icon = t.GetComponent<Image>();
        }
        if (!countText)
        {
            var t = transform.Find("Count");
            if (t) countText = t.GetComponent<TMP_Text>();
        }
    }

    void EnsureRuntimeRefs()
    {
        // Button
        if (!button) button = gameObject.GetComponent<Button>();
        if (!button) button = gameObject.AddComponent<Button>();

        // Background (and targetGraphic)
        if (!background) background = gameObject.GetComponent<Image>();
        if (!background) background = gameObject.AddComponent<Image>();
        if (button && button.targetGraphic == null) button.targetGraphic = background;

        // Icon
        if (!icon)
        {
            var t = transform.Find("Icon");
            if (t) icon = t.GetComponent<Image>();
        }
        if (!icon)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8, 8);
            rt.offsetMax = new Vector2(-8, -8);
            icon = go.GetComponent<Image>();
            icon.preserveAspect = true;
        }

        // Count
        if (!countText)
        {
            var t = transform.Find("Count");
            if (t) countText = t.GetComponent<TMP_Text>();
        }
        if (!countText)
        {
            var go = new GameObject("Count", typeof(RectTransform), typeof(TMP_Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-6, 6);
            countText = go.GetComponent<TMP_Text>();
            countText.fontSize = 22;
            countText.alignment = TextAlignmentOptions.BottomRight;
            countText.text = "";
        }
    }
}
