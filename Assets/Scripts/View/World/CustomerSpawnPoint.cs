using System.Collections.Generic;
using UnityEngine;

public class CustomerSpawnPoint : MonoBehaviour
{
    [SerializeField] List<Transform> entryPoints = new List<Transform>();

    public int SlotCount => entryPoints != null ? entryPoints.Count : 0;

    public Vector2 GetPosition(int index)
    {
        if (entryPoints == null || index < 0 || index >= entryPoints.Count)
            return transform.position;

        var point = entryPoints[index];
        return point != null ? point.position : transform.position;
    }

    void OnDrawGizmosSelected()
    {
        if (entryPoints == null)
            return;

        for (int i = 0; i < entryPoints.Count; i++)
        {
            var point = entryPoints[i];
            if (point == null)
                continue;

            Gizmos.color = new Color(1f, 0.6f, 0.2f);
            Gizmos.DrawWireSphere(point.position, 0.2f);
        }
    }
}
