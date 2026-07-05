using UnityEngine;

public class CustomerSpawnPoint : MonoBehaviour
{
    [SerializeField] int slotIndex;
    [SerializeField] int capacity = 5;
    [SerializeField] float spacing = 1.85f;
    [SerializeField] Transform entryPoint;
    [SerializeField] Transform sacrificePoint;
    [SerializeField] Transform deliveryPoint;

    public int SlotIndex => slotIndex;
    public int Capacity => capacity > 0 ? capacity : 1;
    public float Spacing => spacing;

    public Vector2 GetStandPosition(int localIndex)
    {
        return (Vector2)transform.position + new Vector2(-localIndex * spacing, 0f);
    }

    public Vector2 GetEntryPosition(int localIndex)
    {
        Vector2 stand = GetStandPosition(localIndex);
        if (entryPoint != null)
            return stand + ((Vector2)entryPoint.position - (Vector2)transform.position);

        return stand + new Vector2(-4f, 0f);
    }

    public Vector2 GetSacrificePosition(int localIndex)
    {
        Vector2 stand = GetStandPosition(localIndex);
        if (sacrificePoint != null)
            return stand + ((Vector2)sacrificePoint.position - (Vector2)transform.position);

        return stand + new Vector2(0f, -1.1f);
    }

    public Vector2 GetDeliveryPosition(int localIndex)
    {
        Vector2 stand = GetStandPosition(localIndex);
        if (deliveryPoint != null)
            return stand + ((Vector2)deliveryPoint.position - (Vector2)transform.position);

        return stand + new Vector2(0f, 0.3f);
    }

    void OnDrawGizmosSelected()
    {
        for (int i = 0; i < Capacity; i++)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f);
            Gizmos.DrawWireSphere(GetStandPosition(i), 0.2f);
        }

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
