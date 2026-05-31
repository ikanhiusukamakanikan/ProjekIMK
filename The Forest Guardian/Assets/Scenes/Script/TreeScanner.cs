using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.UI;
using System.Collections;

public class TreeScanner : MonoBehaviour
{
    public static event Action<TreeScanner> ScannerGrabbed;
    public static event Action<TreeScanner> ScannerReleased;
    public static event Action<TreeScanner, TreeData, bool> TreeScanned;

    [Header("XR")]
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    [Header("Scanner")]
    public GameObject ScannerLaser;
    public float scanDistance = 20f;
    public float scanInterval = 3f;

    [Header("Raycast")]
    public LayerMask scanLayers;

    [Header("UI")]
    public Image resultImage;
    public Sprite goodSprite;
    public Sprite badSprite;

    private bool isHeld = false;
    private Coroutine scanRoutine;

    void Start()
    {
        Debug.Log("[TreeScanner] Start");

        if (grabInteractable == null)
        {
            grabInteractable =
                GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            Debug.Log("[TreeScanner] Auto assigned XRGrabInteractable");
        }

        ScannerLaser.SetActive(false);

        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);

        Debug.Log("[TreeScanner] Listeners Added");
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        Debug.Log("[TreeScanner] Scanner Grabbed");

        isHeld = true;

        ScannerLaser.SetActive(true);
        ScannerGrabbed?.Invoke(this);

        scanRoutine = StartCoroutine(ScanRoutine());
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        Debug.Log("[TreeScanner] Scanner Released");

        isHeld = false;

        ScannerLaser.SetActive(false);
        ScannerReleased?.Invoke(this);

        if (scanRoutine != null)
        {
            StopCoroutine(scanRoutine);

            Debug.Log("[TreeScanner] Scan Routine Stopped");
        }
    }

    IEnumerator ScanRoutine()
    {
        Debug.Log("[TreeScanner] Scan Routine Started");

        while (isHeld)
        {
            ScanTree();

            yield return new WaitForSeconds(scanInterval);
        }
    }

    void ScanTree()
    {
        Vector3 origin = ScannerLaser.transform.position;
        Vector3 direction = ScannerLaser.transform.right;

        RaycastHit hit;

        Debug.Log("[TreeScanner] Shooting Raycast");

        if (Physics.Raycast(
            origin,
            direction,
            out hit,
            scanDistance,
            scanLayers
        ))
        {
            // VISUALIZE HIT
            Debug.DrawRay(
                origin,
                direction * hit.distance,
                Color.green,
                scanInterval
            );

            Debug.DrawRay(
                hit.point,
                hit.normal,
                Color.yellow,
                scanInterval
            );

            DebugExtension.DebugWireSphere(
                hit.point,
                Color.red,
                0.15f,
                scanInterval
            );

            Debug.Log("[TreeScanner] Hit Object: " + hit.collider.name);

            Debug.Log("[TreeScanner] Layer: " +
                LayerMask.LayerToName(hit.collider.gameObject.layer));

            if (hit.collider.CompareTag("Tree"))
            {
                Debug.Log("[TreeScanner] Hit Tree");

                TreeData treeData =
                    hit.collider.GetComponent<TreeData>();

                if (treeData != null)
                {
                    Debug.Log("[TreeScanner] TreeData Found");

                    Debug.Log("[TreeScanner] isBadTree = " +
                        treeData.isBadTree);

                    if (treeData.isBadTree)
                    {
                        Debug.Log("[TreeScanner] BAD TREE DETECTED");

                        if (resultImage != null)
                        {
                            resultImage.sprite = badSprite;
                        }
                    }
                    else
                    {
                        Debug.Log("[TreeScanner] GOOD TREE DETECTED");

                        if (resultImage != null)
                        {
                            resultImage.sprite = goodSprite;
                        }
                    }

                    TreeScanned?.Invoke(this, treeData, treeData.isBadTree);
                }
                else
                {
                    Debug.LogWarning(
                        "[TreeScanner] TreeData NOT found");
                }
            }
            else
            {
                Debug.Log(
                    "[TreeScanner] Hit object is NOT tagged Tree");
            }
        }
        else
        {
            // VISUALIZE MISS
            Debug.DrawRay(
                origin,
                direction * scanDistance,
                Color.red,
                scanInterval
            );

            Debug.Log("[TreeScanner] Raycast missed");
        }
    }
}
