using UnityEngine;
using System.Collections.Generic;

public class TreeManager : MonoBehaviour
{
    public static TreeManager Instance;

    public Transform treeParent;

    public List<GameObject> allTrees = new();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        RegisterAllTrees();
    }

    [ContextMenu("Register Trees")]
    public void RegisterAllTrees()
    {
        allTrees.Clear();

        foreach (Transform child in treeParent)
        {
            if (child.CompareTag("Tree"))
            {
                allTrees.Add(child.gameObject);
            }
        }

        Debug.Log("Registered Trees: " + allTrees.Count);
    }

    public void RemoveTree(GameObject tree)
    {
        allTrees.Remove(tree);
    }
}