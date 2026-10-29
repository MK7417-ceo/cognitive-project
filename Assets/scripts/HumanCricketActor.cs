using UnityEngine;

/// <summary>Creates a stationary human visual from the scene's humanoid bowler.</summary>
public static class HumanCricketActor
{
    public static GameObject Create(BowlingManager template, string actorName, Vector3 position, Quaternion rotation)
    {
        if (template == null || template.animator == null)
        {
            Debug.LogError($"HumanCricketActor: cannot create {actorName}; no humanoid bowler template is available.");
            return null;
        }

        GameObject actor = Object.Instantiate(template.gameObject, position, rotation);
        actor.name = actorName;

        // Keep the imported humanoid and its idle animator, but disable the
        // bowling/gameplay scripts and physics from the source bowler.
        foreach (MonoBehaviour script in actor.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script != null) script.enabled = false;
        }

        foreach (Collider collider in actor.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null) collider.enabled = false;
        }

        foreach (Rigidbody body in actor.GetComponentsInChildren<Rigidbody>(true))
        {
            if (body == null) continue;
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
        }

        foreach (Camera childCamera in actor.GetComponentsInChildren<Camera>(true))
            if (childCamera != null) childCamera.enabled = false;

        foreach (AudioListener listener in actor.GetComponentsInChildren<AudioListener>(true))
            if (listener != null) listener.enabled = false;

        Animator[] animators = actor.GetComponentsInChildren<Animator>(true);
        foreach (Animator animator in animators)
        {
            if (animator == null) continue;
            animator.applyRootMotion = false;
            animator.enabled = true;
        }

        // OverManager may have deactivated the source bowler while selecting
        // the active bowler. Activate the visual clone only after its gameplay
        // scripts and physics have been disabled.
        actor.SetActive(true);
        return actor;
    }

}
