using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class LoseUIHandler : MonoBehaviour, IEndScreenButtonOwner
{
    private const string MainMenuAction = "main_menu";
    private const string RetryAction = "retry";
    private const string QuitAction = "quit"; 

    [Header("Root")]
    public GameObject loseRoot;
    public CanvasGroup loseCanvasGroup;
    public RectTransform loseRect;
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
    public EndScreenButton retryButton;
    public EndScreenButton quitButton;

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
        RegisterButton(RetryAction, retryButton);
        RegisterButton(QuitAction, quitButton);
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
        switch (actionId)
        {
            case MainMenuAction:
                ReturnToMainMenu();
                break;
            case RetryAction:
                RetryCurrentScene();
                break;
            case QuitAction:
                QuitGame();
                break;
        }
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning($"[{nameof(LoseUIHandler)}] Main Menu Scene Name belum diisi.", this);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void RetryCurrentScene()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
        if (loseRoot == null)
        {
            loseRoot = gameObject;
        }

        if (loseCanvasGroup == null)
        {
            loseCanvasGroup = loseRoot.GetComponent<CanvasGroup>();
        }

        if (loseCanvasGroup == null)
        {
            loseCanvasGroup = loseRoot.AddComponent<CanvasGroup>();
        }

        if (loseRect == null)
        {
            loseRect = loseRoot.GetComponent<RectTransform>();
        }

        visibleScale = loseRect != null ? loseRect.localScale : loseRoot.transform.localScale;
    }

    private void SetVisible(bool visible)
    {
        CacheRoot();
        visibilityTween?.Kill();
        loseRoot.SetActive(true);
        SetRayInteractorActive(visible);
        loseCanvasGroup.interactable = visible;
        loseCanvasGroup.blocksRaycasts = visible;

        if (visible)
        {
            SoundManager.PlaySound(SoundType.Lose);
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
            loseCanvasGroup.alpha = 0f;
            SetScale(visibleScale * hiddenScale);
        }

        visibilityTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(loseRoot)
            .Append(loseCanvasGroup.DOFade(visible ? 1f : 0f, duration))
            .Join(GetScaleTween(targetScale, duration))
            .SetEase(visible ? appearEase : disappearEase)
            .OnComplete(() =>
            {
                loseCanvasGroup.interactable = visible;
                loseCanvasGroup.blocksRaycasts = visible;
                loseRoot.SetActive(visible || !deactivateRootWhenHidden);
                visibilityTween = null;
            });
    }

    private void SetVisibleImmediate(bool visible)
    {
        CacheRoot();
        visibilityTween?.Kill();
        visibilityTween = null;
        loseRoot.SetActive(visible || !deactivateRootWhenHidden);
        SetRayInteractorActive(visible);
        loseCanvasGroup.alpha = visible ? 1f : 0f;
        loseCanvasGroup.interactable = visible;
        loseCanvasGroup.blocksRaycasts = visible;
        SetScale(visible ? visibleScale : visibleScale * hiddenScale);
    }

    private Tween GetScaleTween(Vector3 targetScale, float duration)
    {
        if (loseRect != null)
        {
            return loseRect.DOScale(targetScale, duration);
        }

        return loseRoot.transform.DOScale(targetScale, duration);
    }

    private void SetScale(Vector3 scale)
    {
        if (loseRect != null)
        {
            loseRect.localScale = scale;
            return;
        }

        loseRoot.transform.localScale = scale;
    }

    private void SetRayInteractorActive(bool active)
    {
        if (RayInteractor != null && RayInteractor.activeSelf != active)
        {
            RayInteractor.SetActive(active);
        }
    }
}
