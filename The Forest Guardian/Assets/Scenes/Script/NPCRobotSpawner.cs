using UnityEngine;
using DG.Tweening;

public class NPCRobotSpawner : MonoBehaviour
{
    [Header("References")]
    public Transform spawnPoint;
    public GameObject robotPrefab;
    public Transform playerTransform;

    [Header("Idle Detection")]
    public float idleThreshold = 0.1f; // Jarak movement yang dianggap "not moving"
    public float idleDuration = 10f;  // Waktu sebelum spawn (detik)

    [Header("Distance Check")]
    public float maxDistance = 50f;   // Jarak maksimal sebelum NPC hilang

    [Header("Animation Settings")]
    public float spawnDuration = 1.5f;     // Durasi animasi spawn (dari bawah)
    public float despawnDuration = 1.5f;   // Durasi animasi despawn (ke atas)
    public float spawnHeight = 5f;         // Tinggi offset saat spawn dari bawah
    public Ease spawnEase = Ease.OutQuart; // Easing untuk spawn
    public Ease despawnEase = Ease.InQuart;// Easing untuk despawn

    private Vector3 lastPlayerPosition;
    private float idleTimer = 0f;
    private GameObject currentRobotInstance;
    private bool isSpawned = false;
    private float distanceCheckTimer = 0f;
    private float distanceCheckInterval = 0.5f; // Check jarak setiap 0.5 detik

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
            }
        }

        lastPlayerPosition = playerTransform.position;
    }

    void Update()
    {
        if (playerTransform == null)
        {
            return;
        }

        // Cek idle status player
        if (!isSpawned)
        {
            CheckPlayerIdle();
        }
        else
        {
            // Jika sudah spawn, cek jarak
            CheckDistanceToNPC();
        }
    }

    private void CheckPlayerIdle()
    {
        // Hitung jarak movement player
        float distanceMoved = Vector3.Distance(playerTransform.position, lastPlayerPosition);

        if (distanceMoved < idleThreshold)
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= idleDuration)
            {
                SpawnRobot();
                idleTimer = 0f;
            }
        }
        else
        {
            idleTimer = 0f;
        }

        lastPlayerPosition = playerTransform.position;
    }

    private void CheckDistanceToNPC()
    {
        distanceCheckTimer += Time.deltaTime;

        if (distanceCheckTimer < distanceCheckInterval)
        {
            return;
        }

        distanceCheckTimer = 0f;

        if (currentRobotInstance == null)
        {
            isSpawned = false;
            return;
        }

        float distance = Vector3.Distance(playerTransform.position, currentRobotInstance.transform.position);

        if (distance > maxDistance)
        {
            DespawnRobot();
        }
    }

    private void SpawnRobot()
    {
        if (currentRobotInstance != null || spawnPoint == null || robotPrefab == null)
        {
            return;
        }

        // Spawn robot di posisi SpawnPoint, tapi tidak sebagai child
        Vector3 spawnPos = spawnPoint.position;
        Vector3 startPos = spawnPos + Vector3.down * spawnHeight;

        currentRobotInstance = Instantiate(robotPrefab, startPos, Quaternion.identity);
        isSpawned = true;

        // Animasi spawn: dari bawah ke posisi spawn point (levitating)
        currentRobotInstance.transform.DOMove(spawnPos, spawnDuration)
            .SetEase(spawnEase);

        // Optional: Tambah scale animation untuk efek lebih smooth
        currentRobotInstance.transform.localScale = Vector3.zero;
        currentRobotInstance.transform.DOScale(Vector3.one, spawnDuration)
            .SetEase(spawnEase);

        Debug.Log("[NPCRobotSpawner] Robot spawned at: " + spawnPos);
    }

    private void DespawnRobot()
    {
        if (currentRobotInstance == null)
        {
            return;
        }

        // Animasi despawn: ke atas (ditarik UFO style)
        Vector3 despawnTarget = currentRobotInstance.transform.position + Vector3.up * spawnHeight;

        currentRobotInstance.transform.DOMove(despawnTarget, despawnDuration)
            .SetEase(despawnEase);

        currentRobotInstance.transform.DOScale(Vector3.zero, despawnDuration)
            .SetEase(despawnEase)
            .OnComplete(() =>
            {
                if (currentRobotInstance != null)
                {
                    Destroy(currentRobotInstance);
                }
                currentRobotInstance = null;
                isSpawned = false;
                idleTimer = 0f;
            });

        Debug.Log("[NPCRobotSpawner] Robot despawned - distance exceeded");
    }

    void OnDrawGizmos()
    {
        if (spawnPoint != null)
        {
            // Draw spawn point
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPoint.position, 1f);
        }

        if (isSpawned && currentRobotInstance != null)
        {
            // Draw max distance radius
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(currentRobotInstance.transform.position, maxDistance);
        }
    }
}
