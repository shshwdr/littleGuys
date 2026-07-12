using UnityEngine;
using UnityEngine.EventSystems;

public class UpgradeTreePanZoom : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    RectTransform viewport;
    RectTransform panTarget;
    float minScale = 0.4f;
    float maxScale = 2.5f;
    float scrollSensitivity = 0.35f;
    bool pointerInside;
    bool dragging;
    Vector2 lastMousePosition;
    float scale = 1f;

    public void Setup(RectTransform viewportRect, RectTransform target, float sensitivity = 0.35f)
    {
        viewport = viewportRect;
        panTarget = target;
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
            dragging = true;
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
            dragging = false;

        if (dragging && Input.GetMouseButton(0))
        {
            Vector2 current = Input.mousePosition;
            panTarget.anchoredPosition += (current - lastMousePosition) * scrollSensitivity;
            lastMousePosition = current;
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
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport,
            Input.mousePosition,
            null,
            out localPoint);

        Vector2 focusOffset = localPoint - panTarget.anchoredPosition;
        float scaleRatio = scale / prevScale;
        panTarget.anchoredPosition -= focusOffset * (scaleRatio - 1f);
        panTarget.localScale = Vector3.one * scale;
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
