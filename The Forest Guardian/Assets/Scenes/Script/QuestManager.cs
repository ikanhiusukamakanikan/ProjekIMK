using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }
    public const string SelectedModePlayerPrefsKey = "ForestGuardian.SelectedQuestMode";

    public enum QuestMode
    {
        Story,
        Sandbox
    }

    private enum StoryStep
    {
        None,
        Move,
        Look,
        Sprint,
        OpenInventory,
        SummonScanner,
        GrabScanner,
        ScanBadTree,
        ChopBadTrees,
        DigAfterChop,
        PlantAfterChop,
        SummonFireHose,
        GrabFireHose,
        ExtinguishFire,
        DigAfterFire,
        PlantAfterFire,
        RestoreStats,
        Complete
    }

    private enum ObjectiveType
    {
        None,
        Move,
        Look,
        Sprint,
        OpenInventory,
        SummonScanner,
        GrabScanner,
        ScanBadTree,
        ChopBadTree,
        Dig,
        PlantTree,
        SummonFireHose,
        GrabFireHose,
        ExtinguishFire,
        RestoreStats
    }

    [Serializable]
    public struct StatDelta
    {
        public float coverage;
        public float co2Level;
        public float damagePercent;
        public float temperature;

        public StatDelta(float coverage, float co2Level, float damagePercent, float temperature)
        {
            this.coverage = coverage;
            this.co2Level = co2Level;
            this.damagePercent = damagePercent;
            this.temperature = temperature;
        }

        public void Apply(StatsIndicatorUI stats, bool animate = true, float multiplier = 1f)
        {
            if (stats == null)
            {
                return;
            }

            stats.ApplyDelta(
                coverage * multiplier,
                co2Level * multiplier,
                damagePercent * multiplier,
                temperature * multiplier,
                animate
            );
        }
    }

    [Header("Mode")]
    public QuestMode mode = QuestMode.Story;
    public bool useSelectedModeFromMainMenu = true;
    public bool startStoryOnStart = true;
    public bool sandboxHotkeysEnabled = true;

    [Header("References")]
    public StatsIndicatorUI statsIndicator;
    public FireSpawner fireSpawner;
    public WinUIHandler winUIHandler;
    public LoseUIHandler loseUIHandler;

    [Header("Dialog UI")]
    public CanvasGroup dialogCanvasGroup;
    public RectTransform dialogPanel;
    public TMP_Text dialogText;

    [Header("Quest UI")]
    public TMP_Text questText;

    [Header("UI Animation")]
    public float panelAppearDuration = 0.2f;
    public float panelDisappearDuration = 0.14f;
    public float hiddenPanelScale = 0.92f;
    public bool autoHideDialog = false;
    public float dialogAutoHideDelay = 3.5f;
    public Ease panelAppearEase = Ease.OutBack;
    public Ease panelDisappearEase = Ease.InCubic;

    [Header("Story Targets")]
    public int badTreesToChop = 5;
    public int treesToPlantAfterChop = 3;
    public int treesToPlantAfterFire = 5;

    [Header("Sandbox Targets")]
    public int sandboxScanTarget = 1;
    public int sandboxChopTarget = 5;
    public int sandboxPlantTarget = 3;

    [Header("Stats Delta")]
    public StatDelta chopBadTreeDelta = new StatDelta(-2f, 25f, 3f, 0.3f);
    public StatDelta digDelta = new StatDelta(-1f, 12f, 2f, 0.2f);
    public StatDelta plantTreeDelta = new StatDelta(4f, -35f, -6f, -0.5f);
    public StatDelta fireIgniteDelta = new StatDelta(-4f, 80f, 8f, 1.5f);
    public StatDelta fireTickDelta = new StatDelta(-0.8f, 18f, 2f, 0.35f);

    [Header("Fire Rules")]
    public int fireLoseGeneration = 3;
    public float fireStatsTickInterval = 1f;
    public int maxFireTickMultiplier = 5;

    [Header("Input Detection")]
    public float movementInputThreshold = 0.35f;
    public float lookInputThreshold = 0.35f;

    [Header("VR Sprint")]
    public bool enableVrSprint = true;
    public MonoBehaviour continuousMoveProvider;
    public bool autoFindContinuousMoveProvider = true;
    public float walkSpeed = 2f;
    public float sprintSpeed = 4f;
    public float sprintAcceleration = 1.5f;
    public float sprintDeceleration = 2.5f;

    public event Action QuestWon;
    public event Action<string> QuestLost;

    private readonly List<InputDevice> controllers = new();
    private readonly HashSet<FireNode> activeFires = new();

    private StoryStep currentStoryStep = StoryStep.None;
    private ObjectiveType currentObjective = ObjectiveType.None;
    private string objectiveLabel = string.Empty;
    private int currentAmount;
    private int targetAmount;
    private int totalFiresDuringEmergency;
    private int extinguishedFiresDuringEmergency;
    private float nextFireStatsTick;
    private bool questRunning;
    private bool questFinished;
    private bool sandboxPlantFlowActive;
    private bool fireEmergencyActive;

    private Vector3 dialogVisibleScale = Vector3.one;
    private Vector3 questVisibleScale = Vector3.one;
    private Sequence dialogTween;
    private Sequence questTween;
    private Tween questPulseTween;
    private CanvasGroup questCanvasGroup;
    private RectTransform questPanel;
    private PropertyInfo moveSpeedProperty;
    private FieldInfo moveSpeedField;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveReferences();
        CachePanelScales();
        HidePanelImmediate(dialogCanvasGroup, dialogPanel, dialogVisibleScale);
        HidePanelImmediate(questCanvasGroup, questPanel, questVisibleScale);
    }

    void OnEnable()
    {
        ItemSelectionUI.MenuOpened += OnInventoryOpened;
        ItemSelectionUI.ItemSpawned += OnItemSpawned;
        TreeScanner.ScannerGrabbed += OnScannerGrabbed;
        TreeScanner.TreeScanned += OnTreeScanned;
        TreeChop.TreeBroken += OnTreeBroken;
        VRShovelDig.Dug += OnDug;
        TanahGrowTree.TreeGrown += OnTreeGrown;
        TanahGrowTree.PlantingBlockedByBurnRule += OnPlantingBlockedByBurnRule;
        FireHoseSpray.FireHoseGrabbed += OnFireHoseGrabbed;
        FireNode.FireIgnited += OnFireIgnited;
        FireNode.FireBurnedOut += OnFireBurnedOut;
        FireNode.FireExtinguished += OnFireExtinguished;
    }

    void Start()
    {
        ResolveReferences();
        ApplySelectedModeFromMainMenu();

        if (mode == QuestMode.Story && startStoryOnStart)
        {
            StartStoryMode();
        }
        else if (mode == QuestMode.Sandbox)
        {
            StartSandboxMode();
        }
    }

    void Update()
    {
        if (mode == QuestMode.Sandbox && sandboxHotkeysEnabled)
        {
            HandleSandboxHotkeys();
        }

        HandleVrSprint();

        if (!questRunning || questFinished)
        {
            return;
        }

        HandleInputObjectives();
        HandleFireStatsTick();
        HandleRestoreStatsObjective();
        CheckLoseByStats();
    }

    void OnDisable()
    {
        ItemSelectionUI.MenuOpened -= OnInventoryOpened;
        ItemSelectionUI.ItemSpawned -= OnItemSpawned;
        TreeScanner.ScannerGrabbed -= OnScannerGrabbed;
        TreeScanner.TreeScanned -= OnTreeScanned;
        TreeChop.TreeBroken -= OnTreeBroken;
        VRShovelDig.Dug -= OnDug;
        TanahGrowTree.TreeGrown -= OnTreeGrown;
        TanahGrowTree.PlantingBlockedByBurnRule -= OnPlantingBlockedByBurnRule;
        FireHoseSpray.FireHoseGrabbed -= OnFireHoseGrabbed;
        FireNode.FireIgnited -= OnFireIgnited;
        FireNode.FireBurnedOut -= OnFireBurnedOut;
        FireNode.FireExtinguished -= OnFireExtinguished;

        dialogTween?.Kill();
        questTween?.Kill();
        questPulseTween?.Kill();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void StartStoryMode()
    {
        mode = QuestMode.Story;
        questRunning = true;
        questFinished = false;
        sandboxPlantFlowActive = false;
        fireEmergencyActive = false;
        activeFires.Clear();
        currentStoryStep = StoryStep.None;
        AdvanceStory();
    }

    public void StartSandboxMode()
    {
        mode = QuestMode.Sandbox;
        questRunning = false;
        questFinished = false;
        sandboxPlantFlowActive = false;
        fireEmergencyActive = false;
        currentObjective = ObjectiveType.None;
        currentStoryStep = StoryStep.None;
        ShowDialog("Sandbox mode ready. Use numpad 1-4 to start a quest.");
        questTween = HidePanel(
            questCanvasGroup,
            questPanel,
            questVisibleScale,
            questTween,
            () => questTween = null
        );
    }

    public static void SaveSelectedMode(QuestMode selectedMode)
    {
        PlayerPrefs.SetString(SelectedModePlayerPrefsKey, selectedMode.ToString());
        PlayerPrefs.Save();
    }

    private void ApplySelectedModeFromMainMenu()
    {
        if (!useSelectedModeFromMainMenu || !PlayerPrefs.HasKey(SelectedModePlayerPrefsKey))
        {
            return;
        }

        string selectedMode = PlayerPrefs.GetString(SelectedModePlayerPrefsKey);
        if (Enum.TryParse(selectedMode, out QuestMode parsedMode))
        {
            mode = parsedMode;
        }
    }

    private void AdvanceStory()
    {
        switch (currentStoryStep)
        {
            case StoryStep.None:
                BeginStoryStep(StoryStep.Move);
                break;
            case StoryStep.Move:
                BeginStoryStep(StoryStep.Look);
                break;
            case StoryStep.Look:
                BeginStoryStep(StoryStep.Sprint);
                break;
            case StoryStep.Sprint:
                BeginStoryStep(StoryStep.OpenInventory);
                break;
            case StoryStep.OpenInventory:
                BeginStoryStep(StoryStep.SummonScanner);
                break;
            case StoryStep.SummonScanner:
                BeginStoryStep(StoryStep.GrabScanner);
                break;
            case StoryStep.GrabScanner:
                BeginStoryStep(StoryStep.ScanBadTree);
                break;
            case StoryStep.ScanBadTree:
                BeginStoryStep(StoryStep.ChopBadTrees);
                break;
            case StoryStep.ChopBadTrees:
                BeginStoryStep(StoryStep.DigAfterChop);
                break;
            case StoryStep.DigAfterChop:
                BeginStoryStep(StoryStep.PlantAfterChop);
                break;
            case StoryStep.PlantAfterChop:
                BeginStoryStep(StoryStep.SummonFireHose);
                break;
            case StoryStep.SummonFireHose:
                BeginStoryStep(StoryStep.GrabFireHose);
                break;
            case StoryStep.GrabFireHose:
                BeginStoryFireEmergency();
                break;
            case StoryStep.ExtinguishFire:
                BeginStoryStep(StoryStep.DigAfterFire);
                break;
            case StoryStep.DigAfterFire:
                BeginStoryStep(StoryStep.PlantAfterFire);
                break;
            case StoryStep.PlantAfterFire:
                BeginStoryStep(StoryStep.RestoreStats);
                break;
            case StoryStep.RestoreStats:
                CompleteStoryQuest();
                break;
        }
    }

    private void BeginStoryStep(StoryStep step)
    {
        currentStoryStep = step;

        switch (step)
        {
            case StoryStep.Move:
                StartObjective(ObjectiveType.Move, "Move with Left Joystick", 1, "Move with the left joystick.");
                break;
            case StoryStep.Look:
                StartObjective(ObjectiveType.Look, "Look Around", 1, "Move the camera with the right joystick.");
                break;
            case StoryStep.Sprint:
                StartObjective(ObjectiveType.Sprint, "Try Running", 1, "Hold the right controller primary button to run.");
                break;
            case StoryStep.OpenInventory:
                StartObjective(ObjectiveType.OpenInventory, "Open Inventory", 1, "Open the inventory using [X | Primary Button] to summon the scanner.");
                break;
            case StoryStep.SummonScanner:
                StartObjective(ObjectiveType.SummonScanner, "Summon Scanner", 1, "Choose the scanner from the inventory.");
                break;
            case StoryStep.GrabScanner:
                StartObjective(ObjectiveType.GrabScanner, "Grab Scanner", 1, "Grab the scanner with [Grip Button].");
                break;
            case StoryStep.ScanBadTree:
                StartObjective(ObjectiveType.ScanBadTree, "Scan Bad Tree", 1, "Aim the scanner at a bad tree.");
                break;
            case StoryStep.ChopBadTrees:
                StartObjective(
                    ObjectiveType.ChopBadTree,
                    BuildTargetLabel("Chop", badTreesToChop, "Bad Trees"),
                    badTreesToChop,
                    "Cut down bad trees with the axe."
                );
                break;
            case StoryStep.DigAfterChop:
                StartObjective(
                    ObjectiveType.Dig,
                    BuildTargetLabel("Dig", treesToPlantAfterChop, "Planting Holes"),
                    treesToPlantAfterChop,
                    "Dig soil before planting new trees."
                );
                break;
            case StoryStep.PlantAfterChop:
                StartObjective(
                    ObjectiveType.PlantTree,
                    BuildTargetLabel("Plant", treesToPlantAfterChop, "Trees"),
                    treesToPlantAfterChop,
                    "Plant new trees in the prepared soil."
                );
                break;
            case StoryStep.SummonFireHose:
                StartObjective(ObjectiveType.SummonFireHose, "Summon Fire Hose", 1, "Choose the fire hose from the inventory.");
                break;
            case StoryStep.GrabFireHose:
                StartObjective(ObjectiveType.GrabFireHose, "Grab Fire Hose", 1, "Grab the fire hose before the fire starts.");
                break;
            case StoryStep.DigAfterFire:
                StartObjective(
                    ObjectiveType.Dig,
                    BuildTargetLabel("Dig", treesToPlantAfterFire, "Burned Areas"),
                    treesToPlantAfterFire,
                    "Dig the burned soil before replanting."
                );
                break;
            case StoryStep.PlantAfterFire:
                StartObjective(
                    ObjectiveType.PlantTree,
                    BuildTargetLabel("Plant", treesToPlantAfterFire, "Trees"),
                    treesToPlantAfterFire,
                    "Plant trees in burned areas. If no burned area exists, planting anywhere is allowed."
                );
                break;
            case StoryStep.RestoreStats:
                StartObjective(
                    ObjectiveType.RestoreStats,
                    "Restore Forest Stats",
                    1,
                    "Keep planting until the forest stats return to a healthy state."
                );
                RefreshQuestText();
                break;
        }
    }

    private void BeginStoryFireEmergency()
    {
        currentStoryStep = StoryStep.ExtinguishFire;
        BeginFireEmergencyObjective("A fire has started. Extinguish it before it reaches generation 3.");
    }

    private void HandleSandboxHotkeys()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Keypad1))
        {
            ClearAllFireObjects();
            StartSandboxObjective(
                ObjectiveType.ScanBadTree,
                BuildTargetLabel("Scan", sandboxScanTarget, "Bad Trees"),
                sandboxScanTarget,
                "Scan bad trees with the scanner."
            );
        }
        else if (Input.GetKeyDown(KeyCode.Keypad2))
        {
            ClearAllFireObjects();
            StartSandboxObjective(
                ObjectiveType.ChopBadTree,
                BuildTargetLabel("Chop", sandboxChopTarget, "Bad Trees"),
                sandboxChopTarget,
                "Cut down bad trees with the axe."
            );
        }
        else if (Input.GetKeyDown(KeyCode.Keypad3))
        {
            ClearAllFireObjects();
            BeginFireEmergencyObjective("Fire sandbox quest started. Extinguish all active fires.");
        }
        else if (Input.GetKeyDown(KeyCode.Keypad4))
        {
            ClearAllFireObjects();
            sandboxPlantFlowActive = true;
            StartSandboxObjective(
                ObjectiveType.Dig,
                BuildTargetLabel("Dig", sandboxPlantTarget, "Planting Holes"),
                sandboxPlantTarget,
                "Dig first, then plant trees."
            );
        }
#endif
    }

    private void StartSandboxObjective(ObjectiveType type, string label, int target, string dialog)
    {
        if (mode != QuestMode.Sandbox)
        {
            return;
        }

        fireEmergencyActive = false;
        questFinished = false;
        questRunning = true;
        currentStoryStep = StoryStep.None;
        StartObjective(type, label, target, dialog);
    }

    private void BeginFireEmergencyObjective(string dialog)
    {
        ResolveReferences();

        questFinished = false;
        questRunning = true;
        sandboxPlantFlowActive = false;
        fireEmergencyActive = true;
        activeFires.Clear();
        totalFiresDuringEmergency = 0;
        extinguishedFiresDuringEmergency = 0;
        nextFireStatsTick = Time.time + fireStatsTickInterval;

        int expectedFires = fireSpawner != null ? Mathf.Max(1, fireSpawner.spawnCount) : 1;
        StartObjective(ObjectiveType.ExtinguishFire, "Extinguish Fires", expectedFires, dialog);

        int spawnedCount = fireSpawner != null ? fireSpawner.SpawnFire() : 0;
        if (spawnedCount == 0)
        {
            ShowDialog("No valid fire spawn point was found. Check FireSpawner height limits.");
        }
    }

    private void StartObjective(ObjectiveType type, string label, int target, string dialog)
    {
        currentObjective = type;
        objectiveLabel = label;
        currentAmount = 0;
        targetAmount = Mathf.Max(1, target);

        ShowDialog(dialog);
        RefreshQuestText();
        PulseQuestPanel();
    }

    private void AddObjectiveProgress(int amount = 1)
    {
        if (!questRunning || questFinished || currentObjective == ObjectiveType.None)
        {
            return;
        }

        currentAmount = Mathf.Clamp(currentAmount + amount, 0, targetAmount);
        RefreshQuestText();

        if (currentObjective == ObjectiveType.ExtinguishFire)
        {
            TryCompleteExtinguishObjective();
            return;
        }

        if (currentAmount >= targetAmount)
        {
            CompleteCurrentObjective();
        }
    }

    private void CompleteCurrentObjective()
    {
        if (mode == QuestMode.Story)
        {
            AdvanceStory();
            return;
        }

        if (mode == QuestMode.Sandbox && sandboxPlantFlowActive && currentObjective == ObjectiveType.Dig)
        {
            StartSandboxObjective(
                ObjectiveType.PlantTree,
                BuildTargetLabel("Plant", sandboxPlantTarget, "Trees"),
                sandboxPlantTarget,
                "Plant trees in burned areas. If no burned area exists, planting anywhere is allowed."
            );
            return;
        }

        CompleteSandboxObjective();
    }

    private void CompleteSandboxObjective()
    {
        questRunning = false;
        sandboxPlantFlowActive = false;
        fireEmergencyActive = false;
        currentObjective = ObjectiveType.None;
        ShowDialog("Sandbox quest complete.");
        ShowQuestText("Quest Complete");
    }

    private void CompleteStoryQuest()
    {
        questRunning = false;
        questFinished = true;
        fireEmergencyActive = false;
        currentObjective = ObjectiveType.None;
        currentStoryStep = StoryStep.Complete;
        ShowDialog("Forest restored. Mission complete.");
        ShowQuestText("Quest Complete");
        ShowWinScreen();
        QuestWon?.Invoke();
    }

    private void LoseQuest(string reason)
    {
        if (questFinished)
        {
            return;
        }

        questRunning = false;
        questFinished = true;
        fireEmergencyActive = false;
        currentObjective = ObjectiveType.None;
        ShowDialog("Mission failed: " + reason);
        ShowQuestText("Mission Failed");
        ShowLoseScreen();
        QuestLost?.Invoke(reason);
    }

    private void ShowWinScreen()
    {
        ResolveReferences();

        if (mode != QuestMode.Story || winUIHandler == null)
        {
            return;
        }

        if (statsIndicator != null)
        {
            winUIHandler.Show(statsIndicator.Snapshot);
            return;
        }

        winUIHandler.Show();
    }

    private void ShowLoseScreen()
    {
        ResolveReferences();

        if (loseUIHandler == null)
        {
            return;
        }

        if (statsIndicator != null)
        {
            loseUIHandler.Show(statsIndicator.Snapshot);
            return;
        }

        loseUIHandler.Show();
    }

    private void HandleInputObjectives()
    {
        if (currentObjective == ObjectiveType.Move && HasMoveInput())
        {
            AddObjectiveProgress();
        }
        else if (currentObjective == ObjectiveType.Look && HasLookInput())
        {
            AddObjectiveProgress();
        }
        else if (currentObjective == ObjectiveType.Sprint && HasRightPrimaryButtonInput())
        {
            AddObjectiveProgress();
        }
    }

    private void HandleRestoreStatsObjective()
    {
        if (currentObjective != ObjectiveType.RestoreStats || statsIndicator == null)
        {
            return;
        }

        RefreshQuestText();

        if (statsIndicator.IsHealthy)
        {
            currentAmount = targetAmount;
            CompleteCurrentObjective();
        }
    }

    private void HandleFireStatsTick()
    {
        if (!fireEmergencyActive || activeFires.Count == 0 || Time.time < nextFireStatsTick)
        {
            return;
        }

        nextFireStatsTick = Time.time + fireStatsTickInterval;
        float multiplier = Mathf.Clamp(activeFires.Count, 1, Mathf.Max(1, maxFireTickMultiplier));
        fireTickDelta.Apply(statsIndicator, true, multiplier);
    }

    private void CheckLoseByStats()
    {
        if (statsIndicator == null || !statsIndicator.IsDanger)
        {
            return;
        }

        LoseQuest("forest stats reached danger level.");
    }

    private void OnInventoryOpened(ItemSelectionUI selectionUI)
    {
        if (currentObjective == ObjectiveType.OpenInventory)
        {
            AddObjectiveProgress();
        }
    }

    private void OnItemSpawned(ItemSelectionUI selectionUI, GameObject spawnedItem)
    {
        if (spawnedItem == null)
        {
            return;
        }

        if (currentObjective == ObjectiveType.SummonScanner
            && spawnedItem.GetComponentInChildren<TreeScanner>(true) != null)
        {
            AddObjectiveProgress();
        }
        else if (currentObjective == ObjectiveType.SummonFireHose
            && spawnedItem.GetComponentInChildren<FireHoseSpray>(true) != null)
        {
            AddObjectiveProgress();
        }
    }

    private void OnScannerGrabbed(TreeScanner scanner)
    {
        if (currentObjective == ObjectiveType.GrabScanner)
        {
            AddObjectiveProgress();
        }
    }

    private void OnTreeScanned(TreeScanner scanner, TreeData treeData, bool isBadTree)
    {
        if (currentObjective == ObjectiveType.ScanBadTree && isBadTree)
        {
            AddObjectiveProgress();
        }
    }

    private void OnTreeBroken(TreeChop treeChop, TreeData treeData)
    {
        if (treeData == null || !treeData.isBadTree)
        {
            return;
        }

        chopBadTreeDelta.Apply(statsIndicator);

        if (currentObjective == ObjectiveType.ChopBadTree)
        {
            AddObjectiveProgress();
        }
    }

    private void OnDug(Vector3 digPosition)
    {
        digDelta.Apply(statsIndicator);

        if (currentObjective == ObjectiveType.Dig)
        {
            AddObjectiveProgress();
        }
    }

    private void OnTreeGrown(TanahGrowTree soil, GameObject tree)
    {
        plantTreeDelta.Apply(statsIndicator);

        if (currentObjective == ObjectiveType.PlantTree)
        {
            AddObjectiveProgress();
        }
        else if (currentObjective == ObjectiveType.RestoreStats)
        {
            RefreshQuestText();
        }
    }

    private void OnPlantingBlockedByBurnRule(TanahGrowTree soil)
    {
        if (currentObjective == ObjectiveType.PlantTree || currentObjective == ObjectiveType.RestoreStats)
        {
            ShowDialog("Plant inside the burned area first.");
        }
    }

    private void OnFireHoseGrabbed(FireHoseSpray hose)
    {
        if (currentObjective == ObjectiveType.GrabFireHose)
        {
            AddObjectiveProgress();
        }
    }

    private void OnFireIgnited(FireNode fireNode)
    {
        if (!fireEmergencyActive || fireNode == null)
        {
            return;
        }

        activeFires.Add(fireNode);
        totalFiresDuringEmergency++;
        targetAmount = Mathf.Max(targetAmount, totalFiresDuringEmergency);
        fireIgniteDelta.Apply(statsIndicator);
        RefreshQuestText();

        if (fireNode.generation >= fireLoseGeneration)
        {
            LoseQuest("fire reached generation " + fireLoseGeneration + ".");
        }
    }

    private void OnFireBurnedOut(FireNode fireNode)
    {
        if (!fireEmergencyActive || fireNode == null)
        {
            return;
        }

        activeFires.Remove(fireNode);
        RefreshQuestText();
    }

    private void OnFireExtinguished(FireNode fireNode)
    {
        if (!fireEmergencyActive || fireNode == null)
        {
            return;
        }

        activeFires.Remove(fireNode);
        extinguishedFiresDuringEmergency++;
        currentAmount = Mathf.Clamp(extinguishedFiresDuringEmergency, 0, targetAmount);
        RefreshQuestText();
        TryCompleteExtinguishObjective();
    }

    private void TryCompleteExtinguishObjective()
    {
        if (currentObjective != ObjectiveType.ExtinguishFire)
        {
            return;
        }

        if (extinguishedFiresDuringEmergency > 0 && activeFires.Count == 0)
        {
            currentAmount = targetAmount;
            RefreshQuestText();
            fireEmergencyActive = false;
            CompleteCurrentObjective();
        }
    }

    private bool HasMoveInput()
    {
        if (HasLegacyAxis("Horizontal", "Vertical", movementInputThreshold))
        {
            return true;
        }

        return HasControllerAxis(InputDeviceCharacteristics.Left, movementInputThreshold);
    }

    private bool HasLookInput()
    {
        if (HasLegacyAxis("Mouse X", "Mouse Y", lookInputThreshold))
        {
            return true;
        }

        return HasControllerAxis(InputDeviceCharacteristics.Right, lookInputThreshold);
    }

    private bool HasRightPrimaryButtonInput()
    {
        controllers.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
            controllers
        );

        for (int i = 0; i < controllers.Count; i++)
        {
            if (controllers[i].TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed)
                && pressed)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleVrSprint()
    {
        if (!enableVrSprint)
        {
            return;
        }

        ResolveContinuousMoveProvider();

        if (continuousMoveProvider == null)
        {
            return;
        }

        bool sprintPressed = HasRightPrimaryButtonInput();
        float targetSpeed = sprintPressed ? sprintSpeed : walkSpeed;
        float speedChange = sprintPressed ? sprintAcceleration : sprintDeceleration;
        float currentSpeed = GetMoveProviderSpeed(walkSpeed);
        float nextSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            Mathf.Max(0.01f, speedChange) * Time.deltaTime
        );

        SetMoveProviderSpeed(nextSpeed);
    }

    private void ResolveContinuousMoveProvider()
    {
        if (continuousMoveProvider != null || !autoFindContinuousMoveProvider)
        {
            CacheMoveProviderSpeedAccessors();
            return;
        }

        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null)
            {
                continue;
            }

            Type behaviourType = behaviours[i].GetType();
            if (behaviourType.Name == "ContinuousMoveProvider" || behaviourType.Name == "DynamicMoveProvider")
            {
                continuousMoveProvider = behaviours[i];
                CacheMoveProviderSpeedAccessors();
                SetMoveProviderSpeed(walkSpeed);
                return;
            }
        }
    }

    private void CacheMoveProviderSpeedAccessors()
    {
        if (continuousMoveProvider == null || moveSpeedProperty != null || moveSpeedField != null)
        {
            return;
        }

        Type providerType = continuousMoveProvider.GetType();
        moveSpeedProperty = providerType.GetProperty("moveSpeed", BindingFlags.Instance | BindingFlags.Public);
        moveSpeedField = GetFieldInTypeHierarchy(providerType, "m_MoveSpeed");
    }

    private float GetMoveProviderSpeed(float fallback)
    {
        CacheMoveProviderSpeedAccessors();

        if (continuousMoveProvider == null)
        {
            return fallback;
        }

        if (moveSpeedProperty != null)
        {
            return (float)moveSpeedProperty.GetValue(continuousMoveProvider);
        }

        if (moveSpeedField != null)
        {
            return (float)moveSpeedField.GetValue(continuousMoveProvider);
        }

        return fallback;
    }

    private void SetMoveProviderSpeed(float speed)
    {
        CacheMoveProviderSpeedAccessors();

        if (continuousMoveProvider == null)
        {
            return;
        }

        if (moveSpeedProperty != null)
        {
            moveSpeedProperty.SetValue(continuousMoveProvider, speed);
        }
        else if (moveSpeedField != null)
        {
            moveSpeedField.SetValue(continuousMoveProvider, speed);
        }
    }

    private FieldInfo GetFieldInTypeHierarchy(Type type, string fieldName)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }

    private bool HasLegacyAxis(string xAxis, string yAxis, float threshold)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        float x = Input.GetAxisRaw(xAxis);
        float y = Input.GetAxisRaw(yAxis);
        return new Vector2(x, y).sqrMagnitude >= threshold * threshold;
#else
        return false;
#endif
    }

    private bool HasControllerAxis(InputDeviceCharacteristics hand, float threshold)
    {
        controllers.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            hand | InputDeviceCharacteristics.Controller,
            controllers
        );

        for (int i = 0; i < controllers.Count; i++)
        {
            if (controllers[i].TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis)
                && axis.sqrMagnitude >= threshold * threshold)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshQuestText()
    {
        if (currentObjective == ObjectiveType.RestoreStats)
        {
            bool healthy = statsIndicator != null && statsIndicator.IsHealthy;
            ShowQuestText(objectiveLabel + (healthy ? " (Good)" : " (Plant More)"));
            return;
        }

        ShowQuestText(objectiveLabel + " (" + currentAmount + "/" + targetAmount + ")");
    }

    private void ShowDialog(string message)
    {
        if (dialogText != null)
        {
            dialogText.text = message;
        }

        dialogTween = ShowPanel(
            dialogCanvasGroup,
            dialogPanel,
            dialogVisibleScale,
            dialogTween,
            autoHideDialog,
            () => dialogTween = null
        );
    }

    private void ShowQuestPanel()
    {
        questTween = ShowPanel(
            questCanvasGroup,
            questPanel,
            questVisibleScale,
            questTween,
            false,
            () => questTween = null
        );
    }

    private void ShowQuestText(string message)
    {
        if (questText != null)
        {
            questText.text = message;
        }

        if (questTween == null
            && (questCanvasGroup == null || questCanvasGroup.alpha < 0.99f || !questCanvasGroup.blocksRaycasts))
        {
            ShowQuestPanel();
        }
    }

    private Sequence ShowPanel(
        CanvasGroup group,
        RectTransform panel,
        Vector3 visibleScale,
        Sequence activeTween,
        bool autoHide,
        Action onComplete
    )
    {
        if (group == null)
        {
            return null;
        }

        activeTween?.Kill();

        group.gameObject.SetActive(true);
        group.interactable = true;
        group.blocksRaycasts = true;
        group.alpha = 0f;
        SetPanelScale(panel, group.transform, visibleScale * hiddenPanelScale);

        Sequence tween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(group)
            .Append(group.DOFade(1f, Mathf.Max(0.01f, panelAppearDuration)))
            .Join(GetPanelScaleTween(panel, group.transform, visibleScale, panelAppearDuration))
            .SetEase(panelAppearEase);

        if (autoHide && dialogAutoHideDelay > 0f)
        {
            tween.AppendInterval(dialogAutoHideDelay)
                .Append(group.DOFade(0f, Mathf.Max(0.01f, panelDisappearDuration)))
                .Join(GetPanelScaleTween(panel, group.transform, visibleScale * hiddenPanelScale, panelDisappearDuration))
                .SetEase(panelDisappearEase)
                .OnComplete(() =>
                {
                    group.interactable = false;
                    group.blocksRaycasts = false;
                    onComplete?.Invoke();
                });
        }
        else
        {
            tween.OnComplete(() => onComplete?.Invoke());
        }

        return tween;
    }

    private Sequence HidePanel(
        CanvasGroup group,
        RectTransform panel,
        Vector3 visibleScale,
        Sequence activeTween,
        Action onComplete
    )
    {
        if (group == null)
        {
            return null;
        }

        activeTween?.Kill();
        Sequence tween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(group)
            .Append(group.DOFade(0f, Mathf.Max(0.01f, panelDisappearDuration)))
            .Join(GetPanelScaleTween(panel, group.transform, visibleScale * hiddenPanelScale, panelDisappearDuration))
            .SetEase(panelDisappearEase)
            .OnComplete(() =>
            {
                group.interactable = false;
                group.blocksRaycasts = false;
                onComplete?.Invoke();
            });

        return tween;
    }

    private void HidePanelImmediate(CanvasGroup group, RectTransform panel, Vector3 visibleScale)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        SetPanelScale(panel, group.transform, visibleScale * hiddenPanelScale);
    }

    private void PulseQuestPanel()
    {
        if (questPanel == null)
        {
            return;
        }

        questPulseTween?.Kill();
        questPulseTween = questPanel
            .DOPunchScale(Vector3.one * 0.05f, 0.22f, 6, 0.6f)
            .SetUpdate(true)
            .OnComplete(() => questPulseTween = null);
    }

    private Tween GetPanelScaleTween(RectTransform panel, Transform fallback, Vector3 targetScale, float duration)
    {
        if (panel != null)
        {
            return panel.DOScale(targetScale, Mathf.Max(0.01f, duration));
        }

        return fallback.DOScale(targetScale, Mathf.Max(0.01f, duration));
    }

    private void SetPanelScale(RectTransform panel, Transform fallback, Vector3 scale)
    {
        if (panel != null)
        {
            panel.localScale = scale;
            return;
        }

        fallback.localScale = scale;
    }

    private void ResolveReferences()
    {
        if (statsIndicator == null)
        {
            statsIndicator = StatsIndicatorUI.Instance;
        }

        if (fireSpawner == null)
        {
            fireSpawner = FireSpawner.Instance;
        }

        if (winUIHandler == null)
        {
            winUIHandler = FindObjectOfType<WinUIHandler>(true);
        }

        if (loseUIHandler == null)
        {
            loseUIHandler = FindObjectOfType<LoseUIHandler>(true);
        }

        if (dialogCanvasGroup == null && dialogPanel != null)
        {
            dialogCanvasGroup = EnsureCanvasGroup(dialogPanel.gameObject);
        }

        if (questCanvasGroup == null && questPanel != null)
        {
            questCanvasGroup = EnsureCanvasGroup(questPanel.gameObject);
        }

        if (questText != null)
        {
            if (questPanel == null)
            {
                questPanel = questText.rectTransform;
            }

            if (questCanvasGroup == null)
            {
                questCanvasGroup = EnsureCanvasGroup(questText.gameObject);
            }
        }

        if (dialogPanel == null && dialogCanvasGroup != null)
        {
            dialogPanel = dialogCanvasGroup.GetComponent<RectTransform>();
        }

        if (questPanel == null && questCanvasGroup != null)
        {
            questPanel = questCanvasGroup.GetComponent<RectTransform>();
        }
    }

    private void CachePanelScales()
    {
        if (questText != null && questPanel == null)
        {
            questPanel = questText.rectTransform;
        }

        if (dialogPanel != null)
        {
            dialogVisibleScale = dialogPanel.localScale;
        }
        else if (dialogCanvasGroup != null)
        {
            dialogVisibleScale = dialogCanvasGroup.transform.localScale;
        }

        if (questPanel != null)
        {
            questVisibleScale = questPanel.localScale;
        }
        else if (questCanvasGroup != null)
        {
            questVisibleScale = questCanvasGroup.transform.localScale;
        }
    }

    private CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private void ClearAllFireObjects()
    {
        FireNode[] fires = FindObjectsOfType<FireNode>(true);

        for (int i = 0; i < fires.Length; i++)
        {
            if (fires[i] != null)
            {
                Destroy(fires[i].gameObject);
            }
        }

        activeFires.Clear();
        totalFiresDuringEmergency = 0;
        extinguishedFiresDuringEmergency = 0;
        fireEmergencyActive = false;
    }

    private string BuildTargetLabel(string verb, int target, string objectName)
    {
        return verb + " " + Mathf.Max(1, target) + " " + objectName;
    }
}
