using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class WarningUIHandler : MonoBehaviour
{
    private enum WarningType
    {
        None,
        Co2,
        Coverage,
        Temperature
    }

    [Header("Root")]
    public GameObject warningRoot;
    public CanvasGroup warningCanvasGroup;
    public RectTransform warningRect;
    public bool hideOnStart = true;
    public bool deactivateRootWhenHidden;

    [Header("Stats Source")]
    public StatsIndicatorUI statsIndicator;

    [Header("Texts")]
    public TMP_Text currentText;
    public TMP_Text currentValueText;
    public TMP_Text targetValueText;

    [Header("Thresholds")]
    public float co2WarningThreshold = 800f;
    public float minCoverageWarningThreshold = 85f;
    public float maxTemperatureWarningThreshold = 32f;

    [Header("Display")]
    public float displayDuration = 5f;
    public float appearDuration = 0.18f;
    public float disappearDuration = 0.14f;
    public float hiddenScale = 0.88f;
    public float pulseScale = 1.08f;
    public float pulseDuration = 0.32f;
    public float blinkMinAlpha = 0.35f;
    public Ease appearEase = Ease.OutBack;
    public Ease disappearEase = Ease.InCubic;
    public Ease pulseEase = Ease.InOutSine;

    private Vector3 visibleScale = Vector3.one;
    private Sequence warningSequence;
    private Tween pulseTween;
    private Tween blinkTween;
    private bool subscribed;
    private bool wasCo2Exceeded;
    private bool wasCoverageExceeded;
    private bool wasTemperatureExceeded;

    void Awake()
    {
        CacheRoot();
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        TrySubscribe();

        if (hideOnStart)
        {
            HideImmediate();
        }

        if (statsIndicator != null)
        {
            OnStatsChanged(statsIndicator.Snapshot);
        }
    }

    void OnDisable()
    {
        Unsubscribe();
        warningSequence?.Kill();
        pulseTween?.Kill();
        blinkTween?.Kill();
        warningSequence = null;
        pulseTween = null;
        blinkTween = null;
    }

    public void ShowCo2Warning(float currentValue)
    {
        ShowWarning("Current Reading", FormatPpm(currentValue), "< " + FormatPpm(co2WarningThreshold));
    }

    public void ShowCoverageWarning(float currentValue)
    {
        ShowWarning("Integrity", FormatPercent(currentValue), "> " + FormatPercent(minCoverageWarningThreshold));
    }

    public void ShowTemperatureWarning(float currentValue)
    {
        ShowWarning("Current Temp", FormatTemperature(currentValue), "< " + FormatTemperature(maxTemperatureWarningThreshold));
    }

    private void OnStatsChanged(StatsIndicatorUI.StatsSnapshot snapshot)
    {
        WarningType warningType = GetNewestWarningType(snapshot);

        switch (warningType)
        {
            case WarningType.Co2:
                ShowCo2Warning(snapshot.Co2Level);
                break;
            case WarningType.Coverage:
                ShowCoverageWarning(snapshot.Coverage);
                break;
            case WarningType.Temperature:
                ShowTemperatureWarning(snapshot.Temperature);
                break;
        }

        CacheExceededStates(snapshot);
    }

    private WarningType GetNewestWarningType(StatsIndicatorUI.StatsSnapshot snapshot)
    {
        bool co2Exceeded = snapshot.Co2Level > co2WarningThreshold;
        bool coverageExceeded = snapshot.Coverage < minCoverageWarningThreshold;
        bool temperatureExceeded = snapshot.Temperature > maxTemperatureWarningThreshold;
        WarningType warningType = WarningType.None;

        if (co2Exceeded && !wasCo2Exceeded)
        {
            warningType = WarningType.Co2;
        }

        if (coverageExceeded && !wasCoverageExceeded)
        {
            warningType = WarningType.Coverage;
        }

        if (temperatureExceeded && !wasTemperatureExceeded)
        {
            warningType = WarningType.Temperature;
        }

        if (warningType != WarningType.None)
        {
            return warningType;
        }

        if (warningSequence != null)
        {
            return WarningType.None;
        }

        if (temperatureExceeded)
        {
            return WarningType.Temperature;
        }

        if (coverageExceeded)
        {
            return WarningType.Coverage;
        }

        if (co2Exceeded)
        {
            return WarningType.Co2;
        }

        return WarningType.None;
    }

    private void ShowWarning(string label, string currentValue, string targetValue)
    {
        CacheRoot();

        if (currentText != null)
        {
            currentText.text = label;
        }

        if (currentValueText != null)
        {
            currentValueText.text = currentValue;
        }

        if (targetValueText != null)
        {
            targetValueText.text = targetValue;
        }

        warningSequence?.Kill();
        StopPulseBlink();
        warningRoot.SetActive(true);
        SoundManager.PlaySound(SoundType.Warning);
        SoundManager.PlaySound(SoundType.UIPopup);
        warningCanvasGroup.interactable = false;
        warningCanvasGroup.blocksRaycasts = false;
        warningCanvasGroup.alpha = 0f;
        SetScale(visibleScale * hiddenScale);

        float appearTime = Mathf.Max(0.01f, appearDuration);
        float disappearTime = Mathf.Max(0.01f, disappearDuration);
        float holdTime = Mathf.Max(0f, displayDuration - appearTime - disappearTime);

        warningSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(warningRoot)
            .Append(warningCanvasGroup.DOFade(1f, appearTime))
            .Join(GetScaleTween(visibleScale, appearTime))
            .SetEase(appearEase)
            .AppendCallback(StartPulseBlink)
            .AppendInterval(holdTime)
            .AppendCallback(StopPulseBlink)
            .AppendCallback(() => SoundManager.PlaySound(SoundType.UIClose))
            .Append(warningCanvasGroup.DOFade(0f, disappearTime))
            .Join(GetScaleTween(visibleScale * hiddenScale, disappearTime))
            .SetEase(disappearEase)
            .OnComplete(() =>
            {
                warningRoot.SetActive(!deactivateRootWhenHidden);
                warningSequence = null;
            });
    }

    private void StartPulseBlink()
    {
        StopPulseBlink();

        pulseTween = GetScaleTween(visibleScale * pulseScale, Mathf.Max(0.01f, pulseDuration))
            .SetEase(pulseEase)
            .SetLoops(-1, LoopType.Yoyo);

        blinkTween = warningCanvasGroup
            .DOFade(Mathf.Clamp01(blinkMinAlpha), Mathf.Max(0.01f, pulseDuration))
            .SetEase(pulseEase)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopPulseBlink()
    {
        pulseTween?.Kill();
        blinkTween?.Kill();
        pulseTween = null;
        blinkTween = null;
        warningCanvasGroup.alpha = 1f;
        SetScale(visibleScale);
    }

    private void TrySubscribe()
    {
        if (subscribed)
        {
            return;
        }

        if (statsIndicator == null)
        {
            statsIndicator = StatsIndicatorUI.Instance;
        }

        if (statsIndicator == null)
        {
            return;
        }

        statsIndicator.StatsChanged += OnStatsChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || statsIndicator == null)
        {
            return;
        }

        statsIndicator.StatsChanged -= OnStatsChanged;
        subscribed = false;
    }

    private void CacheExceededStates(StatsIndicatorUI.StatsSnapshot snapshot)
    {
        wasCo2Exceeded = snapshot.Co2Level > co2WarningThreshold;
        wasCoverageExceeded = snapshot.Coverage < minCoverageWarningThreshold;
        wasTemperatureExceeded = snapshot.Temperature > maxTemperatureWarningThreshold;
    }

    private void CacheRoot()
    {
        if (warningRoot == null)
        {
            warningRoot = gameObject;
        }

        if (warningCanvasGroup == null)
        {
            warningCanvasGroup = warningRoot.GetComponent<CanvasGroup>();
        }

        if (warningCanvasGroup == null)
        {
            warningCanvasGroup = warningRoot.AddComponent<CanvasGroup>();
        }

        if (warningRect == null)
        {
            warningRect = warningRoot.GetComponent<RectTransform>();
        }

        visibleScale = warningRect != null ? warningRect.localScale : warningRoot.transform.localScale;
    }

    private void HideImmediate()
    {
        CacheRoot();
        warningSequence?.Kill();
        StopPulseBlink();
        warningSequence = null;
        warningRoot.SetActive(!deactivateRootWhenHidden);
        warningCanvasGroup.alpha = 0f;
        warningCanvasGroup.interactable = false;
        warningCanvasGroup.blocksRaycasts = false;
        SetScale(visibleScale * hiddenScale);
    }

    private Tween GetScaleTween(Vector3 targetScale, float duration)
    {
        if (warningRect != null)
        {
            return warningRect.DOScale(targetScale, duration);
        }

        return warningRoot.transform.DOScale(targetScale, duration);
    }

    private void SetScale(Vector3 scale)
    {
        if (warningRect != null)
        {
            warningRect.localScale = scale;
            return;
        }

        warningRoot.transform.localScale = scale;
    }

    private string FormatPpm(float value)
    {
        return Mathf.RoundToInt(value).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".") + " ppm";
    }

    private string FormatPercent(float value)
    {
        return Mathf.RoundToInt(value) + "%";
    }

    private string FormatTemperature(float value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture) + " \u00B0C";
    }
}
