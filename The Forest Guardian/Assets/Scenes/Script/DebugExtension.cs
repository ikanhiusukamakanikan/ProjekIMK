using UnityEngine;

public static class DebugExtension
{
    public static void DebugWireSphere(
        Vector3 position,
        Color color,
        float radius,
        float duration = 0,
        int segments = 16
    )
    {
        float angle = 0f;

        Vector3 lastPoint = position + new Vector3(
            Mathf.Cos(angle),
            0,
            Mathf.Sin(angle)
        ) * radius;

        for (int i = 1; i <= segments; i++)
        {
            angle += 2f * Mathf.PI / segments;

            Vector3 nextPoint = position + new Vector3(
                Mathf.Cos(angle),
                0,
                Mathf.Sin(angle)
            ) * radius;

            Debug.DrawLine(
                lastPoint,
                nextPoint,
                color,
                duration
            );

            lastPoint = nextPoint;
        }
    }
}