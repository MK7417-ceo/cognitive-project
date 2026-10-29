using System.Collections.Generic;
using UnityEngine;

/// <summary>Places nine humanoid fielders and assigns each nearby ball to one fielder.</summary>
public class FieldingManager : MonoBehaviour
{
    float ReactRange => CricketMatchManager.Instance != null
        ? CricketMatchManager.Instance.boundaryRadius
        : 55f;
    readonly List<DynamicFielder> fielders = new List<DynamicFielder>(9);
    DynamicFielder chasingFielder;
    BallHitPhysics trackedBall;
    Vector3 mainWicketPosition;

    public static FieldingManager Build(Vector3 bowlerPosition, Vector3 wicketPosition, BowlingManager humanTemplate)
    {
        var existing = FindAnyObjectByType<FieldingManager>();
        if (existing != null) return existing;

        Vector3 pitchDirection = wicketPosition - bowlerPosition;
        pitchDirection.y = 0f;
        if (pitchDirection.sqrMagnitude < 0.01f) pitchDirection = Vector3.forward;
        pitchDirection.Normalize();
        Vector3 offSide = Vector3.Cross(Vector3.up, pitchDirection).normalized;
        Vector3 pitchMiddle = (bowlerPosition + wicketPosition) * 0.5f;

        var root = new GameObject("NineHumanFielders");
        var manager = root.AddComponent<FieldingManager>();
        manager.mainWicketPosition = wicketPosition;

        Vector3[] positions =
        {
            wicketPosition + pitchDirection * 3.2f + offSide * 2.7f,  // first slip
            wicketPosition + pitchDirection * 4.1f + offSide * 4.8f,  // second slip
            wicketPosition + pitchDirection * 2.2f + offSide * 7.0f,  // gully
            pitchMiddle + pitchDirection * 1.5f + offSide * 13.5f,    // cover
            pitchMiddle + offSide * 16f,                              // point
            pitchMiddle - pitchDirection * 5f - offSide * 12f,        // mid-off
            pitchMiddle - pitchDirection * 5f + offSide * 12f,        // mid-on
            wicketPosition - pitchDirection * 8f + offSide * 9f,      // square leg
            wicketPosition - pitchDirection * 13f + offSide * 5f      // fine leg
        };

        string[] names =
        {
            "Fielder_Slip_1", "Fielder_Slip_2", "Fielder_Gully", "Fielder_Cover",
            "Fielder_Point", "Fielder_MidOff", "Fielder_MidOn", "Fielder_SquareLeg", "Fielder_FineLeg"
        };

        for (int i = 0; i < positions.Length; i++)
        {
            positions[i].y = 0f;
            Vector3 towardPitch = pitchMiddle - positions[i];
            towardPitch.y = 0f;
            Quaternion rotation = towardPitch.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(towardPitch.normalized, Vector3.up)
                : Quaternion.identity;

            GameObject human = HumanCricketActor.Create(humanTemplate, names[i], positions[i], rotation);
            if (human == null) continue;

            human.transform.SetParent(root.transform, true);
            var fielder = human.AddComponent<DynamicFielder>();
            fielder.Initialize(positions[i]);
            manager.fielders.Add(fielder);
        }

        Debug.Log($"FieldingManager: created {manager.fielders.Count} human fielders at separate field positions.");
        return manager;
    }

    void Update()
    {
        if (trackedBall == null || trackedBall.State == BallHitPhysics.BallState.Dead)
        {
            ClearAssignment();
            trackedBall = FindLiveBall();
            if (trackedBall == null || trackedBall.State == BallHitPhysics.BallState.Dead) return;
        }

        // Keep the slips clear around the wicket so the wicketkeeper owns
        // catches immediately behind the batter.
        if (Vector3.Distance(trackedBall.transform.position, mainWicketPosition) <= 5f)
        {
            ClearChasingFielder();
            return;
        }

        if (chasingFielder != null && !chasingFielder.IsAvailable)
            return;

        ClearChasingFielder();

        DynamicFielder closest = null;
        float closestDistance = ReactRange;
        foreach (DynamicFielder fielder in fielders)
        {
            if (fielder == null || !fielder.IsAvailable) continue;

            float distance = Vector3.Distance(fielder.transform.position, trackedBall.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = fielder;
            }
        }

        if (closest != null)
        {
            chasingFielder = closest;
            chasingFielder.AssignBall(trackedBall);
        }
    }

    void ClearAssignment()
    {
        ClearChasingFielder();
        trackedBall = null;
    }

    void ClearChasingFielder()
    {
        if (chasingFielder != null) chasingFielder.ClearBall();
        chasingFielder = null;
    }

    static BallHitPhysics FindLiveBall()
    {
        // Prefer the match registry: old caught/resolved balls can remain in
        // the scene briefly while their cleanup timer runs.
        GameObject activeBall = BallThrower.ActiveMatchBall;
        BallHitPhysics activePhysics = activeBall != null
            ? activeBall.GetComponent<BallHitPhysics>()
            : null;
        if (activePhysics != null && activePhysics.State != BallHitPhysics.BallState.Dead)
            return activePhysics;

        foreach (BallHitPhysics candidate in FindObjectsByType<BallHitPhysics>(FindObjectsInactive.Exclude))
            if (candidate != null && candidate.State != BallHitPhysics.BallState.Dead)
                return candidate;
        return null;
    }
}
