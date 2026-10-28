using UnityEngine;
using System.Collections; // Coroutine (Timer) ke liye zaroori hai

public class OverManager : MonoBehaviour
{
    [Header("Bowler Setup")]
    public GameObject[] bowlers; 
    public int ballCount = 0;
    
    [Header("Timers")]
    public float overBreakTime = 15f; // Over ke baad 15 second ka gap

    void Awake()
    {
        // Auto-find all bowlers in scene if unassigned or broken
        if (bowlers == null || bowlers.Length == 0)
        {
            var allManagers = FindObjectsByType<BowlingManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
        ballCount++;
        Debug.Log("OverManager: Ball number " + ballCount + "/6 recorded.");

        if (ballCount >= 6)
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

    void SwitchToRandomBowler()
    {
        if (bowlers == null || bowlers.Length == 0) return;

        SetAllBowlers(false);

        int randomIndex = Random.Range(0, bowlers.Length);
        if (bowlers[randomIndex] != null)
        {
            bowlers[randomIndex].SetActive(true);
            Debug.Log("OverManager: Naya Bowler Aaya -> " + bowlers[randomIndex].name);
        }
        else
        {
            Debug.LogWarning("OverManager: bowler slot " + randomIndex + " is empty.");
        }
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
}
