using UnityEngine;

/// <summary>
/// Lives on every bowled ball (added at runtime). Handles the two real-cricket
/// moments the user asked for:
///   1. Ball hits the stumps -> bails fly (BOWLED!), ball deflects off physically.
///   2. Bat hits the ball   -> REAL physics from the actual contact (Phase 1):
///      the ball keeps its own Rigidbody and bounces off the solid bat blade
///      using the bat's velocity at the contact point, the bat-face normal,
///      and a sweet-spot restitution, so direction, loft and speed all come
///      from the swing itself. No replacement ball, no teleported shot.
/// A struck ball therefore keeps flying on the same Rigidbody, so nothing
/// scripted interferes with its flight to the boundary.
/// </summary>
public class BallHitPhysics : MonoBehaviour
{
    public enum BallState { Flying, Hit, Dead }

    public BallState State { get; private set; } = BallState.Flying;
    public bool CanCallRun => State == BallState.Hit && !boundaryResolved && !outcomeRecorded;

    /// <summary>True once this hit ball has crossed the boundary.</summary>
    public bool IsBoundaryResolved { get { return boundaryResolved; } }

    // Real-world tuning.
    const float BallMass = 0.156f;          // kg, real cricket ball
    const float HitTransfer = 1.35f;        // bat speed -> ball speed multiplier
    const float MaxExitSpeed = 42f;         // m/s (~151 km/h, elite six)
    const float MinExitSpeed = 8f;

    Rigidbody rb;
    int bounces;
    bool boundaryResolved;
    bool hasPitched;   // first ground contact: apply delivery-type pitch behaviour
    float lifeTimer;
    float staleTimer;  // flying ball that has stopped moving / rolled away
    float lastBatContactTime = -10f;
    const float BatContactCooldown = 0.25f; // one contact = one shot, not many
    bool outcomeRecorded;
    bool adjudicatedWide;

    public BowlingManager DeliveringBowler { get; set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnDestroy()
    {
        // A delivery or struck ball that reaches the end of its lifetime
        // without a boundary or dismissal completes the delivery. A struck
        // ball's original scripted object is set Dead before it is replaced.
        if (State == BallState.Flying || State == BallState.Hit)
            Resolve(CricketMatchManager.DeliveryResult.Dot);
        BallThrower.ReleaseMatchBall(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (State != BallState.Flying || rb == null) return;

        // 1) Wicket zone -> bowled.
        var zone = other.GetComponent<WicketZone>();
        if (zone == null) zone = other.GetComponentInParent<WicketZone>();
        if (zone != null)
        {
            OnBowled();
            return;
        }

        // 2) Bat trigger fallback (only if some bat collider is still a
        // trigger in a scene). The solid-collider path in OnCollisionEnter
        // below is the real Phase 1 path; this keeps old setups working.
        var tracker = other.GetComponent<BatSwingTracker>();
        if (tracker == null) tracker = other.GetComponentInParent<BatSwingTracker>();
        if (tracker != null)
        {
            Vector3 toBall = (transform.position - tracker.transform.position);
            Vector3 normal = toBall.sqrMagnitude > 0.01f ? toBall.normalized : Vector3.up;
            HandleBatContact(tracker, transform.position, normal);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Phase 1: solid bat contact FIRST, before pitch/stump logic.
        // This is the real-world bat: direction and speed come from the
        // contact point, the bat face, and the bat's velocity there.
        if (State == BallState.Flying && collision.contactCount > 0)
        {
            var batTracker = collision.gameObject.GetComponentInParent<BatSwingTracker>();
            if (batTracker != null)
            {
                var cp = collision.GetContact(0);
                HandleBatContact(batTracker, cp.point, cp.normal);
                if (State != BallState.Flying) return; // hit or blocked, done
                // If the bat was still, fall through: ball passes (leave/miss).
            }
        }

        // First pitch: react like the real delivery type (fast skids, spin
        // turns, bouncer climbs, yorker stays low). Overrides the bouncy
        // material for this one contact only.
        if (State == BallState.Flying && !hasPitched)
        {
            foreach (var cp in collision.contacts)
            {
                if (cp.normal.y > 0.5f)
                {
                    hasPitched = true;
                    ApplyPitchBehaviour();
                    break;
                }
            }
        }

        // Direct solid hit on a stump also counts as bowled.
        if (State == BallState.Flying)
        {
            var stumps = collision.gameObject.GetComponentInParent<StumpsPhysics>();
            if (stumps != null)
            {
                OnBowled();
                return;
            }
        }

        // Count ground bounces for FOUR vs SIX.
        if (State == BallState.Hit && !boundaryResolved)
        {
            foreach (var cp in collision.contacts)
            {
                if (cp.normal.y > 0.5f) { bounces++; break; }
            }
        }
    }

    // ---------------- PITCH BEHAVIOUR ----------------
    // Different deliveries react differently off the pitch, like real cricket.
    // The delivery type was tagged at spawn by BowlingManager (BallFlightData).
    void ApplyPitchBehaviour()
    {
        if (rb == null) return;

        var flight = GetComponent<BallFlightData>();
        Vector3 v = rb.linearVelocity;

        Vector3 flat = v; flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) return;
        flat.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, flat); // sideways to travel

        Vector3 nv;
        float turn = 0f;

        if (flight != null)
        {
            bool la = flight.isLeftArm;
            switch (flight.spinKind)
            {
                case BallFlightData.SpinKind.Fast:
                    // Seam/fast: skids through, keeps its pace.
                    nv = v * 0.94f + Vector3.up * 1.0f;
                    break;
                case BallFlightData.SpinKind.OffSpin:
                    // Fingerspin: grips and turns (mirrored for left-arm orthodox).
                    turn = la ? 2.4f : -2.4f;
                    nv = v * 0.8f + lateral * turn + Vector3.up * 2.0f;
                    break;
                case BallFlightData.SpinKind.LegSpin:
                    // Wristspin: turns the other way (mirrored for left-arm wrist).
                    turn = la ? -2.6f : 2.6f;
                    nv = v * 0.8f + lateral * turn + Vector3.up * 2.0f;
                    break;
                case BallFlightData.SpinKind.Googly:
                    // Wrong'un: turns OPPOSITE to the leg-spinner's stock ball.
                    turn = la ? 2.6f : -2.6f;
                    nv = v * 0.8f + lateral * turn + Vector3.up * 2.0f;
                    break;
                case BallFlightData.SpinKind.Bouncer:
                    // Climbs steeply off the pitch.
                    nv = v * 0.9f + Vector3.up * 4.5f;
                    break;
                case BallFlightData.SpinKind.Yorker:
                    // Stays low and skids under the bat.
                    nv = new Vector3(v.x * 0.9f, v.y * 0.25f, v.z * 0.9f);
                    break;
                default:
                    nv = v * 0.94f + Vector3.up * 1.0f;
                    break;
            }
        }
        else
        {
            nv = v * 0.94f + Vector3.up * 1.0f;
        }

        rb.linearVelocity = nv;

        // Visible spin on turning balls.
        if (turn != 0f)
            rb.angularVelocity = lateral * turn * 6f;

        Debug.Log($"BallHitPhysics: pitched as {(flight != null ? flight.spinKind.ToString() : "Fast?")} (leftArm={(flight != null && flight.isLeftArm)})");
    }

    // Called by the wicket-keeper on a catch.
    public bool MarkCaught()
    {
        bool caughtInAir = State == BallState.Hit && bounces == 0;
        bool runOut = !caughtInAir && State == BallState.Hit &&
            CricketMatchManager.Instance != null && CricketMatchManager.Instance.IsRunInProgress;
        State = BallState.Dead;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        lifeTimer = 2f;
        Resolve(caughtInAir
            ? CricketMatchManager.DeliveryResult.Wicket
            : runOut
                ? CricketMatchManager.DeliveryResult.RunOut
                : CricketMatchManager.DeliveryResult.Dot);
        return runOut;
    }

    // ---------------- BOWLED ----------------

    void OnBowled()
    {
        if (State != BallState.Flying) return;
        State = BallState.Dead;
        Resolve(CricketMatchManager.DeliveryResult.Wicket);
        Vector3 incoming = rb.linearVelocity;

        var stumps = FindAnyObjectByType<StumpsPhysics>();
        if (stumps != null) stumps.KnockDown(incoming);

        // Deflect back off the stumps with a pop-up, like a real bowled ball.
        Vector3 back = -incoming;
        back.y = 0f;
        if (back.sqrMagnitude < 0.01f) back = Vector3.back;
        back.Normalize();
        rb.linearVelocity = back * incoming.magnitude * 0.35f + Vector3.up * 3f;
        rb.angularVelocity = Random.insideUnitSphere * 10f;

        var ui = CricketMatchManager.Instance;
        if (ui != null) ui.ShowMessage("BOWLED HIM!", Color.red);

        lifeTimer = 4f;
        Debug.Log($"BallHitPhysics: bowled! incoming {incoming.magnitude:F1} m/s");
    }

    // ---------------- BAT HIT (Phase 1: real physics) ----------------

    /// <summary>
    /// Real bat-ball physics. No replacement ball, no faked direction.
    /// - Bat velocity AT the contact point (linear + angular x lever arm),
    ///   so a tip hit is harder than a handle hit and a wrist snap counts.
    /// - Reflection off the bat face (contact normal), so the shot goes
    ///   where the bat face was actually pointing, with loft from the face.
    /// - Restitution by sweet spot: full power near the sweet spot,
    ///   weaker + more sideways on an edge.
    /// - Incoming ball speed is preserved in the reflection, not discarded:
    ///   a fast ball hit with a fast bat goes further than either alone.
    /// - Still bat (&lt; block threshold): leave/miss, ball passes.
    /// - Slow bat (block threshold): defensive block, ball drops dead.
    /// The same ball keeps its Rigidbody, so it can genuinely reach the
    /// boundary on physics alone, and its lifetime is 14 s from the hit.
    /// </summary>
    void HandleBatContact(BatSwingTracker tracker, Vector3 contactPoint, Vector3 faceNormal)
    {
        if (State != BallState.Flying || rb == null || tracker == null) return;
        if (Time.time - lastBatContactTime < BatContactCooldown) return;
        lastBatContactTime = Time.time;

        float batSpeed = tracker.GetSwingSpeed();

        // Still bat: leave / miss.
        if (batSpeed < tracker.blockThreshold) return;

        // Slow swing: block, not a shot.
        if (!tracker.IsRealSwing())
        {
            OnBlock();
            return;
        }

        Vector3 batVel = tracker.GetVelocityAtPoint(contactPoint);
        Vector3 incoming = rb.linearVelocity;

        // Face normal must point from bat toward ball, or the reflection
        // inverts. Unity contact normals usually do, but enforce it.
        Vector3 normal = faceNormal;
        if (normal.sqrMagnitude < 0.01f)
            normal = (transform.position - contactPoint).normalized;
        normal.Normalize();
        Vector3 toBall = transform.position - contactPoint;
        if (toBall.sqrMagnitude > 0.01f && Vector3.Dot(normal, toBall.normalized) < 0f)
            normal = -normal;

        // Reflect the ball's velocity relative to the moving bat.
        Vector3 relative = incoming - batVel;
        float vn = Vector3.Dot(relative, normal);
        Vector3 reflected = vn < 0f ? relative - 2f * vn * normal : relative;

        // Sweet spot: 0.75 restitution in the middle, 0.35 on a bad edge.
        float sweetDist = Vector3.Distance(contactPoint, tracker.GetSweetSpotPosition());
        float restitution = Mathf.Lerp(0.75f, 0.35f, Mathf.Clamp01(sweetDist / 0.35f));

        Vector3 exit = reflected * restitution + batVel * 0.85f;
        float exitSpeed = Mathf.Clamp(exit.magnitude, MinExitSpeed, MaxExitSpeed);
        if (exit.sqrMagnitude < 0.01f) exit = normal * exitSpeed;
        else exit = exit.normalized * exitSpeed;

        rb.linearVelocity = exit;
        // Spin from the bat's rotation + a little loft spin, like a real shot.
        rb.angularVelocity = tracker.GetAngularVelocity() * 0.5f
            + Vector3.Cross(exit.normalized, Vector3.up) * exitSpeed * 0.2f;

        State = BallState.Hit;
        bounces = 0;
        boundaryResolved = false;
        lifeTimer = 14f;

        var ui = CricketMatchManager.Instance;
        if (ui != null) ui.ShowSpeed(exitSpeed);
        Debug.Log($"BallHitPhysics: REAL HIT! bat {batSpeed:F1} m/s at contact, sweetDist {sweetDist:F2} m, restitution {restitution:F2} -> ball {exitSpeed:F1} m/s ({exitSpeed * 3.6f:F0} km/h) dir {exit.normalized}");
    }

    // Legacy trigger path, kept only so old callers still compile.
    // Phase 1 uses HandleBatContact above.
    void OnBatHit(BatSwingTracker tracker)
    {
        float batSpeed = tracker.GetSwingSpeed(); // m/s, real swing
        Vector3 swingDir = tracker.GetSwingDirection();

        float exitSpeed = Mathf.Clamp(batSpeed * HitTransfer, MinExitSpeed, MaxExitSpeed);
        Vector3 dir = swingDir * 0.9f + Vector3.up * 0.45f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.up;
        dir.Normalize();

        Debug.Log($"BallHitPhysics: HIT! bat {batSpeed:F1} m/s -> ball {exitSpeed:F1} m/s ({exitSpeed * 3.6f:F0} km/h)");
        // Prevent this scripted delivery from being counted as a dot when it
        // is replaced by the free-flight struck ball below.
        State = BallState.Dead;
        SpawnHitBall(transform.position, dir * exitSpeed);
        Destroy(gameObject); // scripted ball replaced by a pure-physics ball
    }

    void OnBlock()
    {
        // Defensive block: usually the ball drops dead near the bat.
        // Sometimes it's an edge and flies to the keeper (CAUGHT BEHIND!).
        if (Random.value < 0.4f)
        {
            var keeper = FindAnyObjectByType<WicketKeeper>();
            Vector3 dir;
            if (keeper != null)
            {
                dir = keeper.transform.position - transform.position;
            }
            else
            {
                dir = -rb.linearVelocity;
            }
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            dir.Normalize();
            rb.linearVelocity = dir * 7f + Vector3.up * 2.5f;
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            Debug.Log("BallHitPhysics: EDGED! flying to the keeper...");
        }
        else
        {
            rb.linearVelocity = rb.linearVelocity * 0.12f + Vector3.up * 1.2f;
            rb.angularVelocity *= 0.2f;
            Debug.Log("BallHitPhysics: blocked.");
        }
    }

    // A fresh ball with no scripted flight — pure rigid-body physics from here.
    void SpawnHitBall(Vector3 pos, Vector3 velocity)
    {
        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        var sc = GetComponent<SphereCollider>();
        Vector3 ballScale = transform.lossyScale;

        var go = new GameObject("HitBall");
        go.transform.position = pos;
        // Match the source prefab's world scale. Without this, its scaled
        // mesh/collider become full prefab-size again after a bat strike.
        go.transform.localScale = ballScale;
        BallThrower.TrackMatchBall(go);

        if (mf != null)
        {
            var n = go.AddComponent<MeshFilter>();
            n.sharedMesh = mf.sharedMesh;
        }
        if (mr != null)
        {
            var n = go.AddComponent<MeshRenderer>();
            n.sharedMaterials = mr.sharedMaterials;
        }
        var col = go.AddComponent<SphereCollider>();
        col.radius = sc != null ? sc.radius : 0.036f;
        if (sc != null) col.material = sc.material;

        var nrb = go.AddComponent<Rigidbody>();
        nrb.mass = BallMass;
        nrb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var hp = go.AddComponent<BallHitPhysics>();
        hp.DeliveringBowler = DeliveringBowler;
        hp.BeginHit(velocity);

        var ui = CricketMatchManager.Instance;
        if (ui != null) ui.ShowSpeed(velocity.magnitude);
    }

    public void BeginHit(Vector3 velocity)
    {
        rb = GetComponent<Rigidbody>();
        State = BallState.Hit;
        rb.linearVelocity = velocity;
        // A touch of backspin, like a real lofted shot.
        rb.angularVelocity = Vector3.Cross(velocity.normalized, Vector3.up) * velocity.magnitude * 0.4f;
        lifeTimer = 14f;
    }

    // ---------------- Boundary + cleanup ----------------

    void Update()
    {
        if (State == BallState.Flying && !adjudicatedWide)
            CheckForWide();

        if (State == BallState.Hit && !boundaryResolved)
        {
            var m = CricketMatchManager.Instance;
            if (m != null)
            {
                Vector3 d = transform.position - m.PitchCentre;
                d.y = 0f;
                if (d.magnitude > m.boundaryRadius)
                {
                    boundaryResolved = true;
                    bool six = bounces == 0;
                    State = BallState.Dead;
                    Resolve(six ? CricketMatchManager.DeliveryResult.BoundarySix : CricketMatchManager.DeliveryResult.BoundaryFour);
                    m.ShowMessage(six ? "SIX!" : "FOUR!", Color.yellow, 3f);
                    Debug.Log($"BallHitPhysics: {(six ? "SIX" : "FOUR")} (bounces={bounces})");

                    // A boundary ends the ball's playable life. Remove it now
                    // instead of leaving a stray ball visible for the 14s
                    // free-flight timeout or making the next delivery wait.
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.isKinematic = true;
                    }
                    Destroy(gameObject);
                    return;
                }
            }
        }

        if (State != BallState.Flying)
        {
            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0f) Destroy(gameObject);
            return;
        }

        // Phase 1: flying ball cleanup WITHOUT a fixed timer.
        // A live ball is never deleted for being slow or old. Only a ball
        // that has actually stopped, fallen off the world, or rolled far
        // away is retired, so the bowler can bowl the next delivery.
        // This, together with BallThrower no longer using Destroy(5 s),
        // is the boundary-vanish fix.
        if (rb != null && rb.linearVelocity.magnitude < 0.05f)
        {
            staleTimer += Time.deltaTime;
            if (staleTimer > 3f)
            {
                State = BallState.Dead;
                lifeTimer = 2f;
            }
        }
        else
        {
            staleTimer = 0f;
        }

        if (transform.position.y < -2f)
        {
            State = BallState.Dead;
            lifeTimer = 0.5f;
        }
    }

    void CheckForWide()
    {
        var match = CricketMatchManager.Instance;
        var flight = GetComponent<BallFlightData>();
        if (match == null || match.Stumps == null || flight == null) return;

        Vector3 direction = flight.deliveryDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        direction.Normalize();

        Vector3 fromWicket = transform.position - match.Stumps.transform.position;
        fromWicket.y = 0f;
        if (Vector3.Dot(fromWicket, direction) < 0f) return;

        adjudicatedWide = true;
        Vector3 lateralOffset = fromWicket - direction * Vector3.Dot(fromWicket, direction);
        if (lateralOffset.magnitude < 1.25f) return;

        State = BallState.Dead;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        lifeTimer = 1.5f;
        Resolve(CricketMatchManager.DeliveryResult.Wide);
        match.ShowMessage("WIDE BALL", Color.yellow);
        Debug.Log("BallHitPhysics: wide ball called outside the wicket line.");
    }

    void Resolve(CricketMatchManager.DeliveryResult result)
    {
        if (outcomeRecorded) return;
        outcomeRecorded = true;
        if (CricketMatchManager.Instance != null)
            CricketMatchManager.Instance.ResolveDelivery(this, result);
    }

}
