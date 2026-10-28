using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;

public class DataLogger : MonoBehaviour
{
    private string logPath;
    private List<LogEntry> sessionLogs = new List<LogEntry>();
    
    void Start()
    {
        logPath = Application.persistentDataPath + "/session_logs/";
        if (!Directory.Exists(logPath))
            Directory.CreateDirectory(logPath);
    }
    
    public void LogEvent(string moduleName, string eventType, float value1 = 0f, float value2 = 0f, string note = "")
    {
        LogEntry entry = new LogEntry();
        entry.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        entry.moduleName = moduleName;
        entry.eventType = eventType;
        entry.value1 = value1;
        entry.value2 = value2;
        entry.note = note;
        entry.sessionID = GameManager.Instance != null ? GameManager.Instance.TotalSessions.ToString() : "0";
        
        sessionLogs.Add(entry);
        
        // Save every 50 logs
        if (sessionLogs.Count >= 50)
            SaveLogs();
    }
    
    public void SaveLogs()
    {
        if (sessionLogs.Count == 0) return;
        
        string fileName = $"session_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
        string fullPath = Path.Combine(logPath, fileName);
        
        try
        {
            string json = JsonConvert.SerializeObject(sessionLogs, Formatting.Indented);
            File.WriteAllText(fullPath, json);
            Debug.Log($"📝 Logs saved: {fullPath} ({sessionLogs.Count} entries)");
            sessionLogs.Clear();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Log save error: {e.Message}");
        }
    }
    
    void OnApplicationQuit()
    {
        SaveLogs();
    }
}

[System.Serializable]
public class LogEntry
{
    public string timestamp;
    public string sessionID;
    public string moduleName;
    public string eventType;
    public float value1;
    public float value2;
    public string note;
}