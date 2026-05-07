using UnityEngine;

public class TerrainBurner : MonoBehaviour
{
    public static TerrainBurner Instance;

    private Terrain terrain;

    [Header("Detail Layers")]
    public int normalLayer = 0;
    public int burnedLayer = 1;

    public int burnRadius = 5;

    [Header("Debug")]
    public bool enableDebug = true;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        terrain = GameManager.Instance.terrain;

        if (terrain == null)
        {
            Error("Terrain NULL! Check GameManager.");
            return;
        }

        Log("TerrainBurner initialized");

        int count = terrain.terrainData.detailPrototypes.Length;
        Log("Detail Layer Count: " + count);

        if (normalLayer >= count || burnedLayer >= count)
        {
            Error($"Layer index invalid! normal:{normalLayer} burned:{burnedLayer}");
        }

        var prototypes = terrain.terrainData.detailPrototypes;

        for (int i = 0; i < prototypes.Length; i++)
        {
            string name = "UNKNOWN";

            if (prototypes[i].prototype != null)
                name = prototypes[i].prototype.name;
            else if (prototypes[i].prototypeTexture != null)
                name = prototypes[i].prototypeTexture.name;

            Debug.Log($"[TerrainBurner] Detail Layer {i} = {name}");
        }
    }

    public void BurnAtPosition(Vector3 worldPos)
    {
        if (terrain == null)
        {
            Error("Terrain NULL on Burn!");
            return;
        }

        TerrainData data = terrain.terrainData;

        Vector3 terrainPos = worldPos - terrain.transform.position;

        int x = (int)((terrainPos.x / data.size.x) * data.detailWidth);
        int z = (int)((terrainPos.z / data.size.z) * data.detailHeight);

        Log($"WorldPos: {worldPos}");
        Log($"TerrainPos: {terrainPos}");
        Log($"Mapped Detail Coord: ({x}, {z})");

        int size = burnRadius * 2;

        int startX = Mathf.Clamp(x - burnRadius, 0, data.detailWidth - size);
        int startZ = Mathf.Clamp(z - burnRadius, 0, data.detailHeight - size);

        Log($"Burn Area Start: ({startX}, {startZ}) Size: {size}x{size}");

        int[,] normal = data.GetDetailLayer(startX, startZ, size, size, normalLayer);
        int[,] burned = data.GetDetailLayer(startX, startZ, size, size, burnedLayer);

        int normalCountBefore = 0;
        int burnedCountBefore = 0;

        // 🔍 Hitung sebelum
        for (int zIdx = 0; zIdx < size; zIdx++)
        {
            for (int xIdx = 0; xIdx < size; xIdx++)
            {
                if (normal[zIdx, xIdx] > 0) normalCountBefore++;
                if (burned[zIdx, xIdx] > 0) burnedCountBefore++;
            }
        }

        Log($"Before → NormalCount: {normalCountBefore}, BurnedCount: {burnedCountBefore}");

        int applied = 0;

        for (int zIdx = 0; zIdx < size; zIdx++)
        {
            for (int xIdx = 0; xIdx < size; xIdx++)
            {
                // hanya burn kalau ada grass normal
                if (normal[zIdx, xIdx] > 0)
                {
                    int amount = normal[zIdx, xIdx];

                    // hapus dari normal
                    normal[zIdx, xIdx] = 0;

                    // pindah ke burned
                    burned[zIdx, xIdx] = amount;

                    applied++;
                }
            }
        }

        data.SetDetailLayer(startX, startZ, normalLayer, normal);
        data.SetDetailLayer(startX, startZ, burnedLayer, burned);

        terrain.Flush();

        // 🔍 VALIDASI setelah set
        int[,] check = data.GetDetailLayer(startX, startZ, size, size, burnedLayer);

        int burnedCountAfter = 0;

        for (int zIdx = 0; zIdx < size; zIdx++)
        {
            for (int xIdx = 0; xIdx < size; xIdx++)
            {
                if (check[zIdx, xIdx] > 0) burnedCountAfter++;
            }
        }

        Log($"After → BurnedCount: {burnedCountAfter}");
        Log($"Applied Cells: {applied}");

        if (burnedCountAfter == 0)
        {
            Warn("Burned layer tetap 0 setelah apply! (Render atau layer issue)");
        }
        else
        {
            Log("Burn SUCCESS (data applied)");
        }
    }

    // ===== DEBUG HELPERS =====
    void Log(string msg)
    {
        if (enableDebug)
            Debug.Log($"[TerrainBurner] {msg}", this);
    }

    void Warn(string msg)
    {
        if (enableDebug)
            Debug.LogWarning($"[TerrainBurner] {msg}", this);
    }

    void Error(string msg)
    {
        Debug.LogError($"[TerrainBurner] {msg}", this);
    }

    // 🔍 BONUS: visual debug di scene
    void OnDrawGizmosSelected()
    {
        if (!enableDebug) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, burnRadius);
    }
}