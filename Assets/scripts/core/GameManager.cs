using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    [Header("Player Settings")]
    public string PlayerName = "Player1";
    public int PlayerLevel = 1;
    public int TotalSessions = 0;
    
    [Header("Module Data")]
    // Runtime dictionaries are persisted through JSON below, not Unity's
    // unsupported dictionary serializer. Properties keep them out of the
    // Inspector while preserving the same JSON field names.
    public Dictionary<string, List<float>> ModuleScores { get; private set; } = new Dictionary<string, List<float>>();
    public Dictionary<string, List<float>> ModuleReactionTimes { get; private set; } = new Dictionary<string, List<float>>();
    public Dictionary<string, List<float>> ModuleAccuracy { get; private set; } = new Dictionary<string, List<float>>();
    
    [Header("Session Info")]
    public bool IsTrainingActive = false;
    public int CurrentModuleIndex = -1;
    public float SessionStartTime;
    public string CurrentModuleName = "";
    
    private string dataPath;
    private string settingsPath;
    
    // Module names (13 modules)
    private string[] moduleNames = {
        "ReactionTime", "GazeTraining", "HandEyeCoordination",
        "PeripheralAwareness", "SmoothPursuit", "SaccadicMovement",
        "Vergence", "VisualAcuity", "ContrastSensitivity",
        "DepthPerception", "SpatialAwareness", "PredictiveTiming",
        "VisualMemory"
    };
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        dataPath = Application.persistentDataPath + "/player_data.json";
        settingsPath = Application.persistentDataPath + "/settings.json";
        InitializeModules();
        LoadPlayerData();
        LoadSettings();
        
        Debug.Log($"🎮 GameManager Initialized! Data Path: {dataPath}");
        Debug.Log($"👤 Player: {PlayerName}, Level: {PlayerLevel}");
    }
    
    void Start()
    {
        SessionStartTime = Time.time;
    }
    
    void InitializeModules()
    {
        foreach (string name in moduleNames)
        {
            if (!ModuleScores.ContainsKey(name))
                ModuleScores[name] = new List<float>();
            
            if (!ModuleReactionTimes.ContainsKey(name))
                ModuleReactionTimes[name] = new List<float>();
            
            if (!ModuleAccuracy.ContainsKey(name))
                ModuleAccuracy[name] = new List<float>();
        }
    }
    
    public void StartModule(int index)
    {
        if (index < 0 || index >= moduleNames.Length)
        {
            Debug.LogError($"Invalid module index: {index}");
            return;
        }
        
        CurrentModuleIndex = index;
        CurrentModuleName = moduleNames[index];
        IsTrainingActive = true;
        SessionStartTime = Time.time;
        
        Debug.Log($"🚀 Starting Module: {CurrentModuleName}");
        OnModuleStarted?.Invoke(CurrentModuleName);
    }
    
    public void EndModule()
    {
        IsTrainingActive = false;
        TotalSessions++;
        SavePlayerData();
        Debug.Log($"✅ Module {CurrentModuleName} completed!");
        OnModuleEnded?.Invoke(CurrentModuleName);
        CurrentModuleName = "";
        CurrentModuleIndex = -1;
    }
    
    public void RecordScore(string moduleName, float score, float reactionTime = 0f, float accuracy = 0f)
    {
        if (!ModuleScores.ContainsKey(moduleName))
        {
            Debug.LogWarning($"Module {moduleName} not found!");
            return;
        }
        
        ModuleScores[moduleName].Add(score);
        
        if (reactionTime > 0)
            ModuleReactionTimes[moduleName].Add(reactionTime);
        
        if (accuracy > 0)
            ModuleAccuracy[moduleName].Add(accuracy);
        
        // Calculate and log stats
        float avgScore = GetAverageScore(moduleName);
        float avgRT = GetAverageReactionTime(moduleName);
        float avgAcc = GetAverageAccuracy(moduleName);
        
        Debug.Log($"📊 {moduleName}: Score={score:F2} | Avg={avgScore:F2} | RT={reactionTime:F3}s | Acc={accuracy:F1}%");
        
        // Auto-save after every 10 records
        if (ModuleScores[moduleName].Count % 10 == 0)
        {
            SavePlayerData();
        }
        
        // Event for UI updates
        OnScoreRecorded?.Invoke(moduleName, score, avgScore, avgRT, avgAcc);
    }
    
    public float GetAverageScore(string moduleName)
    {
        if (!ModuleScores.ContainsKey(moduleName) || ModuleScores[moduleName].Count == 0)
            return 0f;
        return ModuleScores[moduleName].Average();
    }
    
    public float GetAverageReactionTime(string moduleName)
    {
        if (!ModuleReactionTimes.ContainsKey(moduleName) || ModuleReactionTimes[moduleName].Count == 0)
            return 0f;
        return ModuleReactionTimes[moduleName].Average();
    }
    
    public float GetAverageAccuracy(string moduleName)
    {
        if (!ModuleAccuracy.ContainsKey(moduleName) || ModuleAccuracy[moduleName].Count == 0)
            return 0f;
        return ModuleAccuracy[moduleName].Average();
    }
    
    public float GetBestScore(string moduleName)
    {
        if (!ModuleScores.ContainsKey(moduleName) || ModuleScores[moduleName].Count == 0)
            return 0f;
        return ModuleScores[moduleName].Max();
    }
    
    public int GetModuleSessions(string moduleName)
    {
        if (!ModuleScores.ContainsKey(moduleName))
            return 0;
        return ModuleScores[moduleName].Count;
    }
    
    public void SavePlayerData()
    {
        try
        {
            PlayerData data = new PlayerData();
            data.playerName = PlayerName;
            data.playerLevel = PlayerLevel;
            data.totalSessions = TotalSessions;
            data.moduleScores = ModuleScores;
            data.moduleReactionTimes = ModuleReactionTimes;
            data.moduleAccuracy = ModuleAccuracy;
            data.lastPlayed = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(dataPath, json);
            
            Debug.Log($"💾 Data saved: {dataPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Save error: {e.Message}");
        }
    }
    
    public void LoadPlayerData()
    {
        if (!File.Exists(dataPath))
        {
            Debug.Log("📂 No saved data found. Starting fresh.");
            return;
        }
        
        try
        {
            string json = File.ReadAllText(dataPath);
            PlayerData data = JsonConvert.DeserializeObject<PlayerData>(json);
            
            if (data != null)
            {
                PlayerName = data.playerName;
                PlayerLevel = data.playerLevel;
                TotalSessions = data.totalSessions;
                
                // Merge loaded data
                foreach (var kvp in data.moduleScores)
                {
                    if (ModuleScores.ContainsKey(kvp.Key))
                        ModuleScores[kvp.Key] = kvp.Value;
                }
                
                foreach (var kvp in data.moduleReactionTimes)
                {
                    if (ModuleReactionTimes.ContainsKey(kvp.Key))
                        ModuleReactionTimes[kvp.Key] = kvp.Value;
                }
                
                foreach (var kvp in data.moduleAccuracy)
                {
                    if (ModuleAccuracy.ContainsKey(kvp.Key))
                        ModuleAccuracy[kvp.Key] = kvp.Value;
                }
                
                Debug.Log($"📂 Loaded data for: {PlayerName} (Level {PlayerLevel})");
                Debug.Log($"📊 Total sessions: {TotalSessions}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Load error: {e.Message}");
        }
    }
    
    public void SaveSettings()
    {
        SettingsData settings = new SettingsData();
        settings.playerName = PlayerName;
        settings.playerLevel = PlayerLevel;
        
        string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(settingsPath, json);
    }
    
    public void LoadSettings()
    {
        if (!File.Exists(settingsPath)) return;
        
        try
        {
            string json = File.ReadAllText(settingsPath);
            SettingsData settings = JsonConvert.DeserializeObject<SettingsData>(json);
            if (settings != null)
            {
                PlayerName = settings.playerName;
                PlayerLevel = settings.playerLevel;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Settings load error: {e.Message}");
        }
    }
    
    public void ResetPlayerData()
    {
        InitializeModules();
        TotalSessions = 0;
        SavePlayerData();
        Debug.Log("🗑️ All data reset!");
    }
    
    void OnApplicationQuit()
    {
        SavePlayerData();
    }
    
    // Events for UI updates
    public System.Action<string> OnModuleStarted;
    public System.Action<string> OnModuleEnded;
    public System.Action<string, float, float, float, float> OnScoreRecorded;
}

[System.Serializable]
public class PlayerData
{
    public string playerName = "Player1";
    public int playerLevel = 1;
    public int totalSessions = 0;
    public string lastPlayed = "";
    public Dictionary<string, List<float>> moduleScores { get; set; } = new Dictionary<string, List<float>>();
    public Dictionary<string, List<float>> moduleReactionTimes { get; set; } = new Dictionary<string, List<float>>();
    public Dictionary<string, List<float>> moduleAccuracy { get; set; } = new Dictionary<string, List<float>>();
}

[System.Serializable]
public class SettingsData
{
    public string playerName = "Player1";
    public int playerLevel = 1;
    public float masterVolume = 0.8f;
    public bool useEyeTracking = false;
}
