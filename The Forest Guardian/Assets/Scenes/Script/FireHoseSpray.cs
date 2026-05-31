using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class FireHoseSpray : MonoBehaviour
{
    public static event Action<FireHoseSpray> FireHoseGrabbed;
    public static event Action<FireHoseSpray> FireHoseReleased;

    public GameObject sprayObject; // Assign in Inspector

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    private void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
        {
            return;
        }

        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        if (grabInteractable == null)
        {
            return;
        }

        grabInteractable.selectEntered.RemoveListener(OnGrab);
        grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (sprayObject != null)
            sprayObject.SetActive(true);

        FireHoseGrabbed?.Invoke(this);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (sprayObject != null)
            sprayObject.SetActive(false);

        FireHoseReleased?.Invoke(this);
    }
}
