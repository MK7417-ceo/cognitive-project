using System;
using System.Collections;
using UnityEngine;

/// <summary>Moves both batters between the two creases for a completed run.</summary>
public sealed class BattingRunController : MonoBehaviour
{
    [Min(1f)] public float runningSpeed = 7.5f;

    Transform striker;
    Transform nonStriker;
    PlayerMovement strikerControls;
    PlayerMovement nonStrikerControls;
    Coroutine activeRun;

    public bool IsRunning => activeRun != null;

    public static BattingRunController Build(PlayerMovement striker, Vector3 nonStrikerCrease)
    {
        if (striker == null) return null;

        var existing = FindAnyObjectByType<BattingRunController>();
        if (existing != null) return existing;

        GameObject root = new GameObject("BattingRunController");
        var controller = root.AddComponent<BattingRunController>();
        controller.striker = striker.transform;
        controller.strikerControls = striker;

        Vector3 start = striker.transform.position;
        nonStrikerCrease.y = start.y;
        Vector3 facing = start - nonStrikerCrease;
        facing.y = 0f;
        Quaternion rotation = facing.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(facing.normalized, Vector3.up)
            : striker.transform.rotation;

        GameObject clone = Instantiate(striker.gameObject, nonStrikerCrease, rotation);
        clone.name = "NonStrikerBatter";
        clone.transform.SetParent(root.transform, true);
        controller.nonStriker = clone.transform;
        controller.nonStrikerControls = clone.GetComponentInChildren<PlayerMovement>(true);

        // This is a visual stand-in for the non-striker. Keep its renderers
        // and Animator, but don't duplicate player input, XR or physics logic.
        foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Rigidbody body in clone.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        Debug.Log("BattingRunController: non-striker visual created; runs require both batters to reach the opposite crease.");
        return controller;
    }

    /// <summary>
    /// Single-player training rule (user directive, Oct 30): strike ALWAYS
    /// stays with the single VR player. SwapStrike() is therefore a no-op
    /// while keepStrikerFixed is true (the default). Without this guard,
    /// the over-end call in CricketMatchManager and the odd-run call would
    /// hand batting input to the non-striker visual clone -- a GameObject
    /// whose behaviours/colliders are deliberately disabled -- and the VR
    /// player would stop being the batsman. Set this to false only if a
    /// real two-batsman mode is ever added.
    /// </summary>
    public bool keepStrikerFixed = true;

    public void SwapStrike()
    {
        if (keepStrikerFixed) return;
        if (striker == null || nonStriker == null) return;

        Transform previousStriker = striker;
        striker = nonStriker;
        nonStriker = previousStriker;

        PlayerMovement previousControls = strikerControls;
        strikerControls = nonStrikerControls;
        nonStrikerControls = previousControls;
        SetStrikerControls(true);
    }

    public bool TryStartRun(Action<bool> completed)
    {
        if (activeRun != null || striker == null || nonStriker == null)
            return false;

        activeRun = StartCoroutine(RunBetweenCreases(completed));
        return true;
    }

    public void CancelRun()
    {
        if (activeRun == null) return;
        StopCoroutine(activeRun);
        activeRun = null;

        // An unfinished run does not score. Return each batter to the crease
        // they started from so a stopped/dead delivery cannot strand them.
        if (hasRunStart)
        {
            striker.position = runStartStriker;
            nonStriker.position = runStartNonStriker;
        }
        hasRunStart = false;
        SetStrikerControls(true);
    }

    Vector3 runStartStriker;
    Vector3 runStartNonStriker;
    bool hasRunStart;

    IEnumerator RunBetweenCreases(Action<bool> completed)
    {
        Vector3 strikerStart = striker.position;
        Vector3 nonStrikerStart = nonStriker.position;
        runStartStriker = strikerStart;
        runStartNonStriker = nonStrikerStart;
        hasRunStart = true;

        Vector3 strikerEnd = nonStrikerStart;
        Vector3 nonStrikerEnd = strikerStart;
        strikerEnd.y = strikerStart.y;
        nonStrikerEnd.y = nonStrikerStart.y;
        SetStrikerControls(false);

        float distance = Vector3.Distance(strikerStart, strikerEnd);
        float duration = Mathf.Max(0.1f, distance / runningSpeed);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            striker.position = Vector3.Lerp(strikerStart, strikerEnd, t);
            nonStriker.position = Vector3.Lerp(nonStrikerStart, nonStrikerEnd, t);
            Face(striker, strikerEnd - strikerStart);
            Face(nonStriker, nonStrikerEnd - nonStrikerStart);
            yield return null;
        }

        striker.position = strikerEnd;
        nonStriker.position = nonStrikerEnd;
        Face(striker, strikerEnd - strikerStart);
        Face(nonStriker, nonStrikerEnd - nonStrikerStart);
        hasRunStart = false;
        activeRun = null;
        SetStrikerControls(true);
        completed?.Invoke(true);
    }

    void SetStrikerControls(bool enabledState)
    {
        if (strikerControls != null) strikerControls.enabled = enabledState;
        if (nonStrikerControls != null) nonStrikerControls.enabled = !enabledState;
    }

    static void Face(Transform batter, Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            batter.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }
}
