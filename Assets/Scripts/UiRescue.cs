// InventoryUIRescue.cs — v2.2
// Non-intrusive setup: ensures EventSystem + a Canvas exist, without opening the panel or changing visuals.

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryUIRescue : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("The UI panel we care about. If empty, uses this GameObject.")]
    public GameObject panelRoot;

    [Header("Safety")]
    [Tooltip("Ensure an EventSystem exists (required for UI).")]
    public bool ensureEventSystem = true;

    [Tooltip("Ensure there is a Canvas somewhere up the hierarchy. If none, create ONE on panelRoot.")]
    public bool ensureCanvas = true;

    [Tooltip("If we create a Canvas (because none existed), use ScreenSpaceOverlay + high sorting for reliability.")]
    public bool overlayForNewCanvas = true;

    [Tooltip("If we create a Canvas, also add a GraphicRaycaster.")]
    public bool addRaycasterForNewCanvas = true;

    [Header("State Discipline")]
    [Tooltip("We will NOT change panelRoot.activeSelf. We preserve whatever it was before this script runs.")]
    public bool preservePanelActiveSelf = true;

    [Header("Debug (optional)")]
    [Tooltip("Add a TEMP background (one frame) to prove draw order, then remove it next frame.")]
    public bool addTempBackgroundOnce = false;

    [Tooltip("Print logs.")]
    public bool verbose = false;

    [Tooltip("Destroy this component after one frame (recommended).")]
    public bool selfDestructAfterSetup = true;

    const string TEMP_BG_NAME = "__TEMP_BG__";

    void Awake()
    {
        if (!panelRoot) panelRoot = gameObject;

        var originalActive = panelRoot.activeSelf;

        // 1) EventSystem
        if (ensureEventSystem && FindFirstObjectByType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            if (verbose) Debug.Log("[InventoryUIRescue] Created EventSystem.");
        }

        // 2) Canvas
        Canvas canvas = GetCanvasInParents(panelRoot.transform);
        if (ensureCanvas && canvas == null)
        {
            canvas = panelRoot.GetComponent<Canvas>();
            if (canvas == null) canvas = panelRoot.AddComponent<Canvas>();

            if (overlayForNewCanvas)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000;
                canvas.enabled = true;
            }

            if (addRaycasterForNewCanvas && panelRoot.GetComponent<GraphicRaycaster>() == null)
                panelRoot.AddComponent<GraphicRaycaster>();

            if (verbose) Debug.Log("[InventoryUIRescue] Added a new Canvas on panelRoot (overlay for new only).");
        }

        if (addTempBackgroundOnce)
        {
            AddTempBG(panelRoot.transform);
        }

        if (preservePanelActiveSelf && panelRoot.activeSelf != originalActive)
            panelRoot.SetActive(originalActive);

        StartCoroutine(PostFrameCleanup());
    }

    IEnumerator PostFrameCleanup()
    {
        yield return null;

        if (addTempBackgroundOnce)
        {
            var t = panelRoot.transform.Find(TEMP_BG_NAME);
            if (t) Destroy(t.gameObject);
            if (verbose) Debug.Log("[InventoryUIRescue] Removed temporary background.");
        }

        if (selfDestructAfterSetup)
        {
            if (verbose) Debug.Log("[InventoryUIRescue] Self-destructing (setup complete).");
            Destroy(this);
        }
    }

    static Canvas GetCanvasInParents(Transform from)
    {
        var t = from;
        while (t != null)
        {
            var c = t.GetComponent<Canvas>();
            if (c != null) return c;
            t = t.parent;
        }
        return null;
    }

    void AddTempBG(Transform parent)
    {
        if (parent.Find(TEMP_BG_NAME) != null) return;
        var bg = new GameObject(TEMP_BG_NAME, typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);
        var rt = bg.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0, 0, 0, 0.35f);
    }
}
