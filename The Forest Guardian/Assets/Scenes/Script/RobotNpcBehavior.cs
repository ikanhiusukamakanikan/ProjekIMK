using UnityEngine;

public class RobotNpcBehavior : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Camera playerCamera;
    public Animator animator;
    public Transform visualRoot;

    [Header("Look At Player")]
    public bool rotateOnlyOnYAxis = true;
    public float turnSpeed = 6f;

    [Header("Greeting")]
    public string happyParameter = "IsHappy";
    public float happyInterval = 10f;

    [Header("Looked At Reaction")]
    public string blushedParameter = "IsBlush";
    public string stopBlushedParameter = "IsNotLooking";
    public float lookedAtDuration = 5f;
    public float gazeAngle = 12f;
    public bool requireLineOfSight = true;
    public LayerMask lineOfSightMask = ~0;

    [Header("Not Visible Reaction")]
    public string sleepParameter = "IsSleep";
    public string stopSleepParameter = "IsLooking";
    public float notVisibleDuration = 5f;

    [Header("Bool Parameter Pulse")]
    public float boolPulseDuration = 0.25f;

    private Renderer[] renderers;
    private float happyTimer;
    private float lookedAtTimer;
    private float notVisibleTimer;
    private bool blushedTriggered;
    private bool sleepTriggered;
    private readonly bool[] boolResetPending = new bool[4];
    private readonly float[] boolResetTimes = new float[4];

    void Awake()
    {
        ResolveReferences();
        CacheRenderers();
        happyTimer = 0f;
    }

    void Update()
    {
        ResolveReferences();
        FacePlayer();
        HandleGreeting();
        HandleLookedAtReaction();
        HandleNotVisibleReaction();
        HandleBoolPulseResets();
    }

    private void ResolveReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
            else if (playerCamera != null)
            {
                player = playerCamera.transform;
            }
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }
    }

    private void CacheRenderers()
    {
        Transform rendererRoot = visualRoot != null ? visualRoot : transform;
        renderers = rendererRoot.GetComponentsInChildren<Renderer>(true);
    }

    private void FacePlayer()
    {
        if (player == null)
        {
            return;
        }

        Vector3 direction = player.position - transform.position;
        if (rotateOnlyOnYAxis)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Mathf.Max(0.01f, turnSpeed) * Time.deltaTime
        );
    }

    private void HandleGreeting()
    {
        if (blushedTriggered || sleepTriggered)
        {
            happyTimer = 0f;
            return;
        }

        happyTimer += Time.deltaTime;

        if (happyTimer < Mathf.Max(0.01f, happyInterval))
        {
            return;
        }

        TriggerAnimatorParameter(happyParameter, 0);
        happyTimer = 0f;
    }

    private void HandleLookedAtReaction()
    {
        if (IsLookedAt())
        {
            lookedAtTimer += Time.deltaTime;
            sleepTriggered = false;

            if (!blushedTriggered && lookedAtTimer >= lookedAtDuration)
            {
                TriggerAnimatorParameter(blushedParameter, 1);
                blushedTriggered = true;
            }

            return;
        }

        if (blushedTriggered)
        {
            TriggerAnimatorParameter(stopBlushedParameter, 3);
        }

        lookedAtTimer = 0f;
        blushedTriggered = false;
    }

    private void HandleNotVisibleReaction()
    {
        if (IsVisibleToCamera())
        {
            if (sleepTriggered)
            {
                TriggerAnimatorParameter(stopSleepParameter, 3);
            }

            notVisibleTimer = 0f;
            sleepTriggered = false;
            return;
        }

        notVisibleTimer += Time.deltaTime;

        if (!sleepTriggered && notVisibleTimer >= notVisibleDuration)
        {
            TriggerAnimatorParameter(sleepParameter, 2);
            sleepTriggered = true;
        }
    }

    private bool IsLookedAt()
    {
        if (playerCamera == null || !IsVisibleToCamera())
        {
            return false;
        }

        Vector3 targetPoint = GetCenterPoint();
        Vector3 toNpc = targetPoint - playerCamera.transform.position;
        float angle = Vector3.Angle(playerCamera.transform.forward, toNpc);

        if (angle > gazeAngle)
        {
            return false;
        }

        if (!requireLineOfSight)
        {
            return true;
        }

        if (Physics.Linecast(
            playerCamera.transform.position,
            targetPoint,
            out RaycastHit hit,
            lineOfSightMask,
            QueryTriggerInteraction.Ignore
        ))
        {
            return hit.transform == transform || hit.transform.IsChildOf(transform);
        }

        return true;
    }

    private bool IsVisibleToCamera()
    {
        if (playerCamera == null)
        {
            return false;
        }

        if (renderers == null || renderers.Length == 0)
        {
            CacheRenderers();
        }

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(playerCamera);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null
                && renderers[i].enabled
                && renderers[i].gameObject.activeInHierarchy
                && GeometryUtility.TestPlanesAABB(planes, renderers[i].bounds))
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetCenterPoint()
    {
        if (renderers == null || renderers.Length == 0)
        {
            CacheRenderers();
        }

        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return hasBounds ? bounds.center : transform.position;
    }

    private void TriggerAnimatorParameter(string parameterName, int boolPulseIndex)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        AnimatorControllerParameter parameter = FindParameter(parameterName);
        if (parameter == null)
        {
            Debug.LogWarning($"[{nameof(RobotNpcBehavior)}] Animator parameter '{parameterName}' tidak ditemukan.", this);
            return;
        }

        if (parameter.type == AnimatorControllerParameterType.Trigger)
        {
            animator.SetTrigger(parameterName);
        }
        else if (parameter.type == AnimatorControllerParameterType.Bool)
        {
            animator.SetBool(parameterName, true);
            boolResetPending[boolPulseIndex] = true;
            boolResetTimes[boolPulseIndex] = Time.time + Mathf.Max(0.01f, boolPulseDuration);
        }
    }

    private AnimatorControllerParameter FindParameter(string parameterName)
    {
        for (int i = 0; i < animator.parameters.Length; i++)
        {
            if (animator.parameters[i].name == parameterName)
            {
                return animator.parameters[i];
            }
        }

        return null;
    }

    private void HandleBoolPulseResets()
    {
        TryResetBoolParameter(happyParameter, 0);
        TryResetBoolParameter(blushedParameter, 1);
        TryResetBoolParameter(sleepParameter, 2);
        TryResetBoolParameter(stopBlushedParameter, 3);
    }

    private void TryResetBoolParameter(string parameterName, int boolPulseIndex)
    {
        if (!boolResetPending[boolPulseIndex] || Time.time < boolResetTimes[boolPulseIndex])
        {
            return;
        }

        boolResetPending[boolPulseIndex] = false;

        if (animator != null && FindParameter(parameterName)?.type == AnimatorControllerParameterType.Bool)
        {
            animator.SetBool(parameterName, false);
        }
    }
}
