using System;
using System.Collections;
using UnityEngine;

public class TanahGrowTree : MonoBehaviour
{
    public static event Action<TanahGrowTree, GameObject> TreeGrown;
    public static event Action<TanahGrowTree> PlantingBlockedByBurnRule;

    [Header("Setup")]
    public GameObject treePrefab;

    [Header("Tag")]
    public string tunasTag = "tunas";

    [Header("Timing")]
    public float delayBeforeGrow = 1.5f;
    public float growDuration = 2f;

    [Header("Scale")]
    public Vector3 finalScale = Vector3.one;

    [Header("Burned Area Planting")]
    public bool preferBurnedAreaWhenAvailable = true;
    public float burnedAreaCheckRadius = 2f;
    public bool clearBurnedAreaAfterGrow = true;

    private bool alreadyTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (alreadyTriggered) return;

        if (other.CompareTag(tunasTag))
        {
            if (!CanPlantHere())
            {
                PlantingBlockedByBurnRule?.Invoke(this);
                return;
            }

            alreadyTriggered = true;
            Destroy(other.gameObject);
            StartCoroutine(GrowSequence());
        }
    }

    IEnumerator GrowSequence()
    {
        // Delay sebelum tumbuh
        yield return new WaitForSeconds(delayBeforeGrow);

        // Spawn pohon kecil
        GameObject tree = Instantiate(treePrefab, transform.position, Quaternion.identity);

        tree.transform.localScale = Vector3.zero;

        // Animasi scale
        float time = 0f;

        while (time < growDuration)
        {
            float t = time / growDuration;

            // Smooth curve biar enak dilihat (ease out)
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            tree.transform.localScale = Vector3.Lerp(Vector3.zero, finalScale, smoothT);

            time += Time.deltaTime;
            yield return null;
        }

        // Pastikan scale final
        tree.transform.localScale = finalScale;

        if (TreeManager.Instance != null && tree.CompareTag("Tree") && !TreeManager.Instance.allTrees.Contains(tree))
        {
            TreeManager.Instance.allTrees.Add(tree);
        }

        if (clearBurnedAreaAfterGrow && TerrainBurner.Instance != null)
        {
            TerrainBurner.Instance.ClearBurnedAtPosition(transform.position, burnedAreaCheckRadius);
        }

        TreeGrown?.Invoke(this, tree);

        // Hapus tanah
        Destroy(gameObject);
    }

    private bool CanPlantHere()
    {
        if (!preferBurnedAreaWhenAvailable || TerrainBurner.Instance == null)
        {
            return true;
        }

        if (!TerrainBurner.Instance.HasBurnedArea)
        {
            return true;
        }

        return TerrainBurner.Instance.IsPositionInBurnedArea(transform.position, burnedAreaCheckRadius);
    }
}
