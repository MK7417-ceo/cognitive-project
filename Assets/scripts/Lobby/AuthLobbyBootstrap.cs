using UnityEngine;

/// <summary>
/// Auto-starts authentication + lobby when a cricket scene loads.
///
/// Same pattern as CricketMatchBootstrap: RuntimeInitialize AfterSceneLoad
/// runs before any MonoBehaviour Start(), so VRLobbyManager.Awake() can
/// deactivate the bowlers before their first Bowl trigger fires.
/// If a VRLobbyManager already exists (placed in the scene by hand),
/// this does nothing — no duplicates.
/// No scene edits are needed to use this: opening SampleScene and
/// pressing Play shows the Auth panel first.
/// </summary>
public static class AuthLobbyBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (Object.FindAnyObjectByType<VRLobbyManager>() != null) return;

        bool isCricketScene =
            Object.FindAnyObjectByType<BowlingManager>(FindObjectsInactive.Include) != null ||
            Object.FindAnyObjectByType<OverManager>(FindObjectsInactive.Include) != null ||
            GameObject.Find("Bowler") != null;

        if (!isCricketScene) return;

        var go = new GameObject("AuthLobby");
        go.AddComponent<VRLobbyManager>();
        Debug.Log("AuthLobbyBootstrap: auth + lobby started.");
    }
}
