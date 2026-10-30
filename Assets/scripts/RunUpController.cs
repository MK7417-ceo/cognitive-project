using UnityEngine;
using System;

/// <summary>
/// Moves the bowler from the run-up start to the crease.
/// The delivery loop (gap between balls, teleport back, re-trigger) is owned
/// by BowlingManager, which calls BeginRunUp() before every ball. This script
/// no longer resets itself or talks to OverManager, so the two systems can't
/// fight each other anymore.
/// </summary>
public class RunUpController : MonoBehaviour
{
    [Header("Run-up Settings")]
    public Transform creaseTarget;
    public float runSpeed = 2.5f;
    public bool isRunning = true;

    private Vector3 startPosition;
    private Quaternion startRotation;
    public event Action RunUpReachedCrease;

    void Start()
    {
        // Remember the run-up start (position + facing).
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Update()
    {
        if (isRunning && creaseTarget != null)
        {
            Vector3 fixedTarget = new Vector3(creaseTarget.position.x, transform.position.y, creaseTarget.position.z);
            float distance = Vector3.Distance(transform.position, fixedTarget);

            if (distance > 0.1f)
            {
                // Always face the crease while running in.
                transform.LookAt(fixedTarget);

                transform.position = Vector3.MoveTowards(transform.position, fixedTarget, runSpeed * Time.deltaTime);
            }
            else
            {
                // Reached the crease: stop and wait. BowlingManager decides
                // when the next ball starts (it calls BeginRunUp).
                isRunning = false;
                RunUpReachedCrease?.Invoke();
            }
        }
    }

    /// <summary>
    /// Called by BowlingManager before every delivery: teleport back to the
    /// run-up start and run in again, like a real bowler.
    /// </summary>
    public void BeginRunUp()
    {
        transform.position = startPosition;
        transform.rotation = startRotation;
        isRunning = true;
    }
}
