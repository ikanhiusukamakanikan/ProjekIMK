using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class FireHoseSpray : MonoBehaviour
{
    public GameObject sprayObject; // Assign in Inspector

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    private void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrab);
        grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (sprayObject != null)
            sprayObject.SetActive(true);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (sprayObject != null)
            sprayObject.SetActive(false);
    }
}