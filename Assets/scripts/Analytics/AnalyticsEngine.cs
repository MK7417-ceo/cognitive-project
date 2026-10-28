using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AnalyticsEngine : MonoBehaviour
{
    public static AnalyticsEngine Instance;
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    public ModuleStats GetModuleStats(string moduleName)
    {
        ModuleStats stats = new ModuleStats();
        stats.moduleName = moduleName;
        
        if (GameManager.Instance == null)
            return stats;
        
        stats.sessions = GameManager.Instance.GetModuleSessions(moduleName);
        stats.averageScore = GameManager.Instance.GetAverageScore(moduleName);
        stats.bestScore = GameManager.Instance.GetBestScore(moduleName);
        stats.averageReactionTime = GameManager.Instance.GetAverageReactionTime(moduleName);
        stats.averageAccuracy = GameManager.Instance.GetAverageAccuracy(moduleName);
        
        // Calculate improvement trend
        if (GameManager.Instance.ModuleScores.ContainsKey(moduleName) && 
            GameManager.Instance.ModuleScores[moduleName].Count >= 3)
        {
            var scores = GameManager.Instance.ModuleScores[moduleName];
            int count = scores.Count;
            float firstHalf = scores.Take(count / 2).Average();
            float secondHalf = scores.Skip(count / 2).Average();
            stats.improvement = secondHalf - firstHalf;
        }
        
        return stats;
    }
    
    public OverallStats GetOverallStats()
    {
        OverallStats stats = new OverallStats();
        
        if (GameManager.Instance == null)
            return stats;
        
        string[] moduleNames = {
            "ReactionTime", "GazeTraining", "HandEyeCoordination",
            "PeripheralAwareness", "SmoothPursuit", "SaccadicMovement",
            "Vergence", "VisualAcuity", "ContrastSensitivity",
            "DepthPerception", "SpatialAwareness", "PredictiveTiming",
            "VisualMemory"
        };
        
        float totalScore = 0;
        int moduleCount = 0;
        
        foreach (string name in moduleNames)
        {
            float avg = GameManager.Instance.GetAverageScore(name);
            if (avg > 0)
            {
                totalScore += avg;
                moduleCount++;
                stats.moduleStats.Add(GetModuleStats(name));
            }
        }
        
        stats.totalSessions = GameManager.Instance.TotalSessions;
        stats.averageOverallScore = moduleCount > 0 ? totalScore / moduleCount : 0;
        stats.playerLevel = GameManager.Instance.PlayerLevel;
        
        return stats;
    }
}

[System.Serializable]
public class ModuleStats
{
    public string moduleName;
    public int sessions;
    public float averageScore;
    public float bestScore;
    public float averageReactionTime;
    public float averageAccuracy;
    public float improvement;
}

[System.Serializable]
public class OverallStats
{
    public int totalSessions;
    public float averageOverallScore;
    public int playerLevel;
    public List<ModuleStats> moduleStats = new List<ModuleStats>();
}