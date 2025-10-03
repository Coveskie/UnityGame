// Assets/Game/Inventory/Scripts/InventoryUI.cs
// InventoryUI — v1.0.6 (layout-safe + CG enforcement + diagnostics)
// - Keeps your layout/anchors unchanged by default.
// - Fixes your exact issue: a CanvasGroup on the panel had alpha=0 (invisible).
// - On open/close, we now enforce CanvasGroup state on the panel (and, if you want, on parents).
// - Primary singleton + OnInventoryToggled + Diagnose Visibility (Now).

using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Game/Inventory/InventoryUI")]
public class InventoryUI : MonoBehaviour
{
    [Header("UI Root (Panel to show/hide)")]
    public GameObject panelRoot;

    [Header("Optional: Big slot icon (the 'held' display)")]
    public Image bigIcon;

    [Header("Diagnostics")]
    public bool debugLogs = true;

    [Tooltip("Run a diagnostic log on every Open to explain visibility without changing layout.")]
    [SerializeField] bool autoDiagnoseOnOpen = false;

    [Header("Safety (no layout changes)")]
    [Tooltip("Ensure a Canvas exists when first opened (no anchor/size changes).")]
    [SerializeField] bool ensureCanvasOnFirstOpen = true;

    [Tooltip("If >0, temporarily boost Canvas sorting order on first open; 0 leaves as-is.")]
    [SerializeField] int sortingOrderBoostOnFirstOpen = 0;

    [Header("Optional rescue (changes layout)")]
    [Tooltip("If enabled, FIRST OPEN will stretch Rect full-screen and raise sorting order to guarantee visibility (use only to prove rendering, then turn off).")]
    [SerializeField] bool autoRectFixOnFirstOpen = false; // keep OFF to preserve your layout

    [Header("CanvasGroup handling")]
    [Tooltip("Ignore CanvasGroup and just SetActive(). Usually leave OFF.")]
    [SerializeField] bool bypassCanvasGroup = false;

    [Tooltip("When opening/closing, enforce CanvasGroup state on the panel itself.")]
    [SerializeField] bool enforcePanelCanvasGroup = true;

    [Tooltip("Also enforce CanvasGroup state on parent chain (useful if a parent CG hides the panel).")]
    [SerializeField] bool enforceParentCanvasGroups = false;

    // ───────── Legacy compatibility ─────────
    public static InventoryUI Primary { get; private set; }
    public static void SetPrimary(InventoryUI ui) => Primary = ui;
    public event Action<bool> OnInventoryToggled;

    // ───────── Internal ─────────
    bool _isOpen;
    bool _safetyAppliedOnce = false;
    Canvas _canvas;
    GraphicRaycaster _raycaster;
    RectTransform _panelRT;

    void Reset()
    {
        if (!panelRoot) panelRoot = gameObject;
    }

    void Awake()
    {
        if (!panelRoot)
        {
            Debug.LogError("[InventoryUI] panelRoot is not assigned.", this);
            return;
        }

        if (Primary == null) Primary = this;

        _panelRT = panelRoot.GetComponent<RectTransform>();

        // Ensure Canvas exists (no layout changes here)
        _canvas = panelRoot.GetComponentInParent<Canvas>();
        if (!_canvas)
        {
            _canvas = panelRoot.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            panelRoot.AddComponent<CanvasScaler>();
            if (debugLogs) Debug.Log("[InventoryUI] No Canvas found; added ScreenSpaceOverlay Canvas.", panelRoot);
        }

        _raycaster = _canvas.gameObject.GetComponent<GraphicRaycaster>();
        if (!_raycaster) _raycaster = _canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (!FindAnyObjectByType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        SetOpen(false, immediate: true);
    }

    void OnDestroy()
    {
        if (Primary == this) Primary = null;
    }

    public bool IsOpen => _isOpen;

    public void Toggle() => SetOpen(!_isOpen);
    public void Open() => SetOpen(true);
    public void Close() => SetOpen(false);

    public void SetOpen(bool open, bool immediate = false)
    {
        if (!panelRoot) return;

        bool changed = (_isOpen != open);
        _isOpen = open;

        if (bypassCanvasGroup)
        {
            panelRoot.SetActive(open);
        }
        else
        {
            // Respect CG if present, but we’ll ENFORCE it below to avoid alpha=0 invisibility.
            var cg = panelRoot.GetComponent<CanvasGroup>();
            if (cg)
            {
                // Set desired state
                cg.interactable = open;
                cg.blocksRaycasts = open;
                cg.alpha = open ? 1f : 0f;
                panelRoot.SetActive(true); // keep active with CG
            }
            else
            {
                panelRoot.SetActive(open);
            }

            // Enforce CG on the panel and optionally parents (fixes your current issue)
            if (enforcePanelCanvasGroup || enforceParentCanvasGroups)
                EnforceCanvasGroupState(open, enforcePanelCanvasGroup, enforceParentCanvasGroups);
        }

        if (debugLogs)
            Debug.Log($"[InventoryUI] {(open ? "OPEN" : "CLOSE")} '{panelRoot.name}' (cg? {(!bypassCanvasGroup && panelRoot.GetComponent<CanvasGroup>() != null)})", panelRoot);

        if (open)
        {
            if (!_safetyAppliedOnce)
            {
                _safetyAppliedOnce = true;
                ApplyOneTimeCanvasSafety();

                if (autoRectFixOnFirstOpen)
                    ForceVisibleRectStretch(); // last-resort: layout-changing rescue
            }

            if (autoDiagnoseOnOpen)
                DiagnoseVisibilityInternal("AutoDiagnoseOnOpen");
        }

        if (changed)
            OnInventoryToggled?.Invoke(_isOpen);
    }

    public void SetHeldIcon(Sprite icon, bool preserveNativeSize = true)
    {
        if (!bigIcon) return;

        if (!icon)
        {
            bigIcon.sprite = null;
            bigIcon.enabled = false;
            return;
        }

        bigIcon.enabled = true;
        bigIcon.sprite = icon;
        if (preserveNativeSize) bigIcon.SetNativeSize();
    }

    public void ClearHeldIcon() => SetHeldIcon(null);

    // ───────────────────────── Helpers ─────────────────────────

    void ApplyOneTimeCanvasSafety()
    {
        _canvas = panelRoot.GetComponentInParent<Canvas>();
        if (!_canvas) _canvas = panelRoot.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        if (sortingOrderBoostOnFirstOpen != 0)
            _canvas.sortingOrder += sortingOrderBoostOnFirstOpen;

        if (!_canvas.TryGetComponent(out GraphicRaycaster _))
            _canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (!panelRoot.activeSelf) panelRoot.SetActive(true);
    }

    // Only called if autoRectFixOnFirstOpen is ON (you can keep this OFF to preserve layout)
    void ForceVisibleRectStretch()
    {
        var rt = panelRoot.GetComponent<RectTransform>();
        if (rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        // Also raise sorting order high to ensure on top
        _canvas = panelRoot.GetComponentInParent<Canvas>();
        if (_canvas) _canvas.sortingOrder = Mathf.Max(_canvas.sortingOrder, 5000);

        if (debugLogs) Debug.Log("[InventoryUI] ForceVisibleRectStretch applied (layout changed for visibility).", panelRoot);
    }

    /// <summary>
    /// Enforce CanvasGroup state on the panel and optionally on parents.
    /// Fixes cases where some other script/anim keeps CG alpha at 0.
    /// </summary>
    void EnforceCanvasGroupState(bool open, bool enforceSelf, bool enforceParents)
    {
        int count = 0;

        if (enforceSelf)
        {
            var selfCg = panelRoot.GetComponent<CanvasGroup>();
            if (selfCg)
            {
                selfCg.alpha = open ? 1f : 0f;
                selfCg.blocksRaycasts = open;
                selfCg.interactable = open;
                count++;
            }
        }

        if (enforceParents)
        {
            var parent = panelRoot.transform.parent;
            while (parent != null)
            {
                var cg = parent.GetComponent<CanvasGroup>();
                if (cg)
                {
                    cg.alpha = open ? 1f : cg.alpha; // don’t force parents invisible on close; leave them as-is
                    cg.blocksRaycasts = open || cg.blocksRaycasts;
                    cg.interactable = open || cg.interactable;
                    count++;
                }
                parent = parent.parent;
            }
        }

        if (debugLogs && count > 0)
            Debug.Log($"[InventoryUI] Enforced CanvasGroup state on {(enforceParents ? "panel+parents" : "panel")} (count={count}).", panelRoot);
    }

    // ───────────────────────── Diagnostics ─────────────────────────

    [ContextMenu("Diagnose Visibility (Now)")]
    public void DiagnoseVisibilityNow()
    {
        DiagnoseVisibilityInternal("ContextMenu");
    }

    void DiagnoseVisibilityInternal(string tag)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[InventoryUI/Diag:{tag}] Panel='{panelRoot?.name}'");

        // Active flags
        sb.AppendLine($" - panelRoot.activeInHierarchy={panelRoot.activeInHierarchy}");

        // Canvas chain & render modes
        var can = panelRoot.GetComponentInParent<Canvas>(includeInactive: true);
        if (can)
        {
            sb.AppendLine($" - Canvas: enabled={can.enabled}, renderMode={can.renderMode}, sortingOrder={can.sortingOrder}, planeDist={(can.renderMode == RenderMode.ScreenSpaceCamera ? can.planeDistance : 0)}");
            if (can.renderMode == RenderMode.ScreenSpaceCamera && can.worldCamera == null)
                sb.AppendLine("   ! ScreenSpace-Camera has NO camera assigned (invisible).");
        }
        else
        {
            sb.AppendLine(" - Canvas: NONE (would be invisible unless auto-created).");
        }

        // RectTransform stats
        var rt = panelRoot.GetComponent<RectTransform>();
        if (rt)
        {
            sb.AppendLine($" - RectTransform: anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} pivot={rt.pivot}");
            sb.AppendLine($"                  anchoredPos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} localScale={rt.localScale}");
            if (rt.localScale == Vector3.zero) sb.AppendLine("   ! localScale is ZERO.");
        }
        else
        {
            sb.AppendLine(" - RectTransform: NONE (not a UI object?)");
        }

        // CanvasGroup chain up to root
        float minAlpha = 1f;
        bool anyBlocksRaycastsFalse = false;
        Transform t = panelRoot.transform;
        int cgCount = 0;
        while (t != null)
        {
            var cg = t.GetComponent<CanvasGroup>();
            if (cg)
            {
                cgCount++;
                sb.AppendLine($" - CanvasGroup('{t.name}'): alpha={cg.alpha}, blocksRaycasts={cg.blocksRaycasts}, interactable={cg.interactable}");
                minAlpha = Mathf.Min(minAlpha, cg.alpha);
                if (!cg.blocksRaycasts) anyBlocksRaycastsFalse = true;
            }
            t = t.parent;
        }
        if (cgCount == 0) sb.AppendLine(" - CanvasGroups: none on parent chain.");
        if (minAlpha <= 0.001f) sb.AppendLine("   ! A parent CanvasGroup alpha is 0 -> fully invisible.");
        if (anyBlocksRaycastsFalse) sb.AppendLine("   ! A parent CanvasGroup has blocksRaycasts=false (may prevent interaction).");

        // Visible graphics present?
        bool hasGraphics = PanelHasAnyEnabledGraphic();
        sb.AppendLine($" - Enabled graphics under panel: {hasGraphics}");
        if (!hasGraphics) sb.AppendLine("   ! No enabled Image/Text/RawImage/TMP under panel -> appears blank.");

#if UNITY_EDITOR
        Debug.Log(sb.ToString(), panelRoot);
#else
        Debug.Log(sb.ToString());
#endif
    }

    bool PanelHasAnyEnabledGraphic()
    {
        if (!panelRoot.activeInHierarchy) return false;

        var imgs = panelRoot.GetComponentsInChildren<Image>(includeInactive: false);
        foreach (var i in imgs) if (i.enabled && i.color.a > 0.01f) return true;

        var raws = panelRoot.GetComponentsInChildren<RawImage>(includeInactive: false);
        foreach (var r in raws) if (r.enabled && r.color.a > 0.01f) return true;

        var txts = panelRoot.GetComponentsInChildren<Text>(includeInactive: false);
        foreach (var t in txts) if (t.enabled && t.color.a > 0.01f) return true;

#if TMP_PRESENT
        var tmps = panelRoot.GetComponentsInChildren<TMPro.TMP_Text>(includeInactive: false);
        foreach (var tm in tmps) if (tm.enabled && tm.color.a > 0.01f) return true;
#endif
        return false;
    }
}
