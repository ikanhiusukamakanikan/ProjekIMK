using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WinUIHandler : MonoBehaviour, IEndScreenButtonOwner
{
    private const string MainMenuAction = "main_menu";

    [Header("Root")]
    public GameObject winRoot;
    public CanvasGroup winCanvasGroup;
    public RectTransform winRect;
    public bool hideOnStart = true;
    public bool deactivateRootWhenHidden;
    public GameObject RayInteractor;

    [Header("Stats Source")]
    public StatsIndicatorUI statsIndicator;

    [Header("Stats Texts")]
    public TMP_Text damageText;
    public TMP_Text co2Text;
    public TMP_Text coverageText;
    public TMP_Text temperatureText;

    [Header("Buttons")]
    public EndScreenButton mainMenuButton;

    [Header("Scene")]
    public string mainMenuSceneName = "MainMenuScene";

    [Header("Panel Animation")]
    public float appearDuration = 0.24f;
    public float disappearDuration = 0.14f;
    public float hiddenScale = 0.9f;
    public Ease appearEase = Ease.OutBack;
    public Ease disappearEase = Ease.InCubic;

    [Header("Hover Animation")]
    public float hoverFadeDuration = 0.16f;
    [Range(0f, 1f)] public float hoverVisibleAlpha = 1f;
    [Range(0f, 1f)] public float hoverHiddenAlpha = 0f;
    public Ease hoverEase = Ease.OutCubic;

    [Header("Click Animation")]
    public float clickScale = 0.88f;
    public float clickShrinkDuration = 0.08f;
    public float clickReturnDuration = 0.14f;
    public Color hoverClickDarkColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    public Ease clickShrinkEase = Ease.InCubic;
    public Ease clickReturnEase = Ease.OutBack;

    private readonly Dictionary<string, EndScreenButtonRuntime> runtimes = new();
    private Vector3 visibleScale = Vector3.one;
    private Sequence visibilityTween;

    void Awake()
    {
        CacheRoot();
        RegisterButton(MainMenuAction, mainMenuButton);
    }

    void Start()
    {
        if (hideOnStart)
        {
            SetVisibleImmediate(false);
        }
    }

    void OnDestroy()
    {
        visibilityTween?.Kill();

        foreach (EndScreenButtonRuntime runtime in runtimes.Values)
        {
            runtime.Kill();
        }
    }

    public void Show()
    {
        if (statsIndicator == null)
        {
            statsIndicator = StatsIndicatorUI.Instance;
        }

        if (statsIndicator != null)
        {
            Show(statsIndicator.Snapshot);
            return;
        }

        SetVisible(true);
    }

    public void Show(StatsIndicatorUI.StatsSnapshot snapshot)
    {
        SetStatsText(snapshot);
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public void HandleEndScreenButtonHovered(string actionId, bool isHovered)
    {
        if (runtimes.TryGetValue(actionId, out EndScreenButtonRuntime runtime))
        {
            runtime.PlayHover(isHovered, hoverFadeDuration, hoverEase);
        }
    }

    public void HandleEndScreenButtonClicked(string actionId)
    {
        if (!runtimes.TryGetValue(actionId, out EndScreenButtonRuntime runtime))
        {
            ExecuteAction(actionId);
            return;
        }

        runtime.PlayClick(
            clickScale,
            clickShrinkDuration,
            clickReturnDuration,
            clickShrinkEase,
            clickReturnEase,
            hoverClickDarkColor,
            () => ExecuteAction(actionId)
        );
    }

    private void ExecuteAction(string actionId)
    {
        if (actionId == MainMenuAction)
        {
            ReturnToMainMenu();
        }
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning($"[{nameof(WinUIHandler)}] Main Menu Scene Name belum diisi.", this);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void SetStatsText(StatsIndicatorUI.StatsSnapshot snapshot)
    {
        if (damageText != null)
        {
            damageText.text = Mathf.RoundToInt(snapshot.DamagePercent) + "%";
        }

        if (co2Text != null)
        {
            co2Text.text = Mathf.RoundToInt(snapshot.Co2Level) + "ppm";
        }

        if (coverageText != null)
        {
            coverageText.text = Mathf.RoundToInt(snapshot.Coverage) + "%";
        }

        if (temperatureText != null)
        {
            temperatureText.text = Mathf.RoundToInt(snapshot.Temperature) + "\u00B0C";
        }
    }

    private void RegisterButton(string actionId, EndScreenButton button)
    {
        if (button == null || button.buttonObject == null)
        {
            return;
        }

        EndScreenButtonRuntime runtime = new EndScreenButtonRuntime(button, hoverVisibleAlpha, hoverHiddenAlpha);
        runtimes[actionId] = runtime;
        runtime.HideHoverImmediate();

        EndScreenButtonTarget target = button.buttonObject.GetComponent<EndScreenButtonTarget>();
        if (target == null)
        {
            target = button.buttonObject.AddComponent<EndScreenButtonTarget>();
        }

        target.Setup(this, actionId);

        Button uiButton = button.buttonObject.GetComponent<Button>();
        if (uiButton != null)
        {
            uiButton.onClick.AddListener(() => HandleEndScreenButtonClicked(actionId));
        }
    }

    private void CacheRoot()
    {
        if (winRoot == null)
        {
            winRoot = gameObject;
        }

        if (winCanvasGroup == null)
        {
            winCanvasGroup = winRoot.GetComponent<CanvasGroup>();
        }

        if (winCanvasGroup == null)
        {
            winCanvasGroup = winRoot.AddComponent<CanvasGroup>();
        }

        if (winRect == null)
        {
            winRect = winRoot.GetComponent<RectTransform>();
        }

        visibleScale = winRect != null ? winRect.localScale : winRoot.transform.localScale;
    }

    private void SetVisible(bool visible)
    {
        CacheRoot();
        visibilityTween?.Kill();
        winRoot.SetActive(true);
        SetRayInteractorActive(visible);
        winCanvasGroup.interactable = visible;
        winCanvasGroup.blocksRaycasts = visible;

        if (visible)
        {
            SoundManager.PlaySound(SoundType.Win);
            SoundManager.PlaySound(SoundType.UIPopup);
        }
        else
        {
            SoundManager.PlaySound(SoundType.UIClose);
        }

        float duration = Mathf.Max(0.01f, visible ? appearDuration : disappearDuration);
        Vector3 targetScale = visible ? visibleScale : visibleScale * hiddenScale;

        if (visible)
        {
            winCanvasGroup.alpha = 0f;
            SetScale(visibleScale * hiddenScale);
        }

        visibilityTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(winRoot)
            .Append(winCanvasGroup.DOFade(visible ? 1f : 0f, duration))
            .Join(GetScaleTween(targetScale, duration))
            .SetEase(visible ? appearEase : disappearEase)
            .OnComplete(() =>
            {
                winCanvasGroup.interactable = visible;
                winCanvasGroup.blocksRaycasts = visible;
                winRoot.SetActive(visible || !deactivateRootWhenHidden);
                visibilityTween = null;
            });
    }

    private void SetVisibleImmediate(bool visible)
    {
        CacheRoot();
        visibilityTween?.Kill();
        visibilityTween = null;
        winRoot.SetActive(visible || !deactivateRootWhenHidden);
        SetRayInteractorActive(visible);
        winCanvasGroup.alpha = visible ? 1f : 0f;
        winCanvasGroup.interactable = visible;
        winCanvasGroup.blocksRaycasts = visible;
        SetScale(visible ? visibleScale : visibleScale * hiddenScale);
    }

    private Tween GetScaleTween(Vector3 targetScale, float duration)
    {
        if (winRect != null)
        {
            return winRect.DOScale(targetScale, duration);
        }

        return winRoot.transform.DOScale(targetScale, duration);
    }

    private void SetScale(Vector3 scale)
    {
        if (winRect != null)
        {
            winRect.localScale = scale;
            return;
        }

        winRoot.transform.localScale = scale;
    }

    private void SetRayInteractorActive(bool active)
    {
        if (RayInteractor != null && RayInteractor.activeSelf != active)
        {
            RayInteractor.SetActive(active);
        }
    }
}
