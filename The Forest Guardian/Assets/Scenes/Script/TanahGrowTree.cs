using System.Collections;
using UnityEngine;

public class TanahGrowTree : MonoBehaviour
{
    [Header("Setup")]
    public GameObject treePrefab;

    [Header("Tag")]
    public string tunasTag = "tunas";

    [Header("Timing")]
    public float delayBeforeGrow = 1.5f;
    public float growDuration = 2f;

    [Header("Scale")]
    public Vector3 finalScale = Vector3.one;

    private bool alreadyTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (alreadyTriggered) return;

        if (other.CompareTag(tunasTag))
        {
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

        // Hapus tanah
        Destroy(gameObject);
    }
}