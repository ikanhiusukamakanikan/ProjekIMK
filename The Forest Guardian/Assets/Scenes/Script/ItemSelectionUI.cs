using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

#if DOTWEEN || USE_DOTWEEN
using DG.Tweening;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ItemSelectionUI : MonoBehaviour
{
    public static event Action<ItemSelectionUI> MenuOpened;
    public static event Action<ItemSelectionUI> MenuClosed;
    public static event Action<ItemSelectionUI, GameObject> ItemSpawned;

    [System.Serializable]
    public class ItemOption
    {
        [Header("Button")]
        public GameObject buttonObject;
        public GameObject prefab;

        [Header("UI Parts")]
        public Graphic icon;
        public Graphic iconBackground;
        public Graphic text;
        public Graphic textBackground;

        [Header("Optional")]
        public Transform spawnPointOverride;

        [HideInInspector] public RectTransform iconRect;
        [HideInInspector] public LayoutElement iconLayoutElement;
        [HideInInspector] public RectTransform iconBackgroundRect;
        [HideInInspector] public LayoutElement iconBackgroundLayoutElement;
    }

    [Header("Menu")]
    public GameObject itemSelectionRoot;
    public bool startHidden = true;
    public bool toggleWithInput = true;

    [Header("Menu Animation")]
    public float appearDuration = 0.18f;
    public float disappearDuration = 0.12f;
    public float hiddenMenuScale = 0.92f;

    [Header("Spawn")]
    public Transform spawnPoint;
    public Transform spawnedParent;
    public bool disableGravityUntilGrab = true;

    [Header("Input")]
    public KeyCode keyboardToggleKey = KeyCode.I;
    public bool useLeftControllerXButton = true;
    public GameObject RayInteractor;

#if ENABLE_INPUT_SYSTEM
    public Key inputSystemKeyboardToggleKey = Key.I;
    public InputActionReference toggleAction;
#endif

    [Header("Items")]
    public List<ItemOption> items = new List<ItemOption>(5);

    [Header("Hover Animation")]
    public Color idleColor = new Color32(0xA6, 0xA6, 0xA6, 0xFF);
    public Color hoverColor = Color.white;
    public float hoverScale = 1.1f;
    public float hoverDuration = 0.12f;

    [Header("Click Animation")]
    public float bounceScale = 1.18f;
    public float bounceUpDuration = 0.08f;
    public float bounceDownDuration = 0.1f;

    private readonly List<ItemRuntime> runtimes = new List<ItemRuntime>();
    private readonly List<UnityEngine.XR.InputDevice> leftControllers = new List<UnityEngine.XR.InputDevice>();
    private CanvasGroup itemSelectionCanvasGroup;
    private RectTransform itemSelectionRect;
    private Vector3 visibleMenuScale;
    private GameObject currentSpawnedItem;
#if DOTWEEN || USE_DOTWEEN
    private Tween menuVisibilityTween;
#else
    private Coroutine menuVisibilityRoutine;
#endif
    private bool wasXButtonPressed;
    private bool isClicking;
    private bool isVisible = true;

    void Awake()
    {
        if (itemSelectionRoot == null)
        {
            itemSelectionRoot = gameObject;
        }

        itemSelectionCanvasGroup = itemSelectionRoot.GetComponent<CanvasGroup>();

        if (itemSelectionCanvasGroup == null)
        {
            itemSelectionCanvasGroup = itemSelectionRoot.AddComponent<CanvasGroup>();
        }

        itemSelectionRect = itemSelectionRoot.GetComponent<RectTransform>();
        visibleMenuScale = itemSelectionRoot.transform.localScale;

        runtimes.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            ItemOption item = items[i];

            if (item == null || item.buttonObject == null)
            {
                continue;
            }

            CacheMissingReferences(item);
            ItemRuntime runtime = new ItemRuntime(item);
            runtimes.Add(runtime);

            ApplyIdleState(runtime);
            RegisterEvents(item.buttonObject, i);
        }
    }

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.Enable();
        }
#endif
    }

    void Start()
    {
        SetVisibleImmediate(!startHidden);
    }

    void Update()
    {
        if (!toggleWithInput)
        {
            return;
        }

        if (WasTogglePressed())
        {
            ToggleVisible();
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();

        foreach (ItemRuntime runtime in runtimes)
        {
            KillAnimations(runtime);
        }

        KillMenuAnimation();

#if ENABLE_INPUT_SYSTEM
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.Disable();
        }
#endif
    }

    public void ToggleVisible()
    {
        SetVisible(!isVisible);
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;

        if (!itemSelectionRoot.activeSelf)
        {
            itemSelectionRoot.SetActive(true);
        }

        SetRayInteractorActive(visible);
        SoundManager.PlaySound(visible ? SoundType.UIPopup : SoundType.UIClose);
        PlayMenuVisibilityAnimation(visible);
        NotifyVisibilityChanged(visible);

        if (visible)
        {
            isClicking = false;

            foreach (ItemRuntime runtime in runtimes)
            {
                KillAnimations(runtime);
                ApplyIdleState(runtime);
            }
        }
    }

    private void SetVisibleImmediate(bool visible)
    {
        isVisible = visible;

        if (!itemSelectionRoot.activeSelf)
        {
            itemSelectionRoot.SetActive(true);
        }

        SetRayInteractorActive(visible);
        KillMenuAnimation();
        itemSelectionCanvasGroup.alpha = visible ? 1f : 0f;
        itemSelectionCanvasGroup.interactable = visible;
        itemSelectionCanvasGroup.blocksRaycasts = visible;
        SetMenuScale(visible ? visibleMenuScale : visibleMenuScale * hiddenMenuScale);

        if (visible)
        {
            isClicking = false;

            foreach (ItemRuntime runtime in runtimes)
            {
                KillAnimations(runtime);
                ApplyIdleState(runtime);
            }
        }
    }

    private void PlayMenuVisibilityAnimation(bool visible)
    {
        KillMenuAnimation();

        itemSelectionCanvasGroup.interactable = visible;
        itemSelectionCanvasGroup.blocksRaycasts = visible;

#if DOTWEEN || USE_DOTWEEN
        float duration = Mathf.Max(0.01f, visible ? appearDuration : disappearDuration);
        Vector3 targetScale = visible ? visibleMenuScale : visibleMenuScale * hiddenMenuScale;

        if (visible)
        {
            itemSelectionCanvasGroup.alpha = 0f;
            SetMenuScale(visibleMenuScale * hiddenMenuScale);
        }

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(itemSelectionRoot)
            .Join(itemSelectionCanvasGroup.DOFade(visible ? 1f : 0f, duration));

        if (itemSelectionRect != null)
        {
            sequence.Join(itemSelectionRect.DOScale(targetScale, duration));
        }
        else
        {
            sequence.Join(itemSelectionRoot.transform.DOScale(targetScale, duration));
        }

        sequence.SetEase(visible ? Ease.OutBack : Ease.InCubic);
        menuVisibilityTween = sequence.OnComplete(() => menuVisibilityTween = null);
#else
        menuVisibilityRoutine = StartCoroutine(MenuVisibilityRoutine(visible));
#endif
    }

#if !DOTWEEN && !USE_DOTWEEN
    private IEnumerator MenuVisibilityRoutine(bool visible)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, visible ? appearDuration : disappearDuration);
        float startAlpha = itemSelectionCanvasGroup.alpha;
        float targetAlpha = visible ? 1f : 0f;
        Vector3 startScale = itemSelectionRoot.transform.localScale;
        Vector3 targetScale = visible ? visibleMenuScale : visibleMenuScale * hiddenMenuScale;

        if (visible)
        {
            startAlpha = 0f;
            startScale = visibleMenuScale * hiddenMenuScale;
            itemSelectionCanvasGroup.alpha = startAlpha;
            SetMenuScale(startScale);
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = visible ? EaseOutBack(t) : t * t * t;

            itemSelectionCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            SetMenuScale(Vector3.LerpUnclamped(startScale, targetScale, t));

            yield return null;
        }

        itemSelectionCanvasGroup.alpha = targetAlpha;
        SetMenuScale(targetScale);
        menuVisibilityRoutine = null;
    }
#endif

    private void KillMenuAnimation()
    {
#if DOTWEEN || USE_DOTWEEN
        menuVisibilityTween?.Kill();
        menuVisibilityTween = null;
#else
        if (menuVisibilityRoutine != null)
        {
            StopCoroutine(menuVisibilityRoutine);
            menuVisibilityRoutine = null;
        }
#endif
    }

    private void SetRayInteractorActive(bool active)
    {
        if (RayInteractor != null && RayInteractor.activeSelf != active)
        {
            RayInteractor.SetActive(active);
        }
    }

    private void SetMenuScale(Vector3 scale)
    {
        if (itemSelectionRect != null)
        {
            itemSelectionRect.localScale = scale;
            return;
        }

        itemSelectionRoot.transform.localScale = scale;
    }

    public void SelectItem(int index)
    {
        if (isClicking || index < 0 || index >= items.Count)
        {
            return;
        }

        ItemRuntime runtime = GetRuntime(items[index]);

        if (runtime == null)
        {
            return;
        }

        SoundManager.PlaySound(SoundType.Click);

#if DOTWEEN || USE_DOTWEEN
        PlaySelectTween(index, runtime);
#else
        StartCoroutine(SelectRoutine(index, runtime));
#endif
    }

#if DOTWEEN || USE_DOTWEEN
    private void PlaySelectTween(int index, ItemRuntime runtime)
    {
        isClicking = true;

        SpawnItem(items[index]);
        KillAnimations(runtime);

        runtime.BounceTween = CreateBounceTween(runtime)
            .OnComplete(() =>
            {
                runtime.BounceTween = null;
                SetVisible(false);
                isClicking = false;
            });
    }
#else
    private IEnumerator SelectRoutine(int index, ItemRuntime runtime)
    {
        isClicking = true;

        SpawnItem(items[index]);

        if (runtime.HoverRoutine != null)
        {
            StopCoroutine(runtime.HoverRoutine);
            runtime.HoverRoutine = null;
        }

        if (runtime.BounceRoutine != null)
        {
            StopCoroutine(runtime.BounceRoutine);
        }

        runtime.BounceRoutine = StartCoroutine(BounceRoutine(runtime));
        yield return runtime.BounceRoutine;
        runtime.BounceRoutine = null;

        SetVisible(false);
        isClicking = false;
    }
#endif

    private void SpawnItem(ItemOption item)
    {
        if (item.prefab == null)
        {
            Debug.LogWarning($"[{nameof(ItemSelectionUI)}] Prefab belum diisi untuk {item.buttonObject.name}.", this);
            return;
        }

        Transform targetSpawnPoint = item.spawnPointOverride != null ? item.spawnPointOverride : spawnPoint;

        if (targetSpawnPoint == null)
        {
            Debug.LogWarning($"[{nameof(ItemSelectionUI)}] Spawn point belum diisi.", this);
            return;
        }

        if (currentSpawnedItem != null)
        {
            Destroy(currentSpawnedItem);
        }

        GameObject spawnedItem = Instantiate(
            item.prefab,
            targetSpawnPoint.position,
            targetSpawnPoint.rotation
        );

        currentSpawnedItem = spawnedItem;
        SoundManager.PlaySound(SoundType.ItemSummon);
        AddPickupSoundForSpawnedItem(spawnedItem);

        if (spawnedParent != null)
        {
            spawnedItem.transform.SetParent(spawnedParent, true);
        }

        if (disableGravityUntilGrab)
        {
            DisableGravityUntilGrab(spawnedItem);
        }

        ItemSpawned?.Invoke(this, spawnedItem);
    }

    private void NotifyVisibilityChanged(bool visible)
    {
        if (visible)
        {
            MenuOpened?.Invoke(this);
        }
        else
        {
            MenuClosed?.Invoke(this);
        }
    }

    private void DisableGravityUntilGrab(GameObject spawnedItem)
    {
        Rigidbody[] rigidbodies = spawnedItem.GetComponentsInChildren<Rigidbody>();

        if (rigidbodies.Length == 0)
        {
            return;
        }

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].useGravity = false;
            rigidbodies[i].linearVelocity = Vector3.zero;
            rigidbodies[i].angularVelocity = Vector3.zero;
        }

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable[] grabInteractables =
            spawnedItem.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        if (grabInteractables.Length == 0)
        {
            Debug.LogWarning(
                $"[{nameof(ItemSelectionUI)}] {spawnedItem.name} punya Rigidbody, tapi XRGrabInteractable tidak ditemukan.",
                spawnedItem
            );

            return;
        }

        for (int i = 0; i < grabInteractables.Length; i++)
        {
            SpawnedGrabGravityActivator activator =
                grabInteractables[i].GetComponent<SpawnedGrabGravityActivator>();

            if (activator == null)
            {
                activator = grabInteractables[i].gameObject.AddComponent<SpawnedGrabGravityActivator>();
            }

            activator.Setup(grabInteractables[i], rigidbodies);
        }
    }

    private void AddPickupSoundForSpawnedItem(GameObject spawnedItem)
    {
        if (spawnedItem == null)
        {
            return;
        }

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable[] grabInteractables =
            spawnedItem.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true);

        for (int i = 0; i < grabInteractables.Length; i++)
        {
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable = grabInteractables[i];
            if (grabInteractable == null || HasBuiltInPickupSound(grabInteractable.gameObject))
            {
                continue;
            }

            XRGrabPickupSound pickupSound = grabInteractable.GetComponent<XRGrabPickupSound>();
            if (pickupSound == null)
            {
                pickupSound = grabInteractable.gameObject.AddComponent<XRGrabPickupSound>();
            }

            pickupSound.grabInteractable = grabInteractable;
        }
    }

    private bool HasBuiltInPickupSound(GameObject target)
    {
        return target.GetComponentInParent<TreeScanner>() != null
            || target.GetComponentInParent<AxeHit>() != null
            || target.GetComponentInParent<VRShovelDig>() != null
            || target.GetComponentInParent<FireHoseSpray>() != null
            || target.GetComponentInChildren<TreeScanner>(true) != null
            || target.GetComponentInChildren<AxeHit>(true) != null
            || target.GetComponentInChildren<VRShovelDig>(true) != null
            || target.GetComponentInChildren<FireHoseSpray>(true) != null;
    }

    private void RegisterEvents(GameObject buttonObject, int index)
    {
        EventTrigger eventTrigger = buttonObject.GetComponent<EventTrigger>();

        if (eventTrigger == null)
        {
            eventTrigger = buttonObject.AddComponent<EventTrigger>();
        }

        AddEvent(eventTrigger, EventTriggerType.PointerEnter, () => OnHoverChanged(index, true));
        AddEvent(eventTrigger, EventTriggerType.PointerExit, () => OnHoverChanged(index, false));
        AddEvent(eventTrigger, EventTriggerType.PointerClick, () => SelectItem(index));

        Button button = buttonObject.GetComponent<Button>();

        if (button != null)
        {
            button.onClick.AddListener(() => SelectItem(index));
        }
    }

    private void AddEvent(EventTrigger eventTrigger, EventTriggerType eventType, UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };

        entry.callback.AddListener(_ => action.Invoke());
        eventTrigger.triggers.Add(entry);
    }

    private void OnHoverChanged(int index, bool isHovered)
    {
        if (isClicking || index < 0 || index >= items.Count)
        {
            return;
        }

        ItemRuntime runtime = GetRuntime(items[index]);

        if (runtime == null)
        {
            return;
        }

        if (isHovered)
        {
            SoundManager.PlaySound(SoundType.Hover);
        }

#if DOTWEEN || USE_DOTWEEN
        PlayHoverTween(runtime, isHovered);
#else
        if (runtime.HoverRoutine != null)
        {
            StopCoroutine(runtime.HoverRoutine);
        }

        runtime.HoverRoutine = StartCoroutine(HoverRoutine(runtime, isHovered));
#endif
    }

#if DOTWEEN || USE_DOTWEEN
    private void PlayHoverTween(ItemRuntime runtime, bool isHovered)
    {
        runtime.HoverTween?.Kill();

        Color targetColor = isHovered ? hoverColor : idleColor;
        float targetTextBackgroundAlpha = isHovered ? 1f : 0f;
        Vector2 iconTargetSize = runtime.IconBaseSize * (isHovered ? hoverScale : 1f);
        Vector2 iconBackgroundTargetSize = runtime.IconBackgroundBaseSize * (isHovered ? hoverScale : 1f);

        runtime.HoverTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(runtime.Item.buttonObject);

        if (runtime.Item.icon != null)
        {
            runtime.HoverTween.Join(runtime.Item.icon.DOColor(targetColor, hoverDuration));
        }

        if (runtime.Item.text != null)
        {
            runtime.HoverTween.Join(runtime.Item.text.DOColor(targetColor, hoverDuration));
        }

        if (runtime.Item.textBackground != null)
        {
            runtime.HoverTween.Join(runtime.Item.textBackground.DOFade(targetTextBackgroundAlpha, hoverDuration));
        }

        runtime.HoverTween
            .Join(CreateSizeTween(runtime.IconSizeTarget, iconTargetSize, hoverDuration, Ease.OutCubic))
            .Join(CreateSizeTween(runtime.IconBackgroundSizeTarget, iconBackgroundTargetSize, hoverDuration, Ease.OutCubic))
            .OnComplete(() => runtime.HoverTween = null);
    }

    private Sequence CreateBounceTween(ItemRuntime runtime)
    {
        Vector2 iconCurrentSize = GetCurrentSize(runtime.IconSizeTarget);
        Vector2 iconBackgroundCurrentSize = GetCurrentSize(runtime.IconBackgroundSizeTarget);
        Vector2 iconBounceSize = runtime.IconBaseSize * bounceScale;
        Vector2 iconBackgroundBounceSize = runtime.IconBackgroundBaseSize * bounceScale;

        return DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(runtime.Item.buttonObject)
            .Append(CreateSizeTween(runtime.IconSizeTarget, iconBounceSize, bounceUpDuration, Ease.OutBack, iconCurrentSize))
            .Join(CreateSizeTween(runtime.IconBackgroundSizeTarget, iconBackgroundBounceSize, bounceUpDuration, Ease.OutBack, iconBackgroundCurrentSize))
            .Append(CreateSizeTween(runtime.IconSizeTarget, runtime.IconBaseSize, bounceDownDuration, Ease.OutCubic, iconBounceSize))
            .Join(CreateSizeTween(runtime.IconBackgroundSizeTarget, runtime.IconBackgroundBaseSize, bounceDownDuration, Ease.OutCubic, iconBackgroundBounceSize));
    }

    private Tween CreateSizeTween(SizeTarget target, Vector2 endSize, float duration, Ease ease)
    {
        return CreateSizeTween(target, endSize, duration, ease, GetCurrentSize(target));
    }

    private Tween CreateSizeTween(SizeTarget target, Vector2 endSize, float duration, Ease ease, Vector2 startSize)
    {
        return DOTween
            .To(() => startSize, size => SetSize(target, size), endSize, Mathf.Max(0.01f, duration))
            .SetEase(ease);
    }
#else
    private IEnumerator HoverRoutine(ItemRuntime runtime, bool isHovered)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, hoverDuration);

        Color iconStart = runtime.Item.icon != null ? runtime.Item.icon.color : idleColor;
        Color textStart = runtime.Item.text != null ? runtime.Item.text.color : idleColor;
        Color backgroundTextStart = runtime.Item.textBackground != null ? runtime.Item.textBackground.color : Color.white;

        Color targetColor = isHovered ? hoverColor : idleColor;
        float targetTextBackgroundAlpha = isHovered ? 1f : 0f;

        Vector2 iconStartSize = GetCurrentSize(runtime.IconSizeTarget);
        Vector2 iconBackgroundStartSize = GetCurrentSize(runtime.IconBackgroundSizeTarget);
        Vector2 iconTargetSize = runtime.IconBaseSize * (isHovered ? hoverScale : 1f);
        Vector2 iconBackgroundTargetSize = runtime.IconBackgroundBaseSize * (isHovered ? hoverScale : 1f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = EaseOutCubic(t);

            SetGraphicColor(runtime.Item.icon, Color.Lerp(iconStart, targetColor, t));
            SetGraphicColor(runtime.Item.text, Color.Lerp(textStart, targetColor, t));

            if (runtime.Item.textBackground != null)
            {
                Color color = backgroundTextStart;
                color.a = Mathf.Lerp(backgroundTextStart.a, targetTextBackgroundAlpha, t);
                runtime.Item.textBackground.color = color;
            }

            SetSize(runtime.IconSizeTarget, Vector2.Lerp(iconStartSize, iconTargetSize, t));
            SetSize(runtime.IconBackgroundSizeTarget, Vector2.Lerp(iconBackgroundStartSize, iconBackgroundTargetSize, t));

            yield return null;
        }

        SetGraphicColor(runtime.Item.icon, targetColor);
        SetGraphicColor(runtime.Item.text, targetColor);
        SetGraphicAlpha(runtime.Item.textBackground, targetTextBackgroundAlpha);
        SetSize(runtime.IconSizeTarget, iconTargetSize);
        SetSize(runtime.IconBackgroundSizeTarget, iconBackgroundTargetSize);

        runtime.HoverRoutine = null;
    }

    private IEnumerator BounceRoutine(ItemRuntime runtime)
    {
        Vector2 iconCurrentSize = GetCurrentSize(runtime.IconSizeTarget);
        Vector2 iconBackgroundCurrentSize = GetCurrentSize(runtime.IconBackgroundSizeTarget);
        Vector2 iconBounceSize = runtime.IconBaseSize * bounceScale;
        Vector2 iconBackgroundBounceSize = runtime.IconBackgroundBaseSize * bounceScale;

        yield return AnimateSizes(
            runtime,
            iconCurrentSize,
            iconBounceSize,
            iconBackgroundCurrentSize,
            iconBackgroundBounceSize,
            bounceUpDuration
        );

        yield return AnimateSizes(
            runtime,
            iconBounceSize,
            runtime.IconBaseSize,
            iconBackgroundBounceSize,
            runtime.IconBackgroundBaseSize,
            bounceDownDuration
        );
    }

    private IEnumerator AnimateSizes(
        ItemRuntime runtime,
        Vector2 iconFrom,
        Vector2 iconTo,
        Vector2 iconBackgroundFrom,
        Vector2 iconBackgroundTo,
        float duration
    )
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = EaseOutBack(t);

            SetSize(runtime.IconSizeTarget, Vector2.LerpUnclamped(iconFrom, iconTo, t));
            SetSize(runtime.IconBackgroundSizeTarget, Vector2.LerpUnclamped(iconBackgroundFrom, iconBackgroundTo, t));

            yield return null;
        }

        SetSize(runtime.IconSizeTarget, iconTo);
        SetSize(runtime.IconBackgroundSizeTarget, iconBackgroundTo);
    }
#endif

    private bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleAction != null && toggleAction.action != null && toggleAction.action.WasPressedThisFrame())
        {
            return true;
        }

        if (
            Keyboard.current != null &&
            inputSystemKeyboardToggleKey != Key.None &&
            Keyboard.current[inputSystemKeyboardToggleKey].wasPressedThisFrame
        )
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(keyboardToggleKey))
        {
            return true;
        }
#endif

        return useLeftControllerXButton && WasLeftControllerXPressedThisFrame();
    }

    private bool WasLeftControllerXPressedThisFrame()
    {
        bool isPressed = false;

        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
            leftControllers
        );

        for (int i = 0; i < leftControllers.Count; i++)
        {
            if (leftControllers[i].TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool primaryPressed) && primaryPressed)
            {
                isPressed = true;
                break;
            }
        }

        bool pressedThisFrame = isPressed && !wasXButtonPressed;
        wasXButtonPressed = isPressed;

        return pressedThisFrame;
    }

    private void CacheMissingReferences(ItemOption item)
    {
        if (item.icon != null && item.iconRect == null)
        {
            item.iconRect = item.icon.rectTransform;
        }

        if (item.iconRect != null && item.iconLayoutElement == null)
        {
            item.iconLayoutElement = item.iconRect.GetComponent<LayoutElement>();
        }

        if (item.iconBackground != null && item.iconBackgroundRect == null)
        {
            item.iconBackgroundRect = item.iconBackground.rectTransform;
        }

        if (item.iconBackgroundRect != null && item.iconBackgroundLayoutElement == null)
        {
            item.iconBackgroundLayoutElement = item.iconBackgroundRect.GetComponent<LayoutElement>();
        }
    }

    private void ApplyIdleState(ItemRuntime runtime)
    {
        SetGraphicColor(runtime.Item.icon, idleColor);
        SetGraphicColor(runtime.Item.text, idleColor);
        SetGraphicAlpha(runtime.Item.textBackground, 0f);
        SetSize(runtime.IconSizeTarget, runtime.IconBaseSize);
        SetSize(runtime.IconBackgroundSizeTarget, runtime.IconBackgroundBaseSize);
    }

    private void KillAnimations(ItemRuntime runtime)
    {
#if DOTWEEN || USE_DOTWEEN
        runtime.HoverTween?.Kill();
        runtime.BounceTween?.Kill();
        runtime.HoverTween = null;
        runtime.BounceTween = null;
#else
        if (runtime.HoverRoutine != null)
        {
            StopCoroutine(runtime.HoverRoutine);
            runtime.HoverRoutine = null;
        }

        if (runtime.BounceRoutine != null)
        {
            StopCoroutine(runtime.BounceRoutine);
            runtime.BounceRoutine = null;
        }
#endif
    }

    private ItemRuntime GetRuntime(ItemOption item)
    {
        for (int i = 0; i < runtimes.Count; i++)
        {
            if (runtimes[i].Item == item)
            {
                return runtimes[i];
            }
        }

        return null;
    }

    private static void SetGraphicColor(Graphic graphic, Color color)
    {
        if (graphic != null)
        {
            graphic.color = color;
        }
    }

    private static void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
        {
            return;
        }

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private static Vector2 GetCurrentSize(SizeTarget target)
    {
        if (target.LayoutElement != null && target.LayoutElement.preferredWidth > 0f && target.LayoutElement.preferredHeight > 0f)
        {
            return new Vector2(target.LayoutElement.preferredWidth, target.LayoutElement.preferredHeight);
        }

        if (target.RectTransform != null)
        {
            return target.RectTransform.rect.size;
        }

        return Vector2.zero;
    }

    private static void SetSize(SizeTarget target, Vector2 size)
    {
        if (target.LayoutElement != null)
        {
            target.LayoutElement.preferredWidth = size.x;
            target.LayoutElement.preferredHeight = size.y;
        }

        if (target.RectTransform != null)
        {
            target.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            target.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }
    }

    private static float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;
        float value = t - 1f;
        return 1f + value * value * ((overshoot + 1f) * value + overshoot);
    }

    private class ItemRuntime
    {
        public readonly ItemOption Item;
        public readonly Vector2 IconBaseSize;
        public readonly Vector2 IconBackgroundBaseSize;
        public readonly SizeTarget IconSizeTarget;
        public readonly SizeTarget IconBackgroundSizeTarget;
#if DOTWEEN || USE_DOTWEEN
        public Sequence HoverTween;
        public Sequence BounceTween;
#else
        public Coroutine HoverRoutine;
        public Coroutine BounceRoutine;
#endif

        public ItemRuntime(ItemOption item)
        {
            Item = item;
            IconSizeTarget = new SizeTarget(item.iconRect, item.iconLayoutElement);
            IconBackgroundSizeTarget = new SizeTarget(item.iconBackgroundRect, item.iconBackgroundLayoutElement);
            IconBaseSize = GetCurrentSize(IconSizeTarget);
            IconBackgroundBaseSize = GetCurrentSize(IconBackgroundSizeTarget);
        }
    }

    private readonly struct SizeTarget
    {
        public readonly RectTransform RectTransform;
        public readonly LayoutElement LayoutElement;

        public SizeTarget(RectTransform rectTransform, LayoutElement layoutElement)
        {
            RectTransform = rectTransform;
            LayoutElement = layoutElement;
        }
    }
}

public class SpawnedGrabGravityActivator : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private Rigidbody[] rigidbodies;
    private Coroutine enableGravityRoutine;
    private bool hasActivatedGravity;

    public void Setup(
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable targetGrabInteractable,
        Rigidbody[] targetRigidbodies
    )
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }

        grabInteractable = targetGrabInteractable;
        rigidbodies = targetRigidbodies;
        hasActivatedGravity = false;

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);
        }
    }

    private void OnDestroy()
    {
        if (enableGravityRoutine != null)
        {
            StopCoroutine(enableGravityRoutine);
            enableGravityRoutine = null;
        }

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (hasActivatedGravity || rigidbodies == null)
        {
            return;
        }

        hasActivatedGravity = true;
        EnableGravity();

        if (enableGravityRoutine != null)
        {
            StopCoroutine(enableGravityRoutine);
        }

        enableGravityRoutine = StartCoroutine(EnableGravityAfterXRGrabRoutine());
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (hasActivatedGravity)
        {
            EnableGravity();
        }
    }

    private IEnumerator EnableGravityAfterXRGrabRoutine()
    {
        yield return null;
        EnableGravity();

        yield return new WaitForFixedUpdate();
        EnableGravity();

        enableGravityRoutine = null;
    }

    private void EnableGravity()
    {
        if (rigidbodies == null)
        {
            return;
        }

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] != null)
            {
                rigidbodies[i].useGravity = true;
            }
        }
    }
}
