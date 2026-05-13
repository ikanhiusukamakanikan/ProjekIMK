using UnityEngine;

public enum FireState { Idle, Burning, Burnt }

[RequireComponent(typeof(ParticleSystem))]
[RequireComponent(typeof(Collider))]
public class FireNode : MonoBehaviour
{
    public FireState state = FireState.Idle;

    // ========================
    // 🔥 HEALTH
    // ========================
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    // ========================
    // 🔥 VISUAL
    // ========================
    [Header("Visual")]
    public Transform visualRoot; // biasanya = transform
    private Vector3 initialScale;

    private ParticleSystem fireParticle;
    private ParticleSystem.MainModule mainModule;

    // ========================
    // 🔥 SPREAD
    // ========================
    [Header("Spread")]
    public float spreadRadius = 3f;
    public int spawnCount = 3;

    [Header("Generation Limit")]
    public int generation = 0;
    public int maxGeneration = 3;

    bool hasSpread = false;

    // ========================
    // 🐞 DEBUG
    // ========================
    [Header("Debug")]
    public bool enableDebug = false;

    // ========================
    // 🚀 UNITY EVENTS
    // ========================
    void Awake()
    {
        fireParticle = GetComponent<ParticleSystem>();

        if (visualRoot == null)
            visualRoot = transform;

        initialScale = visualRoot.localScale;

        mainModule = fireParticle.main;
    }

    void Start()
    {
        currentHealth = maxHealth;

        if (state == FireState.Burning)
        {
            Log($"START Burning | Gen {generation}");
            Invoke(nameof(SpreadFire), GetDelay());
        }
    }

    // ========================
    // 🔥 IGNITE
    // ========================
    public void Ignite()
    {
        if (state == FireState.Burnt) return; 

        state = FireState.Burning;

        currentHealth = maxHealth;
        UpdateVisual();

        Log($"IGNITE | Gen {generation}");

        // 🔥 burn terrain
        if (TerrainBurner.Instance != null)
            TerrainBurner.Instance.BurnAtPosition(transform.position);

        BurnNearbyTrees();

        if (generation < maxGeneration)
        {
            Invoke(nameof(SpreadFire), GetDelay());
        }
    }

    void BurnNearbyTrees()
    {
        var trees = TreeManager.Instance.allTrees;

        float burnRadius = 10f;
        float radiusSqr = burnRadius * burnRadius;

        Vector3 firePos = transform.position;

        Debug.Log($"[FireNode] Checking {trees.Count} trees near fire.");

        int burnedCount = 0;

        for (int i = trees.Count - 1; i >= 0; i--)
        {
            GameObject tree = trees[i];

            if (tree == null)
            {
                Debug.Log($"[FireNode] Tree index {i} is NULL");
                continue;
            }

            Vector3 diff = tree.transform.position - firePos;
            float distSqr = diff.sqrMagnitude;

            Debug.Log($"[FireNode] Checking tree: {tree.name} | DistSqr: {distSqr}");

            if (distSqr <= radiusSqr)
            {
                Debug.Log($"[FireNode] BURNING TREE: {tree.name}");

                TreeBurner.Instance.BurnTree(tree);

                burnedCount++;
            }
        }

        Debug.Log($"[FireNode] Total Burned Trees: {burnedCount}");
    }

    // ========================
    // 🔥 SPREAD SYSTEM
    // ========================
    float GetDelay()
    {
        switch (generation)
        {
            case 0: return 1f;
            case 1: return 3f;
            case 2: return 6f;
            default: return 0f;
        }
    }

    void SpreadFire()
    {
        if (hasSpread) return;
        if (generation >= maxGeneration) return;

        hasSpread = true;

        Log($"SPREAD | Gen {generation}");

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-spreadRadius, spreadRadius),
                0,
                Random.Range(-spreadRadius, spreadRadius)
            );

            Vector3 spawnPos = transform.position + offset;

            Terrain terrain = GameManager.Instance.terrain;

            if (terrain == null)
            {
                Error("Terrain NULL!");
                return;
            }

            float y = terrain.SampleHeight(spawnPos) + terrain.transform.position.y;
            spawnPos.y = y;

            if (!FireSpawner.Instance.IsValidHeight(spawnPos.y))
                continue;

            GameObject fire = Instantiate(
                FireSpawner.Instance.firePrefab,
                spawnPos,
                Quaternion.Euler(-90, 0, 0)
            );

            FireNode node = fire.GetComponent<FireNode>();

            if (node != null)
            {
                node.generation = generation + 1;
                node.Ignite();
            }
        }

        state = FireState.Burnt;
    }

    // ========================
    // 💧 DAMAGE (AIR)
    // ========================
    public void TakeDamage(float dmg)
    {
        if (currentHealth <= 0) return;

        currentHealth -= dmg;

        Log($"DAMAGE {dmg} | HP {currentHealth}");

        UpdateVisual();

        if (currentHealth <= 20)
        {
            Extinguish();
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Water"))
        {
            TakeDamage(1f);
        }
    }

    // ========================
    // 🎨 VISUAL UPDATE
    // ========================
    void UpdateVisual()
    {
        float t = Mathf.Clamp01(currentHealth / maxHealth);

        // scale mengecil
        visualRoot.localScale = initialScale * t;

        // alpha turun
        Color c = mainModule.startColor.color;
        c.a = t;
        mainModule.startColor = c;
    }

    // ========================
    // 🔥 MATI
    // ========================
    void Extinguish()
    {
        state = FireState.Burnt;

        Log("EXTINGUISHED");

        CancelInvoke();

        if (fireParticle != null)
            fireParticle.Stop();

        Destroy(gameObject);
    }

    // ========================
    // 🐞 DEBUG
    // ========================
    void Log(string msg)
    {
        if (enableDebug)
            Debug.Log($"[FireNode] {msg}", this);
    }

    void Error(string msg)
    {
        Debug.LogError($"[FireNode] {msg}", this);
    }
}