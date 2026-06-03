using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PauseMenuManager : MonoBehaviour
{
    public enum PauseAction
    {
        Resume,
        Settings,
        ReturnToMainMenu,
        Quit
    }

    public enum VrControllerHand
    {
        Left,
        Right,
        Both
    }

    public enum VrControllerButton
    {
        Menu,
        Primary,
        Secondary,
        Trigger,
        Grip
    }

    [System.Serializable]
    public class PauseButton
    {
        public GameObject buttonObject;
        public GameObject hoverObject;
    }

    [Header("Buttons")]
    public PauseButton resumeButton;
    public PauseButton settingsButton;
    public PauseButton returnToMainMenuButton;
    public PauseButton quitButton;

    [Header("Pause Panel")]
    public GameObject pauseMenuPanel;
    public CanvasGroup pauseMenuCanvasGroup;
    public bool hidePauseMenuOnStart = true;
    public KeyCode pauseKey = KeyCode.Escape;
    public GameObject RayInteractor;

    [Header("VR Controller Pause")]
    public bool enableVrControllerPause = true;
    public VrControllerHand vrControllerHand = VrControllerHand.Left;
    public VrControllerButton vrPauseButton = VrControllerButton.Menu;

    [Header("Scene")]
    public string mainMenuSceneName = "MainMenuScene";

    [Header("Settings Panel")]
    public GameObject settingsPanel;
    public CanvasGroup settingsCanvasGroup;
    public bool hideSettingsOnStart = true;

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

    [Header("Panel Animation")]
    public float panelFadeDuration = 0.18f;
    public float panelHiddenScale = 0.94f;
    public Ease panelEase = Ease.OutCubic;

    private readonly Dictionary<GameObject, ButtonRuntime> runtimes = new();
    private Tween pausePanelTween;
    private Tween settingsTween;
    private Vector3 pausePanelVisibleScale = Vector3.one;
    private Vector3 settingsVisibleScale = Vector3.one;
    private bool isPaused;
    private bool settingsOpen;
    private bool vrPauseButtonWasPressed;

    void Awake()
    {
        RegisterButton(resumeButton, PauseAction.Resume);
        RegisterButton(settingsButton, PauseAction.Settings);
        RegisterButton(returnToMainMenuButton, PauseAction.ReturnToMainMenu);
        RegisterButton(quitButton, PauseAction.Quit);
        CachePausePanel();
        CacheSettingsPanel();
    }

    void Start()
    {
        if (hidePauseMenuOnStart)
        {
            SetPauseMenuImmediate(false);
            Time.timeScale = 1f;
            isPaused = false;
        }

        if (hideSettingsOnStart)
        {
            SetSettingsPanelImmediate(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            TogglePause();
        }

        HandleVrPauseInput();
    }

    void OnDestroy()
    {
        pausePanelTween?.Kill();
        settingsTween?.Kill();

        foreach (ButtonRuntime runtime in runtimes.Values)
        {
            runtime.Kill();
        }

        if (isPaused)
        {
            Time.timeScale = 1f;
        }
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        SetPauseMenu(true);
        SetCursorForPause(true);
    }

    public void ResumeGame()
    {
        SetSettingsPanel(false);
        isPaused = false;
        Time.timeScale = 1f;
        SetPauseMenu(false);
        SetCursorForPause(false);
    }

    public void OpenSettings()
    {
        SetSettingsPanel(true);
    }

    public void CloseSettings()
    {
        SetSettingsPanel(false);
    }

    public void ToggleSettings()
    {
        SetSettingsPanel(!settingsOpen);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning($"[{nameof(PauseMenuManager)}] Main Menu Scene Name belum diisi.", this);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RegisterButton(PauseButton pauseButton, PauseAction action)
    {
        if (pauseButton == null || pauseButton.buttonObject == null)
        {
            return;
        }

        ButtonRuntime runtime = new ButtonRuntime(pauseButton, hoverVisibleAlpha, hoverHiddenAlpha);
        runtimes[pauseButton.buttonObject] = runtime;
        runtime.HideHoverImmediate();

        PauseMenuButtonTarget target = pauseButton.buttonObject.GetComponent<PauseMenuButtonTarget>();
        if (target == null)
        {
            target = pauseButton.buttonObject.AddComponent<PauseMenuButtonTarget>();
        }

        target.Setup(this, action);

        Button uiButton = pauseButton.buttonObject.GetComponent<Button>();
        if (uiButton != null)
        {
            uiButton.onClick.AddListener(() => HandleButtonClicked(action));
        }
    }

    public void HandleButtonHovered(PauseAction action, bool isHovered)
    {
        if (isHovered)
        {
            SoundManager.PlaySound(SoundType.Hover);
        }

        ButtonRuntime runtime = GetRuntime(action);
        if (runtime == null)
        {
            return;
        }

        runtime.PlayHover(isHovered, hoverFadeDuration, hoverEase);
    }

    public void HandleButtonClicked(PauseAction action)
    {
        SoundManager.PlaySound(SoundType.Click);

        ButtonRuntime runtime = GetRuntime(action);
        if (runtime == null)
        {
            ExecuteAction(action);
            return;
        }

        runtime.PlayClick(
            clickScale,
            clickShrinkDuration,
            clickReturnDuration,
            clickShrinkEase,
            clickReturnEase,
            hoverClickDarkColor,
            () => ExecuteAction(action)
        );
    }

    private void ExecuteAction(PauseAction action)
    {
        switch (action)
        {
            case PauseAction.Resume:
                ResumeGame();
                break;
            case PauseAction.Settings:
                OpenSettings();
                break;
            case PauseAction.ReturnToMainMenu:
                ReturnToMainMenu();
                break;
            case PauseAction.Quit:
                QuitGame();
                break;
        }
    }

    private ButtonRuntime GetRuntime(PauseAction action)
    {
        PauseButton pauseButton = GetButton(action);
        if (pauseButton == null || pauseButton.buttonObject == null)
        {
            return null;
        }

        return runtimes.TryGetValue(pauseButton.buttonObject, out ButtonRuntime runtime)
            ? runtime
            : null;
    }

    private PauseButton GetButton(PauseAction action)
    {
        switch (action)
        {
            case PauseAction.Resume:
                return resumeButton;
            case PauseAction.Settings:
                return settingsButton;
            case PauseAction.ReturnToMainMenu:
                return returnToMainMenuButton;
            case PauseAction.Quit:
                return quitButton;
            default:
                return null;
        }
    }

    private void CachePausePanel()
    {
        if (pauseMenuPanel == null)
        {
            return;
        }

        if (pauseMenuCanvasGroup == null)
        {
            pauseMenuCanvasGroup = pauseMenuPanel.GetComponent<CanvasGroup>();
        }

        if (pauseMenuCanvasGroup == null)
        {
            pauseMenuCanvasGroup = pauseMenuPanel.AddComponent<CanvasGroup>();
        }

        pausePanelVisibleScale = pauseMenuPanel.transform.localScale;
    }

    private void SetPauseMenu(bool open)
    {
        CachePausePanel();

        if (pauseMenuPanel == null || pauseMenuCanvasGroup == null)
        {
            return;
        }

        pausePanelTween?.Kill();
        pauseMenuPanel.SetActive(true);
        SetRayInteractorActive(open);
        pauseMenuCanvasGroup.interactable = open;
        pauseMenuCanvasGroup.blocksRaycasts = open;

        SoundManager.PlaySound(open ? SoundType.UIPopup : SoundType.UIClose);

        if (open)
        {
            pauseMenuCanvasGroup.alpha = 0f;
            pauseMenuPanel.transform.localScale = pausePanelVisibleScale * panelHiddenScale;
        }

        pausePanelTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(pauseMenuPanel)
            .Append(pauseMenuCanvasGroup.DOFade(open ? 1f : 0f, Mathf.Max(0.01f, panelFadeDuration)))
            .Join(pauseMenuPanel.transform.DOScale(open ? pausePanelVisibleScale : pausePanelVisibleScale * panelHiddenScale, Mathf.Max(0.01f, panelFadeDuration)))
            .SetEase(panelEase)
            .OnComplete(() =>
            {
                pauseMenuCanvasGroup.interactable = open;
                pauseMenuCanvasGroup.blocksRaycasts = open;
                pauseMenuPanel.SetActive(open);
                pausePanelTween = null;
            });
    }

    private void SetPauseMenuImmediate(bool open)
    {
        CachePausePanel();

        if (pauseMenuPanel == null || pauseMenuCanvasGroup == null)
        {
            return;
        }

        pausePanelTween?.Kill();
        pausePanelTween = null;
        pauseMenuPanel.SetActive(open);
        SetRayInteractorActive(open);
        pauseMenuCanvasGroup.alpha = open ? 1f : 0f;
        pauseMenuCanvasGroup.interactable = open;
        pauseMenuCanvasGroup.blocksRaycasts = open;
        pauseMenuPanel.transform.localScale = open ? pausePanelVisibleScale : pausePanelVisibleScale * panelHiddenScale;
    }

    private void CacheSettingsPanel()
    {
        if (settingsPanel == null)
        {
            return;
        }

        if (settingsCanvasGroup == null)
        {
            settingsCanvasGroup = settingsPanel.GetComponent<CanvasGroup>();
        }

        if (settingsCanvasGroup == null)
        {
            settingsCanvasGroup = settingsPanel.AddComponent<CanvasGroup>();
        }

        settingsVisibleScale = settingsPanel.transform.localScale;
    }

    private void SetSettingsPanel(bool open)
    {
        CacheSettingsPanel();

        if (settingsPanel == null || settingsCanvasGroup == null)
        {
            return;
        }

        settingsOpen = open;
        settingsTween?.Kill();
        settingsPanel.SetActive(true);
        settingsCanvasGroup.interactable = open;
        settingsCanvasGroup.blocksRaycasts = open;

        if (open)
        {
            settingsCanvasGroup.alpha = 0f;
            settingsPanel.transform.localScale = settingsVisibleScale * panelHiddenScale;
        }

        settingsTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(settingsPanel)
            .Append(settingsCanvasGroup.DOFade(open ? 1f : 0f, Mathf.Max(0.01f, panelFadeDuration)))
            .Join(settingsPanel.transform.DOScale(open ? settingsVisibleScale : settingsVisibleScale * panelHiddenScale, Mathf.Max(0.01f, panelFadeDuration)))
            .SetEase(panelEase)
            .OnComplete(() =>
            {
                settingsCanvasGroup.interactable = open;
                settingsCanvasGroup.blocksRaycasts = open;
                settingsPanel.SetActive(open);
                settingsTween = null;
            });
    }

    private void SetSettingsPanelImmediate(bool open)
    {
        CacheSettingsPanel();

        if (settingsPanel == null || settingsCanvasGroup == null)
        {
            return;
        }

        settingsOpen = open;
        settingsTween?.Kill();
        settingsTween = null;
        settingsPanel.SetActive(open);
        settingsCanvasGroup.alpha = open ? 1f : 0f;
        settingsCanvasGroup.interactable = open;
        settingsCanvasGroup.blocksRaycasts = open;
        settingsPanel.transform.localScale = open ? settingsVisibleScale : settingsVisibleScale * panelHiddenScale;
    }

    private void SetCursorForPause(bool paused)
    {
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    private void SetRayInteractorActive(bool active)
    {
        if (RayInteractor != null && RayInteractor.activeSelf != active)
        {
            RayInteractor.SetActive(active);
        }
    }

    private void HandleVrPauseInput()
    {
        if (!enableVrControllerPause)
        {
            vrPauseButtonWasPressed = false;
            return;
        }

        bool isPressed = IsVrPauseButtonPressed();
        if (isPressed && !vrPauseButtonWasPressed)
        {
            TogglePause();
        }

        vrPauseButtonWasPressed = isPressed;
    }

    private bool IsVrPauseButtonPressed()
    {
        switch (vrControllerHand)
        {
            case VrControllerHand.Left:
                return IsVrButtonPressed(XRNode.LeftHand);
            case VrControllerHand.Right:
                return IsVrButtonPressed(XRNode.RightHand);
            case VrControllerHand.Both:
                return IsVrButtonPressed(XRNode.LeftHand) || IsVrButtonPressed(XRNode.RightHand);
            default:
                return false;
        }
    }

    private bool IsVrButtonPressed(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
        {
            return false;
        }

        InputFeatureUsage<bool> buttonUsage = GetVrButtonUsage();
        return device.TryGetFeatureValue(buttonUsage, out bool pressed) && pressed;
    }

    private InputFeatureUsage<bool> GetVrButtonUsage()
    {
        switch (vrPauseButton)
        {
            case VrControllerButton.Primary:
                return CommonUsages.primaryButton;
            case VrControllerButton.Secondary:
                return CommonUsages.secondaryButton;
            case VrControllerButton.Trigger:
                return CommonUsages.triggerButton;
            case VrControllerButton.Grip:
                return CommonUsages.gripButton;
            case VrControllerButton.Menu:
            default:
                return CommonUsages.menuButton;
        }
    }

    private class ButtonRuntime
    {
        private readonly PauseButton button;
        private readonly List<Graphic> hoverGraphics = new();
        private readonly List<Renderer> hoverRenderers = new();
        private readonly List<Color> graphicBaseColors = new();
        private readonly List<Color> rendererBaseColors = new();
        private readonly float visibleAlpha;
        private readonly float hiddenAlpha;
        private readonly Vector3 baseScale;
        private CanvasGroup hoverCanvasGroup;
        private Sequence hoverTween;
        private Sequence clickTween;

        public ButtonRuntime(PauseButton button, float visibleAlpha, float hiddenAlpha)
        {
            this.button = button;
            this.visibleAlpha = visibleAlpha;
            this.hiddenAlpha = hiddenAlpha;
            baseScale = button.buttonObject.transform.localScale;
            CacheHoverVisuals();
        }

        public void PlayHover(bool isHovered, float duration, Ease ease)
        {
            if (button.hoverObject == null)
            {
                return;
            }

            hoverTween?.Kill();
            button.hoverObject.SetActive(true);

            float targetAlpha = isHovered ? visibleAlpha : hiddenAlpha;
            hoverTween = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(button.hoverObject);

            if (hoverCanvasGroup != null)
            {
                hoverTween.Join(hoverCanvasGroup.DOFade(targetAlpha, Mathf.Max(0.01f, duration)));
            }

            for (int i = 0; i < hoverGraphics.Count; i++)
            {
                Color targetColor = graphicBaseColors[i];
                targetColor.a = targetAlpha;
                hoverTween.Join(hoverGraphics[i].DOColor(targetColor, Mathf.Max(0.01f, duration)));
            }

            for (int i = 0; i < hoverRenderers.Count; i++)
            {
                Color targetColor = rendererBaseColors[i];
                targetColor.a = targetAlpha;
                hoverTween.Join(hoverRenderers[i].material.DOColor(targetColor, Mathf.Max(0.01f, duration)));
            }

            hoverTween
                .SetEase(ease)
                .OnComplete(() =>
                {
                    if (!isHovered)
                    {
                        button.hoverObject.SetActive(false);
                    }

                    hoverTween = null;
                });
        }

        public void PlayClick(
            float clickScale,
            float shrinkDuration,
            float returnDuration,
            Ease shrinkEase,
            Ease returnEase,
            Color darkColor,
            TweenCallback onComplete
        )
        {
            clickTween?.Kill();
            hoverTween?.Kill();

            if (button.hoverObject != null)
            {
                button.hoverObject.SetActive(true);
            }

            clickTween = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(button.buttonObject)
                .Append(button.buttonObject.transform.DOScale(baseScale * clickScale, Mathf.Max(0.01f, shrinkDuration)).SetEase(shrinkEase))
                .Join(CreateHoverDarkTween(darkColor, shrinkDuration))
                .Append(button.buttonObject.transform.DOScale(baseScale, Mathf.Max(0.01f, returnDuration)).SetEase(returnEase))
                .OnComplete(() =>
                {
                    clickTween = null;
                    onComplete?.Invoke();
                });
        }

        public void HideHoverImmediate()
        {
            if (button.hoverObject == null)
            {
                return;
            }

            if (hoverCanvasGroup != null)
            {
                hoverCanvasGroup.alpha = hiddenAlpha;
            }

            for (int i = 0; i < hoverGraphics.Count; i++)
            {
                Color color = graphicBaseColors[i];
                color.a = hiddenAlpha;
                hoverGraphics[i].color = color;
            }

            for (int i = 0; i < hoverRenderers.Count; i++)
            {
                Color color = rendererBaseColors[i];
                color.a = hiddenAlpha;
                hoverRenderers[i].material.color = color;
            }

            button.hoverObject.SetActive(false);
        }

        public void Kill()
        {
            hoverTween?.Kill();
            clickTween?.Kill();
        }

        private void CacheHoverVisuals()
        {
            if (button.hoverObject == null)
            {
                return;
            }

            hoverCanvasGroup = button.hoverObject.GetComponent<CanvasGroup>();
            hoverGraphics.AddRange(button.hoverObject.GetComponentsInChildren<Graphic>(true));
            hoverRenderers.AddRange(button.hoverObject.GetComponentsInChildren<Renderer>(true));

            for (int i = 0; i < hoverGraphics.Count; i++)
            {
                graphicBaseColors.Add(hoverGraphics[i].color);
            }

            for (int i = 0; i < hoverRenderers.Count; i++)
            {
                rendererBaseColors.Add(hoverRenderers[i].material.color);
            }
        }

        private Tween CreateHoverDarkTween(Color darkColor, float duration)
        {
            Sequence sequence = DOTween.Sequence().SetUpdate(true);

            if (hoverCanvasGroup != null)
            {
                sequence.Join(hoverCanvasGroup.DOFade(visibleAlpha, Mathf.Max(0.01f, duration)));
            }

            for (int i = 0; i < hoverGraphics.Count; i++)
            {
                sequence.Join(hoverGraphics[i].DOColor(darkColor, Mathf.Max(0.01f, duration)));
            }

            for (int i = 0; i < hoverRenderers.Count; i++)
            {
                sequence.Join(hoverRenderers[i].material.DOColor(darkColor, Mathf.Max(0.01f, duration)));
            }

            return sequence;
        }
    }
}

public class PauseMenuButtonTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private PauseMenuManager manager;
    private PauseMenuManager.PauseAction action;
    private Button uiButton;

    public void Setup(PauseMenuManager manager, PauseMenuManager.PauseAction action)
    {
        this.manager = manager;
        this.action = action;
        uiButton = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        manager?.HandleButtonHovered(action, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        manager?.HandleButtonHovered(action, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (uiButton != null)
        {
            return;
        }

        manager?.HandleButtonClicked(action);
    }

    void OnMouseEnter()
    {
        manager?.HandleButtonHovered(action, true);
    }

    void OnMouseExit()
    {
        manager?.HandleButtonHovered(action, false);
    }

    void OnMouseDown()
    {
        if (uiButton != null)
        {
            return;
        }

        manager?.HandleButtonClicked(action);
    }
}
