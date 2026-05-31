using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("References")]
    public MainMenuManager mainMenuManager;
    public AudioManager audioManager;
    public GameObject settingsPanel;

    [Header("Master Volume")]
    public Slider masterVolumeSlider;
    public TMP_Text masterVolumeValueText;
    public int defaultVolumeStep = 4;

    [Header("Buttons")]
    public Button backButton;
    public GameObject backButtonObject;
    public GameObject backHoverObject;

    [Header("Back Button Animation")]
    public float hoverFadeDuration = 0.16f;
    [Range(0f, 1f)] public float hoverVisibleAlpha = 1f;
    [Range(0f, 1f)] public float hoverHiddenAlpha = 0f;
    public Ease hoverEase = Ease.OutCubic;
    public float clickScale = 0.88f;
    public float clickShrinkDuration = 0.08f;
    public float clickReturnDuration = 0.14f;
    public Color hoverClickDarkColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    public Ease clickShrinkEase = Ease.InCubic;
    public Ease clickReturnEase = Ease.OutBack;

    private readonly List<Graphic> backHoverGraphics = new();
    private readonly List<Renderer> backHoverRenderers = new();
    private readonly List<Color> backGraphicBaseColors = new();
    private readonly List<Color> backRendererBaseColors = new();
    private CanvasGroup backHoverCanvasGroup;
    private Vector3 backButtonBaseScale = Vector3.one;
    private Tween backHoverTween;
    private Sequence backClickTween;

    void Awake()
    {
        SetupBackButtonAnimation();
        SetupSlider();
        RegisterButtons();
    }

    void OnEnable()
    {
        SyncSliderWithAudioManager();
        RefreshValueText();
    }

    void OnDestroy()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(PlayBackButtonClick);
        }

        backHoverTween?.Kill();
        backClickTween?.Kill();
    }

    public void CloseSettings()
    {
        if (mainMenuManager != null)
        {
            mainMenuManager.CloseSettings();
            return;
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void SetupSlider()
    {
        if (masterVolumeSlider == null)
        {
            return;
        }

        masterVolumeSlider.minValue = 0f;
        masterVolumeSlider.maxValue = 4f;
        masterVolumeSlider.wholeNumbers = true;

        int savedStep = PlayerPrefs.GetInt(
            AudioManager.MasterVolumePrefKey,
            Mathf.Clamp(defaultVolumeStep, 0, 4)
        );

        if (audioManager == null)
        {
            audioManager = AudioManager.Instance;
        }

        int step = audioManager != null ? audioManager.MasterVolumeStep : Mathf.Clamp(savedStep, 0, 4);
        masterVolumeSlider.SetValueWithoutNotify(step);
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        RefreshValueText();
    }

    private void RegisterButtons()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(PlayBackButtonClick);
        }
    }

    private void SyncSliderWithAudioManager()
    {
        if (masterVolumeSlider == null)
        {
            return;
        }

        if (audioManager == null)
        {
            audioManager = AudioManager.Instance;
        }

        if (audioManager != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(audioManager.MasterVolumeStep);
        }
    }

    public void HandleBackButtonHovered(bool isHovered)
    {
        if (backHoverObject == null)
        {
            return;
        }

        backHoverTween?.Kill();
        backHoverObject.SetActive(true);

        float targetAlpha = isHovered ? hoverVisibleAlpha : hoverHiddenAlpha;
        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(backHoverObject);

        if (backHoverCanvasGroup != null)
        {
            sequence.Join(backHoverCanvasGroup.DOFade(targetAlpha, Mathf.Max(0.01f, hoverFadeDuration)));
        }

        for (int i = 0; i < backHoverGraphics.Count; i++)
        {
            Color targetColor = backGraphicBaseColors[i];
            targetColor.a = targetAlpha;
            sequence.Join(backHoverGraphics[i].DOColor(targetColor, Mathf.Max(0.01f, hoverFadeDuration)));
        }

        for (int i = 0; i < backHoverRenderers.Count; i++)
        {
            Color targetColor = backRendererBaseColors[i];
            targetColor.a = targetAlpha;
            sequence.Join(backHoverRenderers[i].material.DOColor(targetColor, Mathf.Max(0.01f, hoverFadeDuration)));
        }

        backHoverTween = sequence
            .SetEase(hoverEase)
            .OnComplete(() =>
            {
                if (!isHovered)
                {
                    backHoverObject.SetActive(false);
                }

                backHoverTween = null;
            });
    }

    public void PlayBackButtonClick()
    {
        if (backButtonObject == null)
        {
            CloseSettings();
            return;
        }

        backClickTween?.Kill();
        backHoverTween?.Kill();

        if (backHoverObject != null)
        {
            backHoverObject.SetActive(true);
        }

        backClickTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(backButtonObject)
            .Append(backButtonObject.transform.DOScale(backButtonBaseScale * clickScale, Mathf.Max(0.01f, clickShrinkDuration)).SetEase(clickShrinkEase))
            .Join(CreateBackHoverDarkTween())
            .Append(backButtonObject.transform.DOScale(backButtonBaseScale, Mathf.Max(0.01f, clickReturnDuration)).SetEase(clickReturnEase))
            .OnComplete(() =>
            {
                backClickTween = null;
                CloseSettings();
            });
    }

    private void SetupBackButtonAnimation()
    {
        if (backButtonObject == null && backButton != null)
        {
            backButtonObject = backButton.gameObject;
        }

        if (backButton == null && backButtonObject != null)
        {
            backButton = backButtonObject.GetComponent<Button>();
        }

        if (backButtonObject == null)
        {
            return;
        }

        backButtonBaseScale = backButtonObject.transform.localScale;

        SettingsBackButtonTarget target = backButtonObject.GetComponent<SettingsBackButtonTarget>();
        if (target == null)
        {
            target = backButtonObject.AddComponent<SettingsBackButtonTarget>();
        }

        target.Setup(this, backButton);

        if (backButton != null && backButton.gameObject != backButtonObject)
        {
            SettingsBackButtonTarget buttonTarget = backButton.gameObject.GetComponent<SettingsBackButtonTarget>();
            if (buttonTarget == null)
            {
                buttonTarget = backButton.gameObject.AddComponent<SettingsBackButtonTarget>();
            }

            buttonTarget.Setup(this, backButton);
        }

        CacheBackHoverVisuals();
        HideBackHoverImmediate();
    }

    private void CacheBackHoverVisuals()
    {
        if (backHoverObject == null)
        {
            return;
        }

        backHoverCanvasGroup = backHoverObject.GetComponent<CanvasGroup>();
        backHoverGraphics.Clear();
        backHoverRenderers.Clear();
        backGraphicBaseColors.Clear();
        backRendererBaseColors.Clear();

        backHoverGraphics.AddRange(backHoverObject.GetComponentsInChildren<Graphic>(true));
        backHoverRenderers.AddRange(backHoverObject.GetComponentsInChildren<Renderer>(true));

        for (int i = 0; i < backHoverGraphics.Count; i++)
        {
            backGraphicBaseColors.Add(backHoverGraphics[i].color);
        }

        for (int i = 0; i < backHoverRenderers.Count; i++)
        {
            backRendererBaseColors.Add(backHoverRenderers[i].material.color);
        }
    }

    private void HideBackHoverImmediate()
    {
        if (backHoverObject == null)
        {
            return;
        }

        if (backHoverCanvasGroup != null)
        {
            backHoverCanvasGroup.alpha = hoverHiddenAlpha;
        }

        for (int i = 0; i < backHoverGraphics.Count; i++)
        {
            Color color = backGraphicBaseColors[i];
            color.a = hoverHiddenAlpha;
            backHoverGraphics[i].color = color;
        }

        for (int i = 0; i < backHoverRenderers.Count; i++)
        {
            Color color = backRendererBaseColors[i];
            color.a = hoverHiddenAlpha;
            backHoverRenderers[i].material.color = color;
        }

        backHoverObject.SetActive(false);
    }

    private Tween CreateBackHoverDarkTween()
    {
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        float duration = Mathf.Max(0.01f, clickShrinkDuration);

        if (backHoverCanvasGroup != null)
        {
            sequence.Join(backHoverCanvasGroup.DOFade(hoverVisibleAlpha, duration));
        }

        for (int i = 0; i < backHoverGraphics.Count; i++)
        {
            sequence.Join(backHoverGraphics[i].DOColor(hoverClickDarkColor, duration));
        }

        for (int i = 0; i < backHoverRenderers.Count; i++)
        {
            sequence.Join(backHoverRenderers[i].material.DOColor(hoverClickDarkColor, duration));
        }

        return sequence;
    }

    private void OnMasterVolumeChanged(float value)
    {
        float snappedStep = Mathf.Round(value);
        masterVolumeSlider.SetValueWithoutNotify(snappedStep);

        if (audioManager == null)
        {
            audioManager = AudioManager.Instance;
        }

        if (audioManager != null)
        {
            audioManager.SetMasterVolumeStep(snappedStep);
        }
        else
        {
            int volumeStep = Mathf.RoundToInt(snappedStep);
            AudioListener.volume = Mathf.Clamp01(volumeStep / 4f);
            PlayerPrefs.SetInt(AudioManager.MasterVolumePrefKey, volumeStep);
            PlayerPrefs.Save();
        }

        RefreshValueText();
    }

    private void RefreshValueText()
    {
        if (masterVolumeValueText == null || masterVolumeSlider == null)
        {
            return;
        }

        int percent = Mathf.RoundToInt((masterVolumeSlider.value / 4f) * 100f);
        masterVolumeValueText.text = percent + "%";
    }
}

public class SettingsBackButtonTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private SettingsManager settingsManager;
    private Button uiButton;

    public void Setup(SettingsManager settingsManager, Button uiButton)
    {
        this.settingsManager = settingsManager;
        this.uiButton = uiButton != null ? uiButton : GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        settingsManager?.HandleBackButtonHovered(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        settingsManager?.HandleBackButtonHovered(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (uiButton != null)
        {
            return;
        }

        settingsManager?.PlayBackButtonClick();
    }

    void OnMouseEnter()
    {
        settingsManager?.HandleBackButtonHovered(true);
    }

    void OnMouseExit()
    {
        settingsManager?.HandleBackButtonHovered(false);
    }

    void OnMouseDown()
    {
        if (uiButton != null)
        {
            return;
        }

        settingsManager?.PlayBackButtonClick();
    }
}
