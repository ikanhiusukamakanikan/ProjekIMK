using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Terrain")]
    public Terrain terrain;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (terrain != null)
        {
            terrain.terrainData = Instantiate(terrain.terrainData);
        }
    }
}