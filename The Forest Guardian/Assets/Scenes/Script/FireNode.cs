using System;
using UnityEngine;

public enum FireState
{
    Idle,
    Burning,
    Burnt
}

[RequireComponent(typeof(ParticleSystem))]
[RequireComponent(typeof(Collider))]
public class FireNode : MonoBehaviour
{
    public static event Action<FireNode> FireIgnited;
    public static event Action<FireNode> FireBurnedOut;
    public static event Action<FireNode> FireExtinguished;

    public FireState state = FireState.Idle;

    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Visual")]
    public Transform visualRoot;
    private Vector3 initialScale;

    private ParticleSystem fireParticle;
    private ParticleSystem.MainModule mainModule;

    [Header("Spread")]
    public float spreadRadius = 3f;
    public int spawnCount = 3;

    [Header("Generation Limit")]
    public int generation = 0;
    public int maxGeneration = 3;

    [Header("Debug")]
    public bool enableDebug = false;

    private bool hasSpread;
    private bool hasScheduledSpread;
    private bool hasReportedBurning;
    private bool hasReportedBurnedOut;
    private bool hasReportedExtinguished;
    private bool hasAppliedIgniteEffects;
    private float nextExtinguishSoundTime;

    void Awake()
    {
        fireParticle = GetComponent<ParticleSystem>();

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        initialScale = visualRoot.localScale;
        mainModule = fireParticle.main;
        currentHealth = maxHealth;
    }

    void Start()
    {
        if (currentHealth <= 0f)
        {
            currentHealth = maxHealth;
        }

        if (state == FireState.Burning)
        {
            Log($"START Burning | Gen {generation}");
            ReportIgnited();
            ApplyIgniteEffectsOnce();
            ScheduleSpread();
        }
    }

    public void Ignite()
    {
        if (state == FireState.Burnt)
        {
            return;
        }

        state = FireState.Burning;
        currentHealth = maxHealth;
        UpdateVisual();

        Log($"IGNITE | Gen {generation}");

        ReportIgnited();
        ApplyIgniteEffectsOnce();
        ScheduleSpread();
    }

    private void ApplyIgniteEffectsOnce()
    {
        if (hasAppliedIgniteEffects)
        {
            return;
        }

        hasAppliedIgniteEffects = true;

        if (TerrainBurner.Instance != null)
        {
            TerrainBurner.Instance.BurnAtPosition(transform.position);
        }

        BurnNearbyTrees();
    }

    private void BurnNearbyTrees()
    {
        if (TreeManager.Instance == null || TreeBurner.Instance == null)
        {
            return;
        }

        var trees = TreeManager.Instance.allTrees;
        float burnRadius = 10f;
        float radiusSqr = burnRadius * burnRadius;
        Vector3 firePos = transform.position;
        int burnedCount = 0;

        Debug.Log($"[FireNode] Checking {trees.Count} trees near fire.");

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

    private void ScheduleSpread()
    {
        if (generation >= maxGeneration || hasScheduledSpread)
        {
            return;
        }

        hasScheduledSpread = true;
        Invoke(nameof(SpreadFire), GetDelay());
    }

    private float GetDelay()
    {
        switch (generation)
        {
            case 0:
                return 15f;
            case 1:
                return 45f;
            case 2:
                return 120f;
            default:
                return 0f;
        }
    }

    private void SpreadFire()
    {
        if (hasSpread || state != FireState.Burning || generation >= maxGeneration)
        {
            return;
        }

        hasSpread = true;
        Log($"SPREAD | Gen {generation}");

        for (int i = 0; i < spawnCount; i++)
        {
            if (GameManager.Instance == null || GameManager.Instance.terrain == null)
            {
                Error("Terrain NULL!");
                return;
            }

            if (FireSpawner.Instance == null || FireSpawner.Instance.firePrefab == null)
            {
                Error("FireSpawner or fire prefab NULL!");
                return;
            }

            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-spreadRadius, spreadRadius),
                0f,
                UnityEngine.Random.Range(-spreadRadius, spreadRadius)
            );

            Vector3 spawnPos = transform.position + offset;
            Terrain terrain = GameManager.Instance.terrain;
            float y = terrain.SampleHeight(spawnPos) + terrain.transform.position.y;
            spawnPos.y = y;

            if (!FireSpawner.Instance.IsValidHeight(spawnPos.y))
            {
                continue;
            }

            GameObject fire = Instantiate(
                FireSpawner.Instance.firePrefab,
                spawnPos,
                Quaternion.Euler(-90f, 0f, 0f)
            );

            FireNode node = fire.GetComponent<FireNode>();
            if (node != null)
            {
                node.generation = generation + 1;
                node.Ignite();
            }
        }

        state = FireState.Burnt;
        ReportBurnedOut();
    }

    public void TakeDamage(float dmg)
    {
        if (state != FireState.Burning || currentHealth <= 0f)
        {
            return;
        }

        currentHealth -= dmg;
        Log($"DAMAGE {dmg} | HP {currentHealth}");

        if (Time.time >= nextExtinguishSoundTime)
        {
            SoundManager.PlaySound(SoundType.FireExtinguish);
            nextExtinguishSoundTime = Time.time + 3f;
        }

        UpdateVisual();

        if (currentHealth <= 20f)
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

    private void UpdateVisual()
    {
        float t = maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

        if (visualRoot != null)
        {
            visualRoot.localScale = initialScale * t;
        }

        if (fireParticle != null)
        {
            Color color = mainModule.startColor.color;
            color.a = t;
            mainModule.startColor = color;
        }
    }

    private void Extinguish()
    {
        if (hasReportedExtinguished)
        {
            return;
        }

        hasReportedExtinguished = true;
        state = FireState.Burnt;

        Log("EXTINGUISHED");
        CancelInvoke();

        if (fireParticle != null)
        {
            fireParticle.Stop();
        }

        FireExtinguished?.Invoke(this);
        Destroy(gameObject);
    }

    private void ReportIgnited()
    {
        if (hasReportedBurning)
        {
            return;
        }

        hasReportedBurning = true;
        FireIgnited?.Invoke(this);
    }

    private void ReportBurnedOut()
    {
        if (hasReportedBurnedOut)
        {
            return;
        }

        hasReportedBurnedOut = true;
        CancelInvoke();

        if (fireParticle != null)
        {
            fireParticle.Stop();
        }

        FireBurnedOut?.Invoke(this);
    }

    private void Log(string msg)
    {
        if (enableDebug)
        {
            Debug.Log($"[FireNode] {msg}", this);
        }
    }

    private void Error(string msg)
    {
        Debug.LogError($"[FireNode] {msg}", this);
    }
}
