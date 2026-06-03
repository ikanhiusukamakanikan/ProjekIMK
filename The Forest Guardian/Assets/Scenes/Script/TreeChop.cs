using System;
using UnityEngine;

public class TreeChop : MonoBehaviour
{
    public static event Action<TreeChop, TreeData> TreeBroken;

    [Header("Health")]
    public int maxHits = 5;

    [Header("Broken Prefab")]
    public GameObject brokenTreePrefab;

    [Header("Force")]
    public float fallForce = 7f;
    public float upwardForce = 2f;

    private int currentHits;
    private bool hasBroken;

    private void Start()
    {
        Debug.Log($"[TreeChop] Tree initialized: {gameObject.name}");
    }

    public void Chop(Vector3 hitPoint, Vector3 hitDirection)
    {
        if (hasBroken)
        {
            return;
        }

        Debug.Log("==================================");
        Debug.Log("[TreeChop] Chop called");

        currentHits++;
        SoundManager.PlaySound(SoundType.AxeChop);

        Debug.Log($"[TreeChop] Current Hits: {currentHits}/{maxHits}");

        Debug.Log($"[TreeChop] Hit Point: {hitPoint}");

        Debug.Log($"[TreeChop] Hit Direction: {hitDirection}");

        // Debug ray
        Debug.DrawRay(hitPoint, hitDirection * 2f, Color.red, 2f);

        if (currentHits >= maxHits)
        {
            Debug.Log("[TreeChop] Max hits reached -> BREAK TREE");

            BreakTree(hitPoint, hitDirection);
        }
        else
        {
            Debug.Log("[TreeChop] Tree not broken yet");
        }
    }

    void BreakTree(Vector3 hitPoint, Vector3 hitDirection)
    {
        if (hasBroken)
        {
            return;
        }

        Debug.Log("==================================");
        Debug.Log("[TreeChop] BreakTree() START");

        if (brokenTreePrefab == null)
        {
            Debug.LogError("[TreeChop] Broken Tree Prefab is NULL!");
            return;
        }

        hasBroken = true;
        SoundManager.PlaySound(SoundType.TreeFall);

        Debug.Log("[TreeChop] Instantiating broken tree prefab...");

        Transform treeRoot = transform.parent != null ? transform.parent : transform;
        TreeData treeData = treeRoot.GetComponentInChildren<TreeData>();

        if (treeData == null)
        {
            treeData = GetComponentInParent<TreeData>();
        }

        // Spawn broken tree
        GameObject broken =
            Instantiate(
                brokenTreePrefab,
                treeRoot.position,
                treeRoot.rotation
            );

        Debug.Log($"[TreeChop] Broken tree spawned: {broken.name}");

        // Find top
        Debug.Log("[TreeChop] Searching for child: Top");

        Transform top = broken.transform.Find("Top");

        if (top != null)
        {
            Debug.Log("[TreeChop] Top found");

            Rigidbody rb = top.GetComponent<Rigidbody>();

            if (rb != null)
            {
                Debug.Log("[TreeChop] Rigidbody found on Top");

                // Fall opposite hit direction
                Vector3 fallDir = -hitDirection.normalized;

                Debug.Log($"[TreeChop] Fall Direction: {fallDir}");

                // Slight upward lift
                Vector3 finalForce =
                    (fallDir + Vector3.up * 0.3f).normalized;

                Debug.Log($"[TreeChop] Final Force Direction: {finalForce}");

                Debug.Log($"[TreeChop] Applying force: {fallForce}");

                rb.AddForce(
                    finalForce * fallForce,
                    ForceMode.Impulse
                );

                Vector3 randomTorque =
                    UnityEngine.Random.insideUnitSphere * 100f;

                Debug.Log($"[TreeChop] Applying torque: {randomTorque}");

                rb.AddTorque(
                    randomTorque,
                    ForceMode.Impulse
                );

                Debug.Log("[TreeChop] Scheduling Top destroy in 30 seconds");

                Destroy(top.gameObject, 30f);
            }
            else
            {
                Debug.LogError("[TreeChop] Rigidbody NOT found on Top!");
            }
        }
        else
        {
            Debug.LogError("[TreeChop] Child named 'Top' NOT found!");
        }

        Debug.Log("[TreeChop] Destroying parent tree object");

        TreeBroken?.Invoke(this, treeData);

        if (TreeManager.Instance != null)
        {
            TreeManager.Instance.RemoveTree(treeRoot.gameObject);
        }

        Destroy(treeRoot.gameObject);

        Debug.Log("[TreeChop] BreakTree() END");
        Debug.Log("==================================");
    }
}
