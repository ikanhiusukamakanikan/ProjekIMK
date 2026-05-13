using UnityEngine;

public class TreeBurner : MonoBehaviour
{
    public static TreeBurner Instance;

    public GameObject burnedTreePrefab;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void BurnTree(GameObject tree)
    {
        if (!tree.CompareTag("Tree"))
            return;

        Vector3 pos = tree.transform.position;
        Quaternion rot = tree.transform.rotation;
        Vector3 scale = tree.transform.localScale;

        GameObject burned = Instantiate(
            burnedTreePrefab,
            pos,
            rot
        );

        burned.transform.localScale = scale;

        TreeManager.Instance.RemoveTree(tree);

        Destroy(tree);
    }
}