using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

public static class ClickDebugLogger
{
    static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>();

    public static void LogClickIfAny()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        var screenPos = Input.mousePosition;
        var builder = new StringBuilder();
        builder.AppendLine($"[Click] screen={screenPos}");

        if (EventSystem.current == null)
        {
            Debug.Log(builder + "[Click] No EventSystem.");
            return;
        }

        builder.AppendLine($"[Click] IsPointerOverGameObject={EventSystem.current.IsPointerOverGameObject()}");

        var pointerData = new PointerEventData(EventSystem.current) { position = screenPos };
        RaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, RaycastResults);

        if (RaycastResults.Count == 0)
        {
            builder.AppendLine("[Click] No UI raycast hit.");
            LogPhysicsHit(builder, screenPos);
            Debug.Log(builder.ToString());
            return;
        }

        builder.AppendLine($"[Click] Top UI hit: {DescribeRaycastResult(RaycastResults[0])}");
        for (int i = 1; i < RaycastResults.Count && i < 5; i++)
            builder.AppendLine($"[Click]   alt[{i}]: {DescribeRaycastResult(RaycastResults[i])}");

        Debug.Log(builder.ToString());
    }

    static void LogPhysicsHit(StringBuilder builder, Vector3 screenPos)
    {
        var cam = Camera.main;
        if (cam == null)
            return;

        var worldPos = cam.ScreenToWorldPoint(screenPos);
        var hit = Physics2D.OverlapPoint(worldPos);
        if (hit == null)
            return;

        builder.AppendLine($"[Click] Physics2D hit: {GetHierarchyPath(hit.transform)} ({hit.name})");
    }

    static string DescribeRaycastResult(RaycastResult result)
    {
        var go = result.gameObject;
        var graphic = result.module != null ? result.module.GetType().Name : "unknown-module";
        return $"{GetHierarchyPath(go.transform)} | module={graphic} | depth={result.depth} | sortingOrder={result.sortingOrder}";
    }

    static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return "<null>";

        var path = transform.name;
        var current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
