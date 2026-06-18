using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuManager : MonoBehaviour
{
    public enum MenuAction
    {
        Play,
        Settings,
        Exit,
        StoryMode,
        SandboxMode,
        ReturnToMainMenu,
        AboutUs,
        Credits,
        CloseAboutUs,
        CloseCredits
    }

    [System.Serializable]
    public class MenuButton
    {
        public GameObject buttonObject;
        public GameObject hoverObject;
    }

    [Header("Buttons")]
    public MenuButton playButton;
    public MenuButton settingsButton;
    public MenuButton aboutUsButton;
    public MenuButton creditsButton;
    public MenuButton exitButton;

    [Header("Panel Back Buttons")]
    public MenuButton aboutUsBackButton;
    public MenuButton creditsBackButton;

    [Header("Stage Buttons")]
    public MenuButton storyModeButton;
    public MenuButton sandboxModeButton;
    public MenuButton returnToMainMenuButton;

    [Header("Menu Panels")]
    public GameObject mainMenuPanel;
    public CanvasGroup mainMenuCanvasGroup;
    public RectTransform mainMenuRect;
    public GameObject stagePanel;
    public CanvasGroup stageCanvasGroup;
    public RectTransform stageRect;
    public bool hideStageOnStart = true;

    [Header("Scene")]
    public string stageSceneName = "StageScene";

    [Header("Settings Panel")]
    public GameObject settingsPanel;
    public CanvasGroup settingsCanvasGroup;
    public bool hideSettingsOnStart = true;
    public bool keepMainMenuVisibleWhenSettingsOpen = true;

    [Header("About Us Panel")]
    public GameObject aboutUsPanel;
    public CanvasGroup aboutUsCanvasGroup;
    public bool hideAboutUsOnStart = true;

    [Header("Credits Panel")]
    public GameObject creditsPanel;
    public CanvasGroup creditsCanvasGroup;
    public bool hideCreditsOnStart = true;

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

    [Header("Settings Animation")]
    public float settingsFadeDuration = 0.18f;
    public float settingsHiddenScale = 0.94f;
    public Ease settingsEase = Ease.OutCubic;

    [Header("Menu Transition Animation")]
    public float menuTransitionDuration = 0.32f;
    public float menuTransitionOffset = 140f;
    public Ease menuTransitionEase = Ease.OutCubic;

    private readonly Dictionary<GameObject, ButtonRuntime> runtimes = new();
    private Tween settingsTween;
    private Tween aboutUsTween;
    private Tween creditsTween;
    private Sequence menuTransitionTween;
    private Vector3 settingsVisibleScale = Vector3.one;
    private Vector3 aboutUsVisibleScale = Vector3.one;
    private Vector3 creditsVisibleScale = Vector3.one;
    private Vector2 mainMenuVisiblePosition;
    private Vector2 stageVisiblePosition;
    private bool settingsScaleCached;
    private bool aboutUsScaleCached;
    private bool creditsScaleCached;
    private bool settingsOpen;
    private bool aboutUsOpen;
    private bool creditsOpen;
    private bool stageOpen;
    private bool menuTransitionPlaying;

    void Awake()
    {
        RegisterButton(playButton, MenuAction.Play);
        RegisterButton(settingsButton, MenuAction.Settings);
        RegisterButton(aboutUsButton, MenuAction.AboutUs);
        RegisterButton(creditsButton, MenuAction.Credits);
        RegisterButton(exitButton, MenuAction.Exit);
        RegisterButton(aboutUsBackButton, MenuAction.CloseAboutUs);
        RegisterButton(creditsBackButton, MenuAction.CloseCredits);
        RegisterButton(storyModeButton, MenuAction.StoryMode);
        RegisterButton(sandboxModeButton, MenuAction.SandboxMode);
        RegisterButton(returnToMainMenuButton, MenuAction.ReturnToMainMenu);
        CacheMenuPanels();
        CacheInfoPanels();
        SyncInfoPanelOpenStates();
    }

    void Start()
    {
        if (hideStageOnStart)
        {
            SetStagePanelImmediate(false);
        }

        if (hideSettingsOnStart)
        {
            SetSettingsPanelImmediate(false);
        }

        if (hideAboutUsOnStart)
        {
            SetAboutUsPanelImmediate(false);
        }

        if (hideCreditsOnStart)
        {
            SetCreditsPanelImmediate(false);
        }

        EnsureOnlyOneInfoPanelImmediate();
    }

    void OnDestroy()
    {
        settingsTween?.Kill();
        aboutUsTween?.Kill();
        creditsTween?.Kill();
        menuTransitionTween?.Kill();

        foreach (ButtonRuntime runtime in runtimes.Values)
        {
            runtime.Kill();
        }
    }

    public void Play()
    {
        OpenStageMenu();
    }

    public void StartStoryMode()
    {
        LoadStageScene(QuestManager.QuestMode.Story);
    }

    public void StartSandboxMode()
    {
        LoadStageScene(QuestManager.QuestMode.Sandbox);
    }

    public void OpenStageMenu()
    {
        CloseAllInfoPanelsImmediate();

        SetStagePanel(true);
    }

    public void ReturnToMainMenu()
    {
        SetStagePanel(false);
    }

    private void LoadStageScene(QuestManager.QuestMode selectedMode)
    {
        if (string.IsNullOrWhiteSpace(stageSceneName))
        {
            Debug.LogWarning($"[{nameof(MainMenuManager)}] Stage Scene Name belum diisi.", this);
            return;
        }

        QuestManager.SaveSelectedMode(selectedMode);
        SceneManager.LoadScene(stageSceneName);
    }

    public void OpenSettings()
    {
        // Settings is shown beside the main menu. Main menu visibility is intentionally unchanged.
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

    public void OpenAboutUs()
    {
        SetAboutUsPanel(true);
    }

    public void CloseAboutUs()
    {
        SetAboutUsPanel(false);
    }

    public void ToggleAboutUs()
    {
        SetAboutUsPanel(!aboutUsOpen);
    }

    public void OpenCredits()
    {
        SetCreditsPanel(true);
    }

    public void CloseCredits()
    {
        SetCreditsPanel(false);
    }

    public void ToggleCredits()
    {
        SetCreditsPanel(!creditsOpen);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RegisterButton(MenuButton menuButton, MenuAction action)
    {
        if (menuButton == null || menuButton.buttonObject == null)
        {
            return;
        }

        ButtonRuntime runtime = new ButtonRuntime(menuButton, hoverVisibleAlpha, hoverHiddenAlpha);
        runtimes[menuButton.buttonObject] = runtime;
        runtime.HideHoverImmediate();

        MainMenuButtonTarget target = menuButton.buttonObject.GetComponent<MainMenuButtonTarget>();
        if (target == null)
        {
            target = menuButton.buttonObject.AddComponent<MainMenuButtonTarget>();
        }

        target.Setup(this, action);

        Button uiButton = menuButton.buttonObject.GetComponent<Button>();
        if (uiButton != null)
        {
            uiButton.onClick.AddListener(() => HandleButtonClicked(action));
        }
    }

    public void HandleButtonHovered(MenuAction action, bool isHovered)
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

    public void HandleButtonClicked(MenuAction action)
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

    private void ExecuteAction(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.Play:
                Play();
                break;
            case MenuAction.Settings:
                OpenSettings();
                break;
            case MenuAction.AboutUs:
                OpenAboutUs();
                break;
            case MenuAction.Credits:
                OpenCredits();
                break;
            case MenuAction.CloseAboutUs:
                CloseAboutUs();
                break;
            case MenuAction.CloseCredits:
                CloseCredits();
                break;
            case MenuAction.Exit:
                ExitGame();
                break;
            case MenuAction.StoryMode:
                StartStoryMode();
                break;
            case MenuAction.SandboxMode:
                StartSandboxMode();
                break;
            case MenuAction.ReturnToMainMenu:
                ReturnToMainMenu();
                break;
        }
    }

    private ButtonRuntime GetRuntime(MenuAction action)
    {
        MenuButton menuButton = GetButton(action);
        if (menuButton == null || menuButton.buttonObject == null)
        {
            return null;
        }

        return runtimes.TryGetValue(menuButton.buttonObject, out ButtonRuntime runtime)
            ? runtime
            : null;
    }

    private MenuButton GetButton(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.Play:
                return playButton;
            case MenuAction.Settings:
                return settingsButton;
            case MenuAction.AboutUs:
                return aboutUsButton;
            case MenuAction.Credits:
                return creditsButton;
            case MenuAction.CloseAboutUs:
                return aboutUsBackButton;
            case MenuAction.CloseCredits:
                return creditsBackButton;
            case MenuAction.Exit:
                return exitButton;
            case MenuAction.StoryMode:
                return storyModeButton;
            case MenuAction.SandboxMode:
                return sandboxModeButton;
            case MenuAction.ReturnToMainMenu:
                return returnToMainMenuButton;
            default:
                return null;
        }
    }

    private void CacheMenuPanels()
    {
        if (mainMenuPanel != null)
        {
            if (mainMenuCanvasGroup == null)
            {
                mainMenuCanvasGroup = mainMenuPanel.GetComponent<CanvasGroup>();
            }

            if (mainMenuCanvasGroup == null)
            {
                mainMenuCanvasGroup = mainMenuPanel.AddComponent<CanvasGroup>();
            }

            if (mainMenuRect == null)
            {
                mainMenuRect = mainMenuPanel.GetComponent<RectTransform>();
            }

            if (mainMenuRect != null)
            {
                mainMenuVisiblePosition = mainMenuRect.anchoredPosition;
            }
        }

        if (stagePanel != null)
        {
            if (stageCanvasGroup == null)
            {
                stageCanvasGroup = stagePanel.GetComponent<CanvasGroup>();
            }

            if (stageCanvasGroup == null)
            {
                stageCanvasGroup = stagePanel.AddComponent<CanvasGroup>();
            }

            if (stageRect == null)
            {
                stageRect = stagePanel.GetComponent<RectTransform>();
            }

            if (stageRect != null)
            {
                stageVisiblePosition = stageRect.anchoredPosition;
            }
        }
    }

    private void SetStagePanel(bool open)
    {
        CacheMenuPanels();

        if (mainMenuPanel == null || stagePanel == null || mainMenuCanvasGroup == null || stageCanvasGroup == null)
        {
            Debug.LogWarning($"[{nameof(MainMenuManager)}] Main Menu Panel dan Stage Panel harus diisi untuk transisi stage UI.", this);
            return;
        }

        if (menuTransitionPlaying || stageOpen == open)
        {
            return;
        }

        SoundManager.PlaySound(open ? SoundType.UIPopup : SoundType.UIClose);

        stageOpen = open;
        menuTransitionPlaying = true;
        menuTransitionTween?.Kill();

        GameObject incomingPanel = open ? stagePanel : mainMenuPanel;
        CanvasGroup incomingGroup = open ? stageCanvasGroup : mainMenuCanvasGroup;
        RectTransform incomingRect = open ? stageRect : mainMenuRect;
        Vector2 incomingVisiblePosition = open ? stageVisiblePosition : mainMenuVisiblePosition;
        Vector2 incomingHiddenPosition = incomingVisiblePosition - Vector2.up * menuTransitionOffset;

        GameObject outgoingPanel = open ? mainMenuPanel : stagePanel;
        CanvasGroup outgoingGroup = open ? mainMenuCanvasGroup : stageCanvasGroup;
        RectTransform outgoingRect = open ? mainMenuRect : stageRect;
        Vector2 outgoingVisiblePosition = open ? mainMenuVisiblePosition : stageVisiblePosition;
        Vector2 outgoingHiddenPosition = outgoingVisiblePosition + Vector2.up * menuTransitionOffset;

        incomingPanel.SetActive(true);
        outgoingPanel.SetActive(true);
        incomingGroup.alpha = 0f;
        incomingGroup.interactable = false;
        incomingGroup.blocksRaycasts = false;
        outgoingGroup.interactable = false;
        outgoingGroup.blocksRaycasts = false;
        SetAnchoredPosition(incomingRect, incomingHiddenPosition);
        SetAnchoredPosition(outgoingRect, outgoingVisiblePosition);

        menuTransitionTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(this)
            .Append(incomingGroup.DOFade(1f, Mathf.Max(0.01f, menuTransitionDuration)))
            .Join(outgoingGroup.DOFade(0f, Mathf.Max(0.01f, menuTransitionDuration)))
            .Join(GetAnchoredPositionTween(incomingRect, incomingVisiblePosition, menuTransitionDuration))
            .Join(GetAnchoredPositionTween(outgoingRect, outgoingHiddenPosition, menuTransitionDuration))
            .SetEase(menuTransitionEase)
            .OnComplete(() =>
            {
                incomingGroup.interactable = true;
                incomingGroup.blocksRaycasts = true;
                outgoingPanel.SetActive(false);
                SetAnchoredPosition(outgoingRect, outgoingVisiblePosition);
                menuTransitionPlaying = false;
                menuTransitionTween = null;
            });
    }

    private void SetStagePanelImmediate(bool open)
    {
        CacheMenuPanels();

        stageOpen = open;
        menuTransitionTween?.Kill();
        menuTransitionTween = null;
        menuTransitionPlaying = false;

        SetPanelImmediate(mainMenuPanel, mainMenuCanvasGroup, mainMenuRect, mainMenuVisiblePosition, !open);
        SetPanelImmediate(stagePanel, stageCanvasGroup, stageRect, stageVisiblePosition, open);
    }

    private void SetPanelImmediate(
        GameObject panel,
        CanvasGroup canvasGroup,
        RectTransform rectTransform,
        Vector2 visiblePosition,
        bool visible
    )
    {
        if (panel == null || canvasGroup == null)
        {
            return;
        }

        panel.SetActive(visible);
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        SetAnchoredPosition(rectTransform, visiblePosition);
    }

    private Tween GetAnchoredPositionTween(RectTransform rectTransform, Vector2 targetPosition, float duration)
    {
        if (rectTransform == null)
        {
            return DOTween.To(() => 0f, _ => { }, 1f, Mathf.Max(0.01f, duration));
        }

        return rectTransform.DOAnchorPos(targetPosition, Mathf.Max(0.01f, duration));
    }

    private void SetAnchoredPosition(RectTransform rectTransform, Vector2 position)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchoredPosition = position;
    }

    private enum InfoPanelType
    {
        Settings,
        AboutUs,
        Credits
    }

    private void CacheInfoPanels()
    {
        settingsCanvasGroup = CacheInfoPanel(settingsPanel, settingsCanvasGroup, ref settingsVisibleScale, ref settingsScaleCached);
        aboutUsCanvasGroup = CacheInfoPanel(aboutUsPanel, aboutUsCanvasGroup, ref aboutUsVisibleScale, ref aboutUsScaleCached);
        creditsCanvasGroup = CacheInfoPanel(creditsPanel, creditsCanvasGroup, ref creditsVisibleScale, ref creditsScaleCached);
    }

    private CanvasGroup CacheInfoPanel(
        GameObject panel,
        CanvasGroup canvasGroup,
        ref Vector3 visibleScale,
        ref bool scaleCached
    )
    {
        if (panel == null)
        {
            return canvasGroup;
        }

        if (canvasGroup == null)
        {
            canvasGroup = panel.GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = panel.AddComponent<CanvasGroup>();
        }

        if (!scaleCached)
        {
            visibleScale = panel.transform.localScale;
            scaleCached = true;
        }

        return canvasGroup;
    }

    private void SetSettingsPanel(bool open)
    {
        CacheInfoPanels();

        if (settingsPanel == null || settingsCanvasGroup == null)
        {
            return;
        }

        if (IsInfoPanelInState(settingsPanel, settingsOpen, open))
        {
            return;
        }

        if (open)
        {
            CloseOtherInfoPanelsImmediate(InfoPanelType.Settings);
        }

        settingsOpen = open;
        settingsTween?.Kill();
        settingsTween = PlayInfoPanelTween(settingsPanel, settingsCanvasGroup, settingsVisibleScale, open, () => settingsTween = null);
    }

    private void SetSettingsPanelImmediate(bool open)
    {
        CacheInfoPanels();

        SetInfoPanelImmediate(settingsPanel, settingsCanvasGroup, ref settingsTween, ref settingsOpen, settingsVisibleScale, open);
    }

    private void SetAboutUsPanel(bool open)
    {
        CacheInfoPanels();

        if (aboutUsPanel == null || aboutUsCanvasGroup == null)
        {
            return;
        }

        if (IsInfoPanelInState(aboutUsPanel, aboutUsOpen, open))
        {
            return;
        }

        if (open)
        {
            CloseOtherInfoPanelsImmediate(InfoPanelType.AboutUs);
        }

        aboutUsOpen = open;
        aboutUsTween?.Kill();
        aboutUsTween = PlayInfoPanelTween(aboutUsPanel, aboutUsCanvasGroup, aboutUsVisibleScale, open, () => aboutUsTween = null);
    }

    private void SetAboutUsPanelImmediate(bool open)
    {
        CacheInfoPanels();

        SetInfoPanelImmediate(aboutUsPanel, aboutUsCanvasGroup, ref aboutUsTween, ref aboutUsOpen, aboutUsVisibleScale, open);
    }

    private void SetCreditsPanel(bool open)
    {
        CacheInfoPanels();

        if (creditsPanel == null || creditsCanvasGroup == null)
        {
            return;
        }

        if (IsInfoPanelInState(creditsPanel, creditsOpen, open))
        {
            return;
        }

        if (open)
        {
            CloseOtherInfoPanelsImmediate(InfoPanelType.Credits);
        }

        creditsOpen = open;
        creditsTween?.Kill();
        creditsTween = PlayInfoPanelTween(creditsPanel, creditsCanvasGroup, creditsVisibleScale, open, () => creditsTween = null);
    }

    private void SetCreditsPanelImmediate(bool open)
    {
        CacheInfoPanels();

        SetInfoPanelImmediate(creditsPanel, creditsCanvasGroup, ref creditsTween, ref creditsOpen, creditsVisibleScale, open);
    }

    private Tween PlayInfoPanelTween(
        GameObject panel,
        CanvasGroup canvasGroup,
        Vector3 visibleScale,
        bool open,
        TweenCallback onComplete
    )
    {
        panel.SetActive(true);
        canvasGroup.interactable = open;
        canvasGroup.blocksRaycasts = open;

        if (open)
        {
            SoundManager.PlaySound(SoundType.UIPopup);
            canvasGroup.alpha = 0f;
            panel.transform.localScale = visibleScale * settingsHiddenScale;
        }
        else
        {
            SoundManager.PlaySound(SoundType.UIClose);
        }

        return DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(panel)
            .Append(canvasGroup.DOFade(open ? 1f : 0f, Mathf.Max(0.01f, settingsFadeDuration)))
            .Join(panel.transform.DOScale(open ? visibleScale : visibleScale * settingsHiddenScale, Mathf.Max(0.01f, settingsFadeDuration)))
            .SetEase(settingsEase)
            .OnComplete(() =>
            {
                canvasGroup.interactable = open;
                canvasGroup.blocksRaycasts = open;
                panel.SetActive(open);
                onComplete?.Invoke();
            });
    }

    private void SetInfoPanelImmediate(
        GameObject panel,
        CanvasGroup canvasGroup,
        ref Tween tween,
        ref bool isOpen,
        Vector3 visibleScale,
        bool open
    )
    {
        if (panel == null || canvasGroup == null)
        {
            return;
        }

        isOpen = open;
        tween?.Kill();
        tween = null;
        panel.SetActive(open);
        canvasGroup.alpha = open ? 1f : 0f;
        canvasGroup.interactable = open;
        canvasGroup.blocksRaycasts = open;
        panel.transform.localScale = open ? visibleScale : visibleScale * settingsHiddenScale;
    }

    private bool IsInfoPanelInState(GameObject panel, bool isOpen, bool open)
    {
        return isOpen == open && panel != null && panel.activeSelf == open;
    }

    private void CloseOtherInfoPanelsImmediate(InfoPanelType activePanel)
    {
        if (activePanel != InfoPanelType.Settings)
        {
            SetSettingsPanelImmediate(false);
        }

        if (activePanel != InfoPanelType.AboutUs)
        {
            SetAboutUsPanelImmediate(false);
        }

        if (activePanel != InfoPanelType.Credits)
        {
            SetCreditsPanelImmediate(false);
        }
    }

    private void CloseAllInfoPanelsImmediate()
    {
        SetSettingsPanelImmediate(false);
        SetAboutUsPanelImmediate(false);
        SetCreditsPanelImmediate(false);
    }

    private void EnsureOnlyOneInfoPanelImmediate()
    {
        bool foundOpenPanel = false;

        if (settingsOpen)
        {
            foundOpenPanel = true;
        }

        if (aboutUsOpen)
        {
            if (foundOpenPanel)
            {
                SetAboutUsPanelImmediate(false);
            }
            else
            {
                foundOpenPanel = true;
            }
        }

        if (creditsOpen && foundOpenPanel)
        {
            SetCreditsPanelImmediate(false);
        }
    }

    private void SyncInfoPanelOpenStates()
    {
        settingsOpen = settingsPanel != null && settingsPanel.activeSelf;
        aboutUsOpen = aboutUsPanel != null && aboutUsPanel.activeSelf;
        creditsOpen = creditsPanel != null && creditsPanel.activeSelf;
    }

    private class ButtonRuntime
    {
        private readonly MenuButton button;
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

        public ButtonRuntime(MenuButton button, float visibleAlpha, float hiddenAlpha)
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

public class MainMenuButtonTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private MainMenuManager manager;
    private MainMenuManager.MenuAction action;
    private Button uiButton;

    public void Setup(MainMenuManager manager, MainMenuManager.MenuAction action)
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
