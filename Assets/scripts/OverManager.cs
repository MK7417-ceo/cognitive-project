using UnityEngine;
using System.Collections; // Coroutine (Timer) ke liye zaroori hai
using System.Collections.Generic;

public class OverManager : MonoBehaviour
{
    public static OverManager Instance { get; private set; }

    [Header("Bowler Setup")]
    public GameObject[] bowlers; 
    public int ballCount = 0;
    
    [Header("Timers")]
    public float overBreakTime = 15f; // Over ke baad 15 second ka gap
    [Min(1)] public int ballsPerOver = 6;
    int currentBowlerIndex = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }
        Instance = this;

        // Auto-find all bowlers in scene if unassigned or broken
        if (bowlers == null || bowlers.Length == 0)
        {
            var allManagers = FindObjectsByType<BowlingManager>(FindObjectsInactive.Include);
            if (allManagers != null && allManagers.Length > 0)
            {
                bowlers = new GameObject[allManagers.Length];
                for (int i = 0; i < allManagers.Length; i++)
                    bowlers[i] = allManagers[i].gameObject;
            }
        }

        // Disable all duplicate bowlers on Awake immediately, then activate only one.
        // This prevents 8 bowlers from running simultaneously or fighting each other.
        if (bowlers != null && bowlers.Length > 0)
        {
            SetAllBowlers(false);
            SwitchToRandomBowler();
        }
    }

    void Start()
    {
        if (bowlers == null || bowlers.Length == 0)
        {
            Debug.LogError("OverManager: no bowlers assigned or found in the scene.");
            enabled = false;
            return;
        }
    }

    public void RecordBall()
    {
        if (!enabled || (bowlers != null && bowlers.Length == 0)) return;
        ballCount++;
        Debug.Log("OverManager: Ball number " + ballCount + "/" + ballsPerOver + " recorded.");

        if (ballCount >= Mathf.Max(1, ballsPerOver))
        {
            ballCount = 0; // Agle over ke liye reset
            StartCoroutine(OverBreakRoutine());
        }
    }

    IEnumerator OverBreakRoutine()
    {
        // 1. Akhri ball ke baad 1 second wait karo (taaki bowler apna follow-through poora kar sake)
        yield return new WaitForSeconds(1f);

        // 2. Current bowler ko screen se hata do
        SetAllBowlers(false);

        // 3. Bacha hua time wait karo (total overBreakTime ka gap)
        yield return new WaitForSeconds(Mathf.Max(0f, overBreakTime - 1f));

        // 4. Break khatam, ab naya bowler ground par aayega
        SwitchToRandomBowler();
    }

    /// <summary>Start a new innings with six fresh legal balls in the over.</summary>
    public void PrepareForNewInnings()
    {
        if (bowlers == null || bowlers.Length == 0) return;

        StopAllCoroutines();
        ballCount = 0;
        SwitchToRandomBowler();

        if (currentBowlerIndex >= 0 && currentBowlerIndex < bowlers.Length &&
            bowlers[currentBowlerIndex] != null)
        {
            StartCoroutine(BeginSelectedBowlerNewInnings(bowlers[currentBowlerIndex]));
        }
    }

    IEnumerator BeginSelectedBowlerNewInnings(GameObject selectedBowler)
    {
        // A bowler which was inactive when the scene loaded runs Start only
        // after activation; wait one frame so its run-up origin is captured.
        yield return null;
        if (selectedBowler == null || !selectedBowler.activeInHierarchy) yield break;
        var manager = selectedBowler.GetComponent<BowlingManager>();
        if (manager != null) manager.BeginNewInnings();
    }

    void SwitchToRandomBowler()
    {
        if (bowlers == null || bowlers.Length == 0) return;

        SetAllBowlers(false);

        var candidates = new List<int>(bowlers.Length);
        for (int i = 0; i < bowlers.Length; i++)
            if (bowlers[i] != null && (bowlers.Length == 1 || i != currentBowlerIndex))
                candidates.Add(i);

        // If only the previous bowler is configured, keep that one available.
        if (candidates.Count == 0 && currentBowlerIndex >= 0 &&
            currentBowlerIndex < bowlers.Length && bowlers[currentBowlerIndex] != null)
            candidates.Add(currentBowlerIndex);

        if (candidates.Count == 0)
        {
            Debug.LogWarning("OverManager: no valid bowler object is assigned.");
            return;
        }

        currentBowlerIndex = candidates[Random.Range(0, candidates.Count)];
        bowlers[currentBowlerIndex].SetActive(true);
        Debug.Log("OverManager: Naya Bowler Aaya -> " + bowlers[currentBowlerIndex].name);
    }

    void SetAllBowlers(bool active)
    {
        if (bowlers == null) return;
        for (int i = 0; i < bowlers.Length; i++)
        {
            if (bowlers[i] != null)
                bowlers[i].SetActive(active);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
