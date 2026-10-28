using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Module 1: Reaction Time & Anticipation Training.
/// Measures player latency from visual stimulus (ball release or visual cue)
/// to player interaction (swing, trigger, or click).
/// Feeds reaction data into GameManager and DataLogger.
/// </summary>
public class Module1_ReactionTime : MonoBehaviour
{
    public static Module1_ReactionTime Instance { get; private set; }

    [Header("Trial Configuration")]
    [Tooltip("Total number of reaction trials in one session")]
    public int totalTrials = 6;
    [Tooltip("Minimum random delay before stimulus (seconds)")]
    public float minDelay = 1.5f;
    [Tooltip("Maximum random delay before stimulus (seconds)")]
    public float maxDelay = 3.5f;

    [Header("Scoring Thresholds (seconds)")]
    public float eliteThreshold = 0.22f;    // < 220ms: Elite
    public float goodThreshold = 0.35f;     // < 350ms: Good
    public float averageThreshold = 0.50f;  // < 500ms: Average

    [Header("State")]
    public int currentTrial = 0;
    public bool isSessionActive = false;
    public bool isWaitingForStimulus = false;
    public bool isStimulusActive = false;

    private float stimulusTimestamp;
    private List<float> sessionReactionTimes = new List<float>();
    private Coroutine trialCoroutine;

    [Header("Events")]
    public UnityEvent<int, int> OnTrialStarted;       // trialIndex, totalTrials
    public UnityEvent OnStimulusTriggered;
    public UnityEvent<float, string> OnReactionRecorded; // reactionTime, rating
    public UnityEvent<float, float> OnSessionCompleted;  // avgReactionTime, finalScore

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Start a full Reaction Time training session.
    /// </summary>
    public void StartTrainingSession()
    {
        currentTrial = 0;
        sessionReactionTimes.Clear();
        isSessionActive = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartModule(0); // Module 0: ReactionTime
        }

        Debug.Log("⚡ Module 1 (Reaction Time) started!");
        NextTrial();
    }

    /// <summary>
    /// Progress to the next trial with randomized anticipation delay.
    /// </summary>
    public void NextTrial()
    {
        if (currentTrial >= totalTrials)
        {
            FinishSession();
            return;
        }

        currentTrial++;
        isStimulusActive = false;
        isWaitingForStimulus = true;

        OnTrialStarted?.Invoke(currentTrial, totalTrials);

        if (trialCoroutine != null) StopCoroutine(trialCoroutine);
        trialCoroutine = StartCoroutine(StimulusCountdownRoutine());
    }

    IEnumerator StimulusCountdownRoutine()
    {
        float delay = Random.Range(minDelay, maxDelay);
        yield return new WaitForSeconds(delay);

        // Stimulus is triggered (visual / delivery cue)
        TriggerStimulus();
    }

    /// <summary>
    /// Triggers the active stimulus that the player must react to.
    /// Can also be invoked externally by ball release events.
    /// </summary>
    public void TriggerStimulus()
    {
        if (!isSessionActive) return;

        isWaitingForStimulus = false;
        isStimulusActive = true;
        stimulusTimestamp = Time.time;

        OnStimulusTriggered?.Invoke();
        Debug.Log($"⚡ Trial {currentTrial}/{totalTrials}: Stimulus Triggered!");
    }

    void Update()
    {
        if (!isSessionActive) return;

        // Player input detection (Space, Left Click, or VR trigger)
        if (isStimulusActive)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                RecordReaction();
            }
        }
        else if (isWaitingForStimulus)
        {
            // False start / early reaction
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                OnFalseStart();
            }
        }
    }

    /// <summary>
    /// Records reaction latency when the player initiates a reaction.
    /// Can be called by BatSwingTracker or external interactors.
    /// </summary>
    public void RecordReaction()
    {
        if (!isStimulusActive) return;

        isStimulusActive = false;
        float reactionTime = Time.time - stimulusTimestamp;
        sessionReactionTimes.Add(reactionTime);

        string rating = GetReactionRating(reactionTime);
        float score = CalculateScore(reactionTime);

        Debug.Log($"⏱️ Trial {currentTrial}: Reaction = {reactionTime:F3}s ({rating}) | Score = {score:F0}");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RecordScore("ReactionTime", score, reactionTime, 100f);
        }

        OnReactionRecorded?.Invoke(reactionTime, rating);

        // Wait brief pause before next trial
        StartCoroutine(PauseBeforeNextTrial());
    }

    void OnFalseStart()
    {
        Debug.LogWarning("❌ False start! Clicked before stimulus.");
        if (trialCoroutine != null) StopCoroutine(trialCoroutine);

        // Penalty entry (1.0s)
        sessionReactionTimes.Add(1.0f);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RecordScore("ReactionTime", 10f, 1.0f, 0f);
        }

        OnReactionRecorded?.Invoke(1.0f, "FALSE START");
        StartCoroutine(PauseBeforeNextTrial());
    }

    IEnumerator PauseBeforeNextTrial()
    {
        yield return new WaitForSeconds(1.5f);
        NextTrial();
    }

    string GetReactionRating(float rt)
    {
        if (rt <= eliteThreshold) return "⚡ ELITE SPEED!";
        if (rt <= goodThreshold) return "🟢 GREAT REACTION!";
        if (rt <= averageThreshold) return "🟡 AVERAGE";
        return "🔴 SLOW";
    }

    float CalculateScore(float rt)
    {
        // 100 points for <= elite, down to 10 points for >= 1.0s
        float clamped = Mathf.Clamp(rt, eliteThreshold, 1.0f);
        float t = (clamped - eliteThreshold) / (1.0f - eliteThreshold);
        return Mathf.Lerp(100f, 10f, t);
    }

    void FinishSession()
    {
        isSessionActive = false;
        isWaitingForStimulus = false;
        isStimulusActive = false;

        float avgRT = 0f;
        if (sessionReactionTimes.Count > 0)
        {
            float sum = 0f;
            foreach (float t in sessionReactionTimes) sum += t;
            avgRT = sum / sessionReactionTimes.Count;
        }

        float finalScore = CalculateScore(avgRT);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndModule();
        }

        Debug.Log($"🎉 Session Finished! Avg Reaction: {avgRT:F3}s | Final Score: {finalScore:F0}");
        OnSessionCompleted?.Invoke(avgRT, finalScore);
    }
}

