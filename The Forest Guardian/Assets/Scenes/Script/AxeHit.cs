using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class AxeHit : MonoBehaviour
{
    [Header("Detection")]
    public float rayRadius = 0.15f;

    [Header("Debug")]
    public bool showDebugRay = true;

    private Vector3 lastPosition;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    void Awake()
    {
        grabInteractable = GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    }

    void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrab);
        }
    }

    void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        SoundManager.PlaySound(SoundType.Pickup);
    }

    void Start()
    {
        lastPosition = transform.position;

        Debug.Log("==================================");
        Debug.Log("[AxeHit] Axe initialized");
        Debug.Log($"[AxeHit] Start Position: {lastPosition}");
        Debug.Log($"[AxeHit] Ray Radius: {rayRadius}");
        Debug.Log("==================================");
    }

    void Update()
    {
        Vector3 direction =
            transform.position - lastPosition;

        float distance = direction.magnitude;

        Debug.Log("----------------------------------");
        Debug.Log("[AxeHit] Update");

        Debug.Log($"[AxeHit] Current Position: {transform.position}");

        Debug.Log($"[AxeHit] Last Position: {lastPosition}");

        Debug.Log($"[AxeHit] Direction: {direction}");

        Debug.Log($"[AxeHit] Distance Moved: {distance}");

        // Draw movement ray
        if (showDebugRay)
        {
            Debug.DrawRay(
                lastPosition,
                direction,
                Color.green
            );
        }

        // Prevent tiny movement checks
        if (distance > 0.001f)
        {
            Debug.Log("[AxeHit] Movement detected");

            RaycastHit hit;

            bool didHit = Physics.SphereCast(
                lastPosition,
                rayRadius,
                direction.normalized,
                out hit,
                distance
            );

            if (didHit)
            {
                Debug.Log("[AxeHit] SphereCast HIT something");

                Debug.Log($"[AxeHit] Hit Object: {hit.collider.name}");

                Debug.Log($"[AxeHit] Hit Point: {hit.point}");

                Debug.Log($"[AxeHit] Hit Distance: {hit.distance}");

                Debug.Log($"[AxeHit] Hit Tag: {hit.collider.tag}");

                // Draw hit normal
                Debug.DrawRay(
                    hit.point,
                    hit.normal,
                    Color.red,
                    1f
                );

                if (hit.collider.CompareTag("Tree"))
                {
                    Debug.Log("[AxeHit] Hit object is TREE");

                    TreeChop tree =
                        hit.collider.GetComponentInParent<TreeChop>();

                    if (tree != null)
                    {
                        Debug.Log("[AxeHit] TreeChop component FOUND");

                        Debug.Log("[AxeHit] Calling Chop()");

                        tree.Chop(
                            hit.point,
                            direction.normalized
                        );
                    }
                    else
                    {
                        Debug.LogError(
                            "[AxeHit] TreeChop component NOT FOUND in parent!"
                        );
                    }
                }
                else
                {
                    Debug.Log("[AxeHit] Hit object is NOT tree");
                }
            }
            else
            {
                Debug.Log("[AxeHit] SphereCast hit NOTHING");
            }
        }
        else
        {
            Debug.Log("[AxeHit] Movement too small, skipping SphereCast");
        }

        lastPosition = transform.position;
    }
}
