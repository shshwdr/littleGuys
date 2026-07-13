using UnityEngine;
using UnityEngine.EventSystems;

public class UpgradeTreePanZoom : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    RectTransform viewport;
    RectTransform panTarget;
    Canvas canvas;
    float minScale = 0.4f;
    float maxScale = 2.5f;
    float scrollSensitivity = 0.35f;
    bool pointerInside;
    bool dragging;
    Vector2 lastLocalPoint;
    float scale = 1f;

    public void Setup(RectTransform viewportRect, RectTransform target, float sensitivity = 0.35f)
    {
        viewport = viewportRect;
        panTarget = target;
        canvas = viewport != null ? viewport.GetComponentInParent<Canvas>() : null;
        scrollSensitivity = Mathf.Max(0.01f, sensitivity);
        ResetView();
    }

    public void ResetView()
    {
        scale = 1f;
        if (panTarget != null)
        {
            panTarget.localScale = Vector3.one;
            panTarget.anchoredPosition = Vector2.zero;
        }
    }

    void Update()
    {
        if (panTarget == null || viewport == null || !gameObject.activeInHierarchy)
            return;

        if (!pointerInside && !dragging)
            return;

        if (Input.GetMouseButtonDown(0) && pointerInside && !IsPointerOverButton())
        {
            if (TryGetLocalPoint(Input.mousePosition, out lastLocalPoint))
                dragging = true;
        }

        if (Input.GetMouseButtonUp(0))
            dragging = false;

        if (dragging && Input.GetMouseButton(0))
        {
            if (TryGetLocalPoint(Input.mousePosition, out var current))
            {
                panTarget.anchoredPosition += current - lastLocalPoint;
                lastLocalPoint = current;
            }
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f && pointerInside)
        {
            float prevScale = scale;
            scale = Mathf.Clamp(scale + scroll * scrollSensitivity, minScale, maxScale);
            if (Mathf.Approximately(prevScale, scale))
                return;

            ApplyZoom(prevScale);
        }
    }

    void ApplyZoom(float prevScale)
    {
        if (!TryGetLocalPoint(Input.mousePosition, out var localPoint))
            return;

        Vector2 focusOffset = localPoint - panTarget.anchoredPosition;
        float scaleRatio = scale / prevScale;
        panTarget.anchoredPosition -= focusOffset * (scaleRatio - 1f);
        panTarget.localScale = Vector3.one * scale;
    }

    bool TryGetLocalPoint(Vector2 screenPoint, out Vector2 localPoint)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport,
            screenPoint,
            GetEventCamera(),
            out localPoint);
    }

    Camera GetEventCamera()
    {
        if (canvas == null)
            canvas = viewport != null ? viewport.GetComponentInParent<Canvas>() : null;
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    bool IsPointerOverButton()
    {
        if (EventSystem.current == null)
            return false;

        var pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (result.gameObject.GetComponentInParent<UnityEngine.UI.Button>() != null)
                return true;
        }

        return false;
    }

    public void OnPointerEnter(PointerEventData eventData) => pointerInside = true;

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        dragging = false;
    }
}
