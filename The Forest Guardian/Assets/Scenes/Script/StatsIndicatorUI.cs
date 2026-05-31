using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class StatsIndicatorUI : MonoBehaviour
{
    public static StatsIndicatorUI Instance { get; private set; }

    public enum DamageStatus
    {
        None,
        Low,
        High,
        Critical
    }

    public readonly struct StatsSnapshot
    {
        public readonly float Coverage;
        public readonly float Co2Level;
        public readonly float DamagePercent;
        public readonly float Temperature;
        public readonly DamageStatus DamageStatus;

        public StatsSnapshot(
            float coverage,
            float co2Level,
            float damagePercent,
            float temperature,
            DamageStatus damageStatus
        )
        {
            Coverage = coverage;
            Co2Level = co2Level;
            DamagePercent = damagePercent;
            Temperature = temperature;
            DamageStatus = damageStatus;
        }
    }

    [Header("Root")]
    public GameObject indicatorRoot;
    public bool startHidden = true;
    public bool toggleWithInput = true;

    [Header("Input")]
    public KeyCode keyboardToggleKey = KeyCode.B;
    public bool useLeftControllerYButton = true;
    public bool alsoUseRightControllerSecondaryButton;

    [Header("Texts")]
    public TMP_Text coverageValueText;
    public TMP_Text co2ValueText;
    public TMP_Text damageStatusText;
    public TMP_Text temperatureValueText;
    public string temperatureSuffix = "\u00B0";

    [Header("Damage")]
    public Slider damageSlider;
    public float lowDamageMax = 30f;
    public float highDamageMax = 60f;

    [Header("Initial Healthy Range")]
    public bool randomizeHealthyStatsOnStart = true;
    public Vector2 coverageHealthyRange = new Vector2(70f, 80f);
    public Vector2 co2HealthyRange = new Vector2(900f, 1000f);
    public Vector2 temperatureHealthyRange = new Vector2(24f, 27f);
    public float initialDamagePercent = 0f;

    [Header("Current Values")]
    [Range(0f, 100f)] public float coverage = 75f;
    public float co2Level = 950f;
    [Range(0f, 100f)] public float damagePercent;
    public float temperature = 25f;

    [Header("Value Limits")]
    public Vector2 coverageLimits = new Vector2(0f, 100f);
    public Vector2 co2Limits = new Vector2(250f, 2500f);
    public Vector2 damageLimits = new Vector2(0f, 100f);
    public Vector2 temperatureLimits = new Vector2(-20f, 80f);

    [Header("Healthy Target")]
    public float minHealthyCoverage = 70f;
    public float maxHealthyCo2Level = 1000f;
    public float maxHealthyDamagePercent = 30f;
    public float maxHealthyTemperature = 27f;

    [Header("Danger Lose Threshold")]
    public float dangerCoverage = 5f;
    public float dangerCo2Level = 1800f;
    public float dangerDamagePercent = 100f;
    public float dangerTemperature = 45f;

    [Header("Popup Animation")]
    public float appearDuration = 0.22f;
    public float disappearDuration = 0.14f;
    public float hiddenScale = 0.88f;
    public Ease appearEase = Ease.OutBack;
    public Ease disappearEase = Ease.InCubic;

    [Header("Value Animation")]
    public bool animateValueChanges = true;
    public float valueTweenDuration = 0.35f;
    public Ease valueTweenEase = Ease.OutCubic;

    [Header("Realtime Display Fluctuation")]
    public bool fluctuateDisplayedValues = true;
    public float fluctuationRetargetInterval = 0.45f;
    public float fluctuationSmoothSpeed = 6f;
    public Vector2 coverageDisplayFluctuation = new Vector2(-1.25f, 1.25f);
    public Vector2 co2DisplayFluctuation = new Vector2(-18f, 18f);
    public Vector2 damageDisplayFluctuation = new Vector2(-0.8f, 0.8f);
    public Vector2 temperatureDisplayFluctuation = new Vector2(-0.75f, 0.75f);

    public event Action<StatsSnapshot> StatsChanged;

    private readonly List<InputDevice> controllers = new();
    private CanvasGroup canvasGroup;
    private RectTransform rootRect;
    private Vector3 visibleScale;
    private Tween visibilityTween;
    private Tween valueTween;
    private bool wasControllerYPressed;
    private bool isVisible = true;
    private float displayedCoverage;
    private float displayedCo2Level;
    private float displayedDamagePercent;
    private float displayedTemperature;
    private float targetDisplayedCoverage;
    private float targetDisplayedCo2Level;
    private float targetDisplayedDamagePercent;
    private float targetDisplayedTemperature;
    private float nextFluctuationRetargetTime;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (indicatorRoot == null)
        {
            indicatorRoot = gameObject;
        }

        canvasGroup = indicatorRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = indicatorRoot.AddComponent<CanvasGroup>();
        }

        rootRect = indicatorRoot.GetComponent<RectTransform>();
        visibleScale = indicatorRoot.transform.localScale;
    }

    void Start()
    {
        if (randomizeHealthyStatsOnStart)
        {
            ResetToRandomHealthyStats(false);
        }
        else
        {
            SetStats(coverage, co2Level, damagePercent, temperature, false);
        }

        SetVisibleImmediate(!startHidden);
    }

    void Update()
    {
        if (toggleWithInput && WasTogglePressed())
        {
            ToggleVisible();
        }

        UpdateRealtimeDisplayValues();
    }

    void OnDestroy()
    {
        visibilityTween?.Kill();
        valueTween?.Kill();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public StatsSnapshot Snapshot => new StatsSnapshot(
        coverage,
        co2Level,
        damagePercent,
        temperature,
        GetDamageStatus(damagePercent)
    );

    public bool IsHealthy => coverage >= minHealthyCoverage
        && co2Level <= maxHealthyCo2Level
        && damagePercent <= maxHealthyDamagePercent
        && temperature <= maxHealthyTemperature;

    public bool IsDanger => coverage <= dangerCoverage
        || co2Level >= dangerCo2Level
        || damagePercent >= dangerDamagePercent
        || temperature >= dangerTemperature;

    public void ToggleVisible()
    {
        SetVisible(!isVisible);
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;

        if (!indicatorRoot.activeSelf)
        {
            indicatorRoot.SetActive(true);
        }

        visibilityTween?.Kill();

        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;

        float duration = Mathf.Max(0.01f, visible ? appearDuration : disappearDuration);
        Vector3 targetScale = visible ? visibleScale : visibleScale * hiddenScale;

        if (visible)
        {
            canvasGroup.alpha = 0f;
            SetRootScale(visibleScale * hiddenScale);
        }

        visibilityTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(indicatorRoot)
            .Join(canvasGroup.DOFade(visible ? 1f : 0f, duration))
            .Join(GetScaleTween(targetScale, duration))
            .SetEase(visible ? appearEase : disappearEase)
            .OnComplete(() => visibilityTween = null);
    }

    public void SetVisibleImmediate(bool visible)
    {
        isVisible = visible;

        if (!indicatorRoot.activeSelf)
        {
            indicatorRoot.SetActive(true);
        }

        visibilityTween?.Kill();
        visibilityTween = null;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        SetRootScale(visible ? visibleScale : visibleScale * hiddenScale);
    }

    public void ResetToRandomHealthyStats(bool animate = true)
    {
        SetStats(
            UnityEngine.Random.Range(coverageHealthyRange.x, coverageHealthyRange.y),
            UnityEngine.Random.Range(co2HealthyRange.x, co2HealthyRange.y),
            initialDamagePercent,
            UnityEngine.Random.Range(temperatureHealthyRange.x, temperatureHealthyRange.y),
            animate
        );
    }

    public void ApplyDelta(
        float coverageDelta,
        float co2Delta,
        float damageDelta,
        float temperatureDelta,
        bool animate = true
    )
    {
        SetStats(
            coverage + coverageDelta,
            co2Level + co2Delta,
            damagePercent + damageDelta,
            temperature + temperatureDelta,
            animate
        );
    }

    public void SetStats(
        float newCoverage,
        float newCo2Level,
        float newDamagePercent,
        float newTemperature,
        bool animate = true
    )
    {
        float targetCoverage = Mathf.Clamp(newCoverage, coverageLimits.x, coverageLimits.y);
        float targetCo2 = Mathf.Clamp(newCo2Level, co2Limits.x, co2Limits.y);
        float targetDamage = Mathf.Clamp(newDamagePercent, damageLimits.x, damageLimits.y);
        float targetTemperature = Mathf.Clamp(newTemperature, temperatureLimits.x, temperatureLimits.y);

        valueTween?.Kill();

        if (!animateValueChanges || !animate || !Application.isPlaying)
        {
            coverage = targetCoverage;
            co2Level = targetCo2;
            damagePercent = targetDamage;
            temperature = targetTemperature;
            SnapDisplayedValuesToStats();
            RetargetDisplayedValues();
            RefreshUI();
            RaiseStatsChanged();
            return;
        }

        float startCoverage = coverage;
        float startCo2 = co2Level;
        float startDamage = damagePercent;
        float startTemperature = temperature;

        valueTween = DOTween.To(
                () => 0f,
                t =>
                {
                    coverage = Mathf.Lerp(startCoverage, targetCoverage, t);
                    co2Level = Mathf.Lerp(startCo2, targetCo2, t);
                    damagePercent = Mathf.Lerp(startDamage, targetDamage, t);
                    temperature = Mathf.Lerp(startTemperature, targetTemperature, t);
                    RefreshUI();
                },
                1f,
                Mathf.Max(0.01f, valueTweenDuration)
            )
            .SetEase(valueTweenEase)
            .OnComplete(() =>
            {
                coverage = targetCoverage;
                co2Level = targetCo2;
                damagePercent = targetDamage;
                temperature = targetTemperature;
                RetargetDisplayedValues();
                RefreshUI();
                RaiseStatsChanged();
                valueTween = null;
            });
    }

    public DamageStatus GetDamageStatus(float value)
    {
        if (value <= 0f)
        {
            return DamageStatus.None;
        }

        if (value <= lowDamageMax)
        {
            return DamageStatus.Low;
        }

        if (value <= highDamageMax)
        {
            return DamageStatus.High;
        }

        return DamageStatus.Critical;
    }

    private void RefreshUI()
    {
        float coverageToShow = fluctuateDisplayedValues ? displayedCoverage : coverage;
        float co2ToShow = fluctuateDisplayedValues ? displayedCo2Level : co2Level;
        float damageToShow = fluctuateDisplayedValues ? displayedDamagePercent : damagePercent;
        float temperatureToShow = fluctuateDisplayedValues ? displayedTemperature : temperature;

        if (coverageValueText != null)
        {
            coverageValueText.text = Mathf.RoundToInt(coverageToShow).ToString();
        }

        if (co2ValueText != null)
        {
            co2ValueText.text = Mathf.RoundToInt(co2ToShow).ToString();
        }

        if (damageSlider != null)
        {
            damageSlider.value = damageSlider.maxValue <= 1f
                ? Mathf.Clamp01(damageToShow / 100f)
                : damageToShow;
        }

        if (damageStatusText != null)
        {
            damageStatusText.text = DamageStatusToText(GetDamageStatus(damageToShow));
        }

        if (temperatureValueText != null)
        {
            temperatureValueText.text = Mathf.RoundToInt(temperatureToShow) + temperatureSuffix;
        }
    }

    private void UpdateRealtimeDisplayValues()
    {
        if (!fluctuateDisplayedValues)
        {
            SnapDisplayedValuesToStats();
            RefreshUI();
            return;
        }

        if (Time.unscaledTime >= nextFluctuationRetargetTime)
        {
            RetargetDisplayedValues();
        }

        float lerpT = 1f - Mathf.Exp(-Mathf.Max(0.01f, fluctuationSmoothSpeed) * Time.unscaledDeltaTime);
        displayedCoverage = Mathf.Lerp(displayedCoverage, targetDisplayedCoverage, lerpT);
        displayedCo2Level = Mathf.Lerp(displayedCo2Level, targetDisplayedCo2Level, lerpT);
        displayedDamagePercent = Mathf.Lerp(displayedDamagePercent, targetDisplayedDamagePercent, lerpT);
        displayedTemperature = Mathf.Lerp(displayedTemperature, targetDisplayedTemperature, lerpT);

        RefreshUI();
    }

    private void SnapDisplayedValuesToStats()
    {
        displayedCoverage = coverage;
        displayedCo2Level = co2Level;
        displayedDamagePercent = damagePercent;
        displayedTemperature = temperature;
        targetDisplayedCoverage = displayedCoverage;
        targetDisplayedCo2Level = displayedCo2Level;
        targetDisplayedDamagePercent = displayedDamagePercent;
        targetDisplayedTemperature = displayedTemperature;
    }

    private void RetargetDisplayedValues()
    {
        nextFluctuationRetargetTime = Time.unscaledTime + Mathf.Max(0.05f, fluctuationRetargetInterval);

        targetDisplayedCoverage = ClampWithLimits(
            coverage + UnityEngine.Random.Range(coverageDisplayFluctuation.x, coverageDisplayFluctuation.y),
            coverageLimits
        );

        targetDisplayedCo2Level = ClampWithLimits(
            co2Level + UnityEngine.Random.Range(co2DisplayFluctuation.x, co2DisplayFluctuation.y),
            co2Limits
        );

        targetDisplayedDamagePercent = damagePercent <= 0f
            ? 0f
            : ClampWithLimits(
                damagePercent + UnityEngine.Random.Range(damageDisplayFluctuation.x, damageDisplayFluctuation.y),
                damageLimits
            );

        targetDisplayedTemperature = ClampWithLimits(
            temperature + UnityEngine.Random.Range(temperatureDisplayFluctuation.x, temperatureDisplayFluctuation.y),
            temperatureLimits
        );
    }

    private float ClampWithLimits(float value, Vector2 limits)
    {
        return Mathf.Clamp(value, limits.x, limits.y);
    }

    private void RaiseStatsChanged()
    {
        StatsChanged?.Invoke(Snapshot);
    }

    private string DamageStatusToText(DamageStatus status)
    {
        switch (status)
        {
            case DamageStatus.Low:
                return "low";
            case DamageStatus.High:
                return "high";
            case DamageStatus.Critical:
                return "critical";
            default:
                return "none";
        }
    }

    private bool WasTogglePressed()
    {
        bool keyboardPressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        keyboardPressed = Input.GetKeyDown(keyboardToggleKey);
#endif

        return keyboardPressed || WasControllerYPressedThisFrame();
    }

    private bool WasControllerYPressedThisFrame()
    {
        if (!useLeftControllerYButton && !alsoUseRightControllerSecondaryButton)
        {
            return false;
        }

        bool isPressed = false;

        if (useLeftControllerYButton)
        {
            isPressed |= IsSecondaryButtonPressed(InputDeviceCharacteristics.Left);
        }

        if (alsoUseRightControllerSecondaryButton)
        {
            isPressed |= IsSecondaryButtonPressed(InputDeviceCharacteristics.Right);
        }

        bool pressedThisFrame = isPressed && !wasControllerYPressed;
        wasControllerYPressed = isPressed;

        return pressedThisFrame;
    }

    private bool IsSecondaryButtonPressed(InputDeviceCharacteristics hand)
    {
        controllers.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            hand | InputDeviceCharacteristics.Controller,
            controllers
        );

        for (int i = 0; i < controllers.Count; i++)
        {
            if (controllers[i].TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed) && pressed)
            {
                return true;
            }
        }

        return false;
    }

    private Tween GetScaleTween(Vector3 targetScale, float duration)
    {
        if (rootRect != null)
        {
            return rootRect.DOScale(targetScale, duration);
        }

        return indicatorRoot.transform.DOScale(targetScale, duration);
    }

    private void SetRootScale(Vector3 scale)
    {
        if (rootRect != null)
        {
            rootRect.localScale = scale;
            return;
        }

        indicatorRoot.transform.localScale = scale;
    }
}
