using UnityEngine;

/// <summary>
/// Auto-starts the cricket match systems when the cricket scene loads.
/// Robust against inactive game objects, bowler rotation, and missing scene references.
/// </summary>
public static class CricketMatchBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (Object.FindAnyObjectByType<CricketMatchManager>() != null) return;

        // Check if cricket match components exist in the scene (even if currently inactive)
        bool isCricketScene =
            Object.FindAnyObjectByType<BowlingManager>(FindObjectsInactive.Include) != null ||
            Object.FindAnyObjectByType<OverManager>(FindObjectsInactive.Include) != null ||
            Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include) != null ||
            GameObject.Find("Bowler") != null;

        if (!isCricketScene) return;

        var go = new GameObject("CricketMatchManager");
        go.AddComponent<CricketMatchManager>();
        Debug.Log("CricketMatchBootstrap: match systems started successfully.");
    }
}
