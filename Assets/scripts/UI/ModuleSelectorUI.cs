using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ModuleSelectorUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject moduleButtonPrefab;
    public Transform buttonContainer;
    public TextMeshProUGUI playerNameDisplay;
    public TextMeshProUGUI levelDisplay;
    public TextMeshProUGUI totalSessionsDisplay;
    public GameObject moduleInfoPanel;
    public TextMeshProUGUI moduleInfoText;
    public Button startModuleButton;
    
    [Header("Module Data")]
    private string[] moduleNames = {
        "⚡ Reaction Time & Anticipation",
        "👁️ Gaze Training / Visual Focus",
        "🤝 Hand-Eye Coordination",
        "👀 Peripheral Awareness",
        "🎯 Smooth Pursuit",
        "🔄 Saccadic Movement",
        "📏 Vergence",
        "🔍 Visual Acuity",
        "🌓 Contrast Sensitivity",
        "📐 Depth Perception",
        "🧭 Spatial Awareness",
        "⏱️ Predictive Timing",
        "🧠 Visual Memory & Decision Making"
    };
    
    private string[] moduleDescriptions = {
        "Predict ball delivery before halfway point",
        "Maintain fixation during distractions",
        "Intercept moving targets with precision",
        "Detect off-axis movements",
        "Follow moving objects smoothly",
        "Rapid focus shifts between targets",
        "Track depth as ball approaches",
        "Clarity under motion",
        "Detect ball in varying light",
        "Judge distance and bounce",
        "Maintain position awareness",
        "Time interception perfectly",
        "Recall plays and decide quickly"
    };
    
    private Color[] moduleColors = {
        new Color(1f, 0.2f, 0.2f),
        new Color(0.2f, 0.4f, 1f),
        new Color(0.2f, 0.8f, 0.2f),
        new Color(1f, 0.8f, 0f),
        new Color(0f, 0.8f, 0.8f),
        new Color(0.8f, 0f, 0.8f),
        new Color(0.5f, 0.5f, 0.5f),
        new Color(0.2f, 0.6f, 0.8f),
        new Color(0.8f, 0.4f, 0f),
        new Color(0f, 0.6f, 0.4f),
        new Color(0.6f, 0.2f, 0.6f),
        new Color(0.8f, 0.8f, 0.2f),
        new Color(0.2f, 0.8f, 0.6f)
    };
    
    private int selectedIndex = -1;
    private Canvas menuCanvas;
    
    void Awake()
    {
        menuCanvas = GetComponentInParent<Canvas>();
    }

    void Start()
    {
        UpdatePlayerInfo();
        CreateModuleButtons();
        
        if (moduleInfoPanel != null)
            moduleInfoPanel.SetActive(false);
        
        if (startModuleButton != null)
            startModuleButton.onClick.AddListener(StartSelectedModule);
        
        // Subscribe to GameManager events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnModuleEnded += OnModuleEnded;
        }
    }

    void Update()
    {
        // Toggle Module Menu on 'M' key or 'Escape'
        if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        bool targetState = false;
        if (menuCanvas != null)
        {
            menuCanvas.enabled = !menuCanvas.enabled;
            targetState = menuCanvas.enabled;
        }
        else
        {
            gameObject.SetActive(!gameObject.activeSelf);
            targetState = gameObject.activeSelf;
        }

        if (targetState)
        {
            UpdatePlayerInfo();
            CreateModuleButtons();
        }
    }
    
    void UpdatePlayerInfo()
    {
        if (GameManager.Instance != null)
        {
            if (playerNameDisplay != null) playerNameDisplay.text = $"🏏 {GameManager.Instance.PlayerName}";
            if (levelDisplay != null) levelDisplay.text = $"Level: {GameManager.Instance.PlayerLevel}";
            if (totalSessionsDisplay != null) totalSessionsDisplay.text = $"Sessions: {GameManager.Instance.TotalSessions}";
        }
    }
    
    void CreateModuleButtons()
    {
        if (buttonContainer == null || moduleButtonPrefab == null) return;

        // Clear existing
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);
        
        for (int i = 0; i < moduleNames.Length; i++)
        {
            int index = i;
            GameObject buttonObj = Instantiate(moduleButtonPrefab, buttonContainer);
            
            Button btn = buttonObj.GetComponent<Button>();
            TextMeshProUGUI[] texts = buttonObj.GetComponentsInChildren<TextMeshProUGUI>();
            Image bgImage = buttonObj.GetComponent<Image>();
            
            if (texts.Length >= 2)
            {
                texts[0].text = moduleNames[i];
                texts[1].text = moduleDescriptions[i];
            }
            
            if (bgImage != null)
                bgImage.color = moduleColors[i % moduleColors.Length];
            
            btn.onClick.AddListener(() => SelectModule(index));
        }
    }
    
    string GetModuleKey(int index)
    {
        string[] keys = {
            "ReactionTime", "GazeTraining", "HandEyeCoordination",
            "PeripheralAwareness", "SmoothPursuit", "SaccadicMovement",
            "Vergence", "VisualAcuity", "ContrastSensitivity",
            "DepthPerception", "SpatialAwareness", "PredictiveTiming",
            "VisualMemory"
        };
        if (index >= 0 && index < keys.Length)
            return keys[index];
        return "ReactionTime";
    }
    
    void SelectModule(int index)
    {
        selectedIndex = index;
        
        if (moduleInfoPanel != null)
        {
            moduleInfoPanel.SetActive(true);
            string key = GetModuleKey(index);
            float avgScore = GameManager.Instance != null ? GameManager.Instance.GetAverageScore(key) : 0f;
            float avgRT = GameManager.Instance != null ? GameManager.Instance.GetAverageReactionTime(key) : 0f;
            
            if (moduleInfoText != null)
            {
                moduleInfoText.text = $"<size=36>{moduleNames[index]}</size>\n\n" +
                                      $"{moduleDescriptions[index]}\n\n" +
                                      $"📊 Best Score: {(avgScore > 0 ? avgScore.ToString("F0") : "Not played yet")}\n" +
                                      $"⏱️ Avg Reaction: {(avgRT > 0 ? avgRT.ToString("F3") + "s" : "N/A")}";
            }
        }
        
        Debug.Log($"Selected Module: {moduleNames[index]}");
    }
    
    void StartSelectedModule()
    {
        if (selectedIndex < 0)
        {
            Debug.LogWarning("Please select a module first!");
            return;
        }

        string key = GetModuleKey(selectedIndex);
        string name = moduleNames[selectedIndex];

        // 1. Notify GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartModule(selectedIndex);
        }

        // 2. Hide Module Menu so player can participate in the drill
        if (moduleInfoPanel != null)
            moduleInfoPanel.SetActive(false);

        if (menuCanvas != null)
            menuCanvas.enabled = false;
        else
            gameObject.SetActive(false);

        // 3. Announce drill in Cricket HUD
        if (CricketMatchManager.Instance != null)
        {
            CricketMatchManager.Instance.ShowMessage($"TRAINING STARTED\n{name}", Color.cyan, 3.5f);
        }

        // 4. Activate specific module logic
        if (selectedIndex == 0) // Reaction Time & Anticipation
        {
            var rtModule = FindAnyObjectByType<Module1_ReactionTime>();
            if (rtModule == null)
            {
                var go = new GameObject("Module1_ReactionTime");
                rtModule = go.AddComponent<Module1_ReactionTime>();
            }
            rtModule.StartTrainingSession();
        }
        else
        {
            // Configure environmental bowling drill for the cognitive category
            StartCognitiveDrill(selectedIndex, key);
        }
        
        Debug.Log($"🚀 Started Module {selectedIndex + 1}: {name} (Press 'M' to reopen menu)");
    }

    void StartCognitiveDrill(int index, string moduleKey)
    {
        var bowlingManager = FindAnyObjectByType<BowlingManager>();
        if (bowlingManager != null)
        {
            // Tailor the bowler to the cognitive training domain:
            switch (moduleKey)
            {
                case "DepthPerception":
                    // Yorkers and Bouncers test extreme depth
                    bowlingManager.betweenBallsDelay = 3.5f;
                    break;
                case "PredictiveTiming":
                    // High-speed deliveries test timing
                    bowlingManager.betweenBallsDelay = 3.0f;
                    break;
                case "GazeTraining":
                case "SmoothPursuit":
                    // High spin balls test tracking moving objects
                    bowlingManager.betweenBallsDelay = 4.0f;
                    break;
                default:
                    bowlingManager.betweenBallsDelay = 3.5f;
                    break;
            }
        }
    }

    void OnModuleEnded(string moduleName)
    {
        // Re-display menu upon drill completion
        if (menuCanvas != null)
            menuCanvas.enabled = true;
        else
            gameObject.SetActive(true);

        UpdatePlayerInfo();
        CreateModuleButtons();

        if (CricketMatchManager.Instance != null)
        {
            CricketMatchManager.Instance.ShowMessage($"DRILL COMPLETED!\n{moduleName}", Color.green, 3.0f);
        }
    }
    
    public void ShowAllStats()
    {
        if (AnalyticsEngine.Instance == null) return;
        
        OverallStats stats = AnalyticsEngine.Instance.GetOverallStats();
        string statsText = "<size=36>📊 Overall Stats</size>\n\n";
        statsText += $"Total Sessions: {stats.totalSessions}\n";
        statsText += $"Average Score: {stats.averageOverallScore:F0}\n";
        statsText += $"Player Level: {stats.playerLevel}\n\n";
        
        foreach (var module in stats.moduleStats)
        {
            statsText += $"{module.moduleName}: {module.averageScore:F0}";
            if (module.improvement > 0)
                statsText += $" ▲ +{module.improvement:F0}";
            statsText += "\n";
        }
        
        Debug.Log(statsText);
    }
    
    public void ResetData()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetPlayerData();
            UpdatePlayerInfo();
            CreateModuleButtons();
            Debug.Log("Data reset!");
        }
    }
}
