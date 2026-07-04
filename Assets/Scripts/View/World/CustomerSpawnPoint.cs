using UnityEngine;

public class CustomerSpawnPoint : MonoBehaviour
{
    [SerializeField] int slotIndex;
    [SerializeField] Transform entryPoint;
    [SerializeField] Transform sacrificePoint;
    [SerializeField] Transform deliveryPoint;

    public int SlotIndex => slotIndex;

    public Vector2 StandPosition => transform.position;

    public Vector2 GetEntryPosition()
    {
        if (entryPoint != null)
            return entryPoint.position;
        return StandPosition + new Vector2(-4f, 0f);
    }

    public Vector2 GetSacrificePosition()
    {
        if (sacrificePoint != null)
            return sacrificePoint.position;
        return StandPosition + new Vector2(0f, -1.1f);
    }

    public Vector2 GetDeliveryPosition()
    {
        if (deliveryPoint != null)
            return deliveryPoint.position;
        return StandPosition + new Vector2(0f, 0.3f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        DrawLink(entryPoint, Color.green);
        DrawLink(sacrificePoint, Color.red);
        DrawLink(deliveryPoint, Color.cyan);
    }

    static void DrawLink(Transform point, Color color)
    {
        if (point == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawLine(point.position, point.position + Vector3.up * 0.1f);
        Gizmos.DrawWireSphere(point.position, 0.12f);
    }
}
