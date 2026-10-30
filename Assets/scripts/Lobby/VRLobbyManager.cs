using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authentication + Pavilion lobby for the VR cricket simulator.
///
/// HOW IT WORKS (read before changing this file):
/// - This manager is created by AuthLobbyBootstrap (RuntimeInitialize,
///   AfterSceneLoad) OR can be placed in the scene by hand. Either way its
///   Awake() runs BEFORE any BowlingManager.Start() (Unity guarantees all
///   Awakes run before any Start), so gating the bowlers here really
///   stops the first delivery until the player picks a mode in the lobby.
///   FLOW RULE: the bowler is NEVER hidden (SetActive(false)). A real
///   bowler stands visible at the top of his run-up before the match.
///   Only his BowlingManager + RunUpController are disabled, so he
///   stands idle and does not bowl. Walk Out re-enables them.
///   Do NOT move the bowler gating into Start() — by then the first
///   Bowl trigger has already fired.
/// - Flow: Auth panel -> Lobby panel -> Match / Nets / Training info /
///   How to Play. Sign Out returns to the Auth panel.
/// - PAVILION LOCATION (fixed Oct 30, after a Play screenshot showed the
///   old bug): the pavilion is NOT built at the player's start position.
///   The old code anchored lobbyRoot to the camera, and the camera starts
///   at the batsman — so the whole dressing room, floor and ceiling landed
///   ON TOP of the pitch, over the stumps. That was wrong, and the later
///   30 m side placement was also wrong: the game's boundary is 55 m
///   (CricketMatchManager.boundaryRadius), so 30 m was still INSIDE the
///   field. REAL-WORLD PAVILION RULE (user directive, Oct 30): a real
///   pavilion is a building OUTSIDE the boundary rope, on the side of
///   the ground, facing the pitch — never on the pitch, never in the
///   middle of the outfield, never overlapping the rope, the stands or
///   the pitch. This code therefore places the pavilion centre
///   max(pavilionSideDistance = 62 m, boundaryRadius + 7 m) sideways
///   from the TRUE pitch centre (midpoint between batsman and bowler,
///   not the batsman camera position), facing back toward the pitch.
///   Numbers, pitch centre at 0 m: pavilion centre 62 m, floor front
///   edge 57.5 m (2.5 m beyond the 55 m rope), glass 59.4 m, panel
///   60.4 m — all outside the rope, with a clear walkway gap, no
///   overlap by construction. The pavilion BUILDING stays visible
///   during a match like a real ground (only the lobby panel hides);
///   Walk Out teleports the rig back to the batsman position it saved
///   before leaving. The glass wall looks onto the REAL stadium — the
///   old fake field/pitch/floodlights drawn in front of the glass are
///   deleted, because they painted a second field over the real one.
/// - The lobby is a WorldSpace panel inside a preview-matched Pavilion
///   dressing room built ONLY from code primitives and code-made
///   materials — no external models, no scene edits needed. It matches
///   the approved preview (Option A): kit wall with detailed lockers on
///   the left, a glass wall looking onto the real stadium in front,
///   a sofa and team-talk board on the right and left walls, a wooden
///   floor and bench in the middle, and ceiling lights. It hides when
///   the match starts so it never blocks the pitch. A real art pass
///   (proper pavilion models) can replace the shell later without
///   touching the auth/mode logic: set buildPavilionShell = false and
///   parent this canvas under a real room, via pavilionAnchor if you
///   assign one. The physical lockers, door and board are visual
///   anchors only — the panel buttons are still the controls, because
///   VR ray clicks need XR controller interactors in the scene (open
///   editor step, see README). Do not claim the lockers or door are
///   clickable until those interactors exist and are Play-tested.
/// - "Return to Lobby" from a match is a PAUSE, not a reset: it sets
///   Time.timeScale = 0 and shows the lobby again. Deactivating the
///   bowlers mid-match would strand BowlingManager's private
///   waitingForNextBall flag (its coroutine is killed on deactivate),
///   which can stall the next delivery. Pause avoids that entirely.
///   A full reset is a scene reload, which returns to the Auth panel.
/// - Training button does NOT fake a training module: ModuleSelectorUI
///   is not in SampleScene (verified Oct 2026), so the button shows an
///   info panel with the exact editor step needed, instead of pretending
///   training started.
/// - Match vs Nets, honestly: both start the same bowling loop and the
///   same physics today. The only difference is that Match records a
///   session on the signed-in profile and Nets does not. Do not claim
///   a separate nets physics mode exists until one is built.
/// - VR input limit: buttons work with a controller ray once XR
///   interactors exist in the scene (still an open editor step, see
///   README). Typing name/PIN needs a physical keyboard on PC, or an
///   XR keyboard on Quest (not in this repo yet). Guest mode exists
///   precisely so a headset player is never blocked by typing.
/// </summary>
public class VRLobbyManager : MonoBehaviour
{
    public static VRLobbyManager Instance { get; private set; }

    [Header("Pavilion")]
    [Tooltip("Build the primitive pavilion shell around the lobby. Turn off if it clashes with your scene art.")]
    public bool buildPavilionShell = true;

    [Tooltip("Optional: parent the lobby canvas under this transform (a real pavilion room) instead of the auto shell.")]
    public Transform pavilionAnchor;

    [Header("Pavilion location (real-world rule)")]
    [Tooltip("Distance from the pitch centre to the pavilion, sideways from the pitch. Tune in the Inspector if the pavilion lands inside the stands in your scene scale — the pitch must stay clear of it.")]
    public float pavilionSideDistance = 62f;

    [Tooltip("True = pavilion on the right side of the pitch when facing the bowler, like a real ground's main pavilion. False = left side.")]
    public bool pavilionOnRightSide = true;

    public enum LobbyState { Auth, Lobby, Match, PausedInLobby }
    public LobbyState State { get; private set; } = LobbyState.Auth;

    private PlayerAuthManager auth;
    private GameObject lobbyRoot;
    private Canvas lobbyCanvas;
    private GameObject authPanel;
    private GameObject lobbyPanel;
    private GameObject infoPanel;
    private Text infoText;
    private Text lobbyWelcomeText;
    private Text lobbyProfileText;
    private Text authMessageText;
    private InputField signInNameField;
    private InputField signInPinField;
    private InputField newNameField;
    private InputField newPinField;
    private GameObject signInPanel;
    private GameObject newPlayerPanel;
    private List<BowlingManager> bowlers = new List<BowlingManager>();
    private bool bowlersWereActive;

    // Player-rig teleport state: lobby lives at the boundary, the match
    // lives at the batsman. Both positions are saved and restored exactly.
    private Transform playerRig;
    private Vector3 rigStartPos;
    private Quaternion rigStartRot;
    private Vector3 rigMatchPos;
    private Quaternion rigMatchRot;
    private bool hasMatchRigPos;
    private Vector3 pavilionPos;
    private Quaternion pavilionRot;
    private float pavilionGroundY;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Must run in Awake (see header): bowlers' Start() fires the first
        // Bowl trigger, so they must be gated before Start runs.
        // FLOW: keep the bowler GameObject VISIBLE, disable only the
        // bowling scripts, so he stands idle at the run-up, not vanished.
        CollectBowlers();
        bowlersWereActive = false;
        foreach (var b in bowlers)
        {
            if (b == null) continue;
            bowlersWereActive = true;
            var runUp = b.GetComponent<RunUpController>();
            if (runUp != null)
            {
                runUp.isRunning = false;
                runUp.enabled = false;
            }
            if (b.animator != null)
            {
                b.animator.ResetTrigger("Bowl");
                b.animator.Play("StandIdle", 0, 0f);
            }
            b.enabled = false;
        }

        auth = GetComponent<PlayerAuthManager>();
        if (auth == null) auth = gameObject.AddComponent<PlayerAuthManager>();
        auth.OnAuthChanged += OnAuthChanged;

        BuildLobby();
        ShowAuth(null);
    }

    void Update()
    {
        // Esc returns to the lobby as a pause (see header). ModuleSelectorUI
        // also uses Esc, but it is not in SampleScene, so no conflict today.
        if (Input.GetKeyDown(KeyCode.Escape) && State == LobbyState.Match)
            ReturnToLobby();
    }

    void OnDestroy()
    {
        if (auth != null) auth.OnAuthChanged -= OnAuthChanged;
    }

    // ---------- Bowler control ----------

    private void CollectBowlers()
    {
        bowlers.Clear();
        foreach (var b in FindObjectsByType<BowlingManager>(FindObjectsInactive.Include))
        {
            if (b != null && !bowlers.Contains(b)) bowlers.Add(b);
        }
    }

    private void StartBowling(bool isMatch)
    {
        CollectBowlers();
        Time.timeScale = 1f;
        RestorePlayerRigToMatch();
        // FLOW: bowler was visible but gated. Re-enable scripts only;
        // do NOT call BeginRunUp here - RunUpController.Start has not
        // run yet for a gated bowler, so its start position is not
        // captured yet. Enabling lets Start run next frame and the
        // bowler runs in from his scene run-up position.
        foreach (var b in bowlers)
        {
            if (b == null) continue;
            b.enabled = true;
            var runUp = b.GetComponent<RunUpController>();
            if (runUp != null) runUp.enabled = true;
        }
        if (isMatch && auth != null) auth.RecordSession();
        // Real-world rule: the pavilion BUILDING stays visible during a
        // match. Only the lobby panel hides. Hiding lobbyRoot used to
        // make the whole building vanish when play started.
        if (lobbyCanvas != null) lobbyCanvas.gameObject.SetActive(false);
        State = LobbyState.Match;
    }

    public void ReturnToLobby()
    {
        // Pause, do not deactivate bowlers (see header for why).
        // Save where the batsman camera is now, then walk the player
        // back into the pavilion — the lobby panel is 30 m away at the
        // boundary, it is not visible from the pitch.
        SavePlayerRigAsMatch();
        MovePlayerRigToPavilion();
        Time.timeScale = 0f;
        if (lobbyRoot != null) lobbyRoot.SetActive(true);
        if (lobbyCanvas != null) lobbyCanvas.gameObject.SetActive(true);
        ShowLobby();
        State = LobbyState.PausedInLobby;
    }

    public void ResumeMatch()
    {
        Time.timeScale = 1f;
        RestorePlayerRigToMatch();
        // See StartBowling: building stays, only the panel hides.
        if (lobbyCanvas != null) lobbyCanvas.gameObject.SetActive(false);
        State = LobbyState.Match;
    }

    // ---------- Auth -> Lobby wiring ----------

    private void OnAuthChanged(PlayerAuthManager.PlayerProfile profile)
    {
        if (profile != null) ShowLobby();
        else ShowAuth(null);
    }

    private void ShowAuth(string message)
    {
        State = LobbyState.Auth;
        if (authPanel != null) authPanel.SetActive(true);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (infoPanel != null) infoPanel.SetActive(false);
        if (authMessageText != null) authMessageText.text = message ?? "";
        ShowSignInTab();
    }

    private void ShowLobby()
    {
        State = LobbyState.Lobby;
        Time.timeScale = 1f;
        if (authPanel != null) authPanel.SetActive(false);
        if (infoPanel != null) infoPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        if (auth != null && auth.CurrentProfile != null)
        {
            var p = auth.CurrentProfile;
            if (lobbyWelcomeText != null)
                lobbyWelcomeText.text = p.isGuest ? "Welcome, Guest" : $"Welcome back, {p.name}";
            if (lobbyProfileText != null)
                lobbyProfileText.text = p.isGuest
                    ? "Guest profile — sessions are not saved."
                    : $"Level {p.level}  •  {p.totalSessions} sessions";
        }
    }

    private void ShowInfo(string text)
    {
        if (infoPanel != null) infoPanel.SetActive(true);
        if (authPanel != null) authPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (infoText != null) infoText.text = text;
    }

    private void ShowSignInTab()
    {
        if (signInPanel != null) signInPanel.SetActive(true);
        if (newPlayerPanel != null) newPlayerPanel.SetActive(false);
    }

    private void ShowNewPlayerTab()
    {
        if (signInPanel != null) signInPanel.SetActive(false);
        if (newPlayerPanel != null) newPlayerPanel.SetActive(true);
    }


    // ---------- Pavilion placement: boundary, not pitch ----------

    private Transform FindPlayerRig()
    {
        // Scene name is "XR Origin (VR)" — the old Find("XR Origin")
        // never matched it, which is one reason the old ground height
        // was wrong. Fall back to the plain name, then to the camera.
        GameObject xr = GameObject.Find("XR Origin (VR)");
        if (xr == null) xr = GameObject.Find("XR Origin");
        if (xr != null) return xr.transform;
        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        return cam != null ? cam.transform : null;
    }

    private bool RigIsXROrigin()
    {
        return playerRig != null && playerRig.name.Contains("XR Origin");
    }

    private void MovePlayerRigToPavilion()
    {
        if (playerRig == null) return;
        Vector3 pos = pavilionPos;
        if (RigIsXROrigin())
        {
            // Keep the rig's own height (its camera offset adds eye
            // height on top). Only the horizontal position changes.
            pos.y = playerRig.position.y;
        }
        else
        {
            pos.y = pavilionGroundY + 1.6f;
        }
        playerRig.position = pos;
        playerRig.rotation = pavilionRot;
    }

    private void SavePlayerRigAsMatch()
    {
        if (playerRig == null) return;
        rigMatchPos = playerRig.position;
        rigMatchRot = playerRig.rotation;
        hasMatchRigPos = true;
    }

    private void RestorePlayerRigToMatch()
    {
        if (playerRig == null) return;
        if (hasMatchRigPos)
        {
            playerRig.position = rigMatchPos;
            playerRig.rotation = rigMatchRot;
        }
        else
        {
            playerRig.position = rigStartPos;
            playerRig.rotation = rigStartRot;
        }
    }

    // ---------- UI construction (runtime, no scene edits) ----------

    private void BuildLobby()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();

        // Frame: pitch centre = where the player starts (the batsman).
        // The pavilion is NOT there. Real pavilions sit at the boundary
        // edge, sideways from the pitch, facing it — so place the root
        // pavilionSideDistance metres to the side of the pitch centre,
        // and rotate it so its +Z (glass wall, panel) looks back at the
        // pitch. Ground is y = 0 in SampleScene (stumps, bowler, field
        // all sit at y ~ 0); the old code used the XR Origin height as
        // ground, which floated the floor above the field.
        Vector3 playerPos = cam != null ? cam.transform.position : Vector3.zero;
        Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();
        float groundY = 0f;
        // TRUE pitch centre: midpoint between batsman and bowler. The
        // old code used the camera (batsman) position as "pitch centre",
        // which shifted the pavilion ~10 m along the pitch. If no bowler
        // is found, fall back to the camera direction only.
        Vector3 pitchCentre = new Vector3(playerPos.x, groundY, playerPos.z);
        Vector3 pitchDir = forward;
        GameObject bowlerGo = GameObject.Find("Bowler");
        if (bowlerGo != null)
        {
            Vector3 bowlerPos = bowlerGo.transform.position;
            bowlerPos.y = groundY;
            Vector3 batsmanPos = new Vector3(playerPos.x, groundY, playerPos.z);
            float endDistance = Vector3.Distance(batsmanPos, bowlerPos);
            if (endDistance >= 10f && endDistance <= 30f)
            {
                pitchCentre = (batsmanPos + bowlerPos) * 0.5f;
                pitchDir = (bowlerPos - batsmanPos).normalized;
            }
        }
        Vector3 side = Vector3.Cross(Vector3.up, pitchDir).normalized;
        if (!pavilionOnRightSide) side = -side;
        Vector3 toPitch = -side;
        // Boundary-aware distance: never inside the 55 m rope, even if
        // someone lowers pavilionSideDistance in the Inspector.
        float boundary = 55f;
        if (CricketMatchManager.Instance != null)
            boundary = CricketMatchManager.Instance.boundaryRadius;
        float pavilionDistance = Mathf.Max(pavilionSideDistance, boundary + 7f);
        pavilionPos = pitchCentre + side * pavilionDistance;
        pavilionPos.y = groundY;
        pavilionGroundY = groundY;
        pavilionRot = Quaternion.LookRotation(toPitch, Vector3.up);

        lobbyRoot = new GameObject("PavilionLobby");
        lobbyRoot.transform.position = pavilionPos;
        lobbyRoot.transform.rotation = pavilionRot;

        // Player starts inside the pavilion, not on the pitch.
        playerRig = FindPlayerRig();
        if (playerRig != null)
        {
            rigStartPos = playerRig.position;
            rigStartRot = playerRig.rotation;
        }
        MovePlayerRigToPavilion();

        if (buildPavilionShell && pavilionAnchor == null)
            BuildPavilionShell();

        // Canvas: WorldSpace in VR, ScreenSpaceOverlay as a PC fallback.
        var canvasGo = new GameObject("LobbyCanvas");
        canvasGo.transform.SetParent(pavilionAnchor != null ? pavilionAnchor : lobbyRoot.transform, false);
        lobbyCanvas = canvasGo.AddComponent<Canvas>();
        if (cam != null)
        {
            lobbyCanvas.renderMode = RenderMode.WorldSpace;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1200f, 750f);
            rt.localPosition = new Vector3(0f, 1.55f, 1.6f);
            // Face the player: canvas +Z must point TOWARD the player, so
            // rotate 180 degrees from the root's forward.
            rt.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rt.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f);
        }
        else
        {
            lobbyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(900f, 600f);
        }
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // Backdrop panel so text is readable against the stadium.
        MakePanel(canvasGo.transform, "Backdrop", new Color(0.05f, 0.23f, 0.18f, 0.92f),
            new Vector2(0f, 0f), new Vector2(1150f, 700f));

        authPanel = MakePanel(canvasGo.transform, "AuthPanel", new Color(0f, 0f, 0f, 0f),
            new Vector2(0f, 0f), new Vector2(1100f, 650f));
        lobbyPanel = MakePanel(canvasGo.transform, "LobbyPanel", new Color(0f, 0f, 0f, 0f),
            new Vector2(0f, 0f), new Vector2(1100f, 650f));
        infoPanel = MakePanel(canvasGo.transform, "InfoPanel", new Color(0f, 0f, 0f, 0f),
            new Vector2(0f, 0f), new Vector2(1100f, 650f));

        BuildAuthPanel();
        BuildLobbyPanel();
        BuildInfoPanel();
    }

    private void BuildAuthPanel()
    {
        MakeText(authPanel.transform, "VR CRICKET  •  PAVILION", 40, new Vector2(0f, 270f), TextAnchor.MiddleCenter, Color.yellow);
        MakeText(authPanel.transform, "Sign in to play, or continue as Guest.", 26, new Vector2(0f, 215f), TextAnchor.MiddleCenter, Color.white);

        MakeButton(authPanel.transform, "Sign In", new Vector2(-260f, 150f), new Vector2(300f, 70f), ShowSignInTab);
        MakeButton(authPanel.transform, "New Player", new Vector2(60f, 150f), new Vector2(300f, 70f), ShowNewPlayerTab);
        MakeButton(authPanel.transform, "Guest", new Vector2(380f, 150f), new Vector2(220f, 70f), () =>
        {
            auth.SignInAsGuest();
        });

        // Sign-in sub-panel.
        signInPanel = MakePanel(authPanel.transform, "SignInPanel", new Color(0f, 0f, 0f, 0f),
            new Vector2(0f, -70f), new Vector2(1000f, 340f));
        signInNameField = MakeInput(signInPanel.transform, "Player name", new Vector2(0f, 90f), false);
        signInPinField = MakeInput(signInPanel.transform, "4-digit PIN", new Vector2(0f, 0f), true);
        MakeButton(signInPanel.transform, "Enter Pavilion", new Vector2(0f, -100f), new Vector2(360f, 70f), () =>
        {
            if (auth.SignIn(signInNameField.text, signInPinField.text, out string err)) return;
            if (authMessageText != null) authMessageText.text = err;
        });

        // New-player sub-panel.
        newPlayerPanel = MakePanel(authPanel.transform, "NewPlayerPanel", new Color(0f, 0f, 0f, 0f),
            new Vector2(0f, -70f), new Vector2(1000f, 340f));
        newNameField = MakeInput(newPlayerPanel.transform, "Choose a player name", new Vector2(0f, 90f), false);
        newPinField = MakeInput(newPlayerPanel.transform, "Choose a 4-digit PIN", new Vector2(0f, 0f), true);
        MakeButton(newPlayerPanel.transform, "Create Player", new Vector2(0f, -100f), new Vector2(360f, 70f), () =>
        {
            if (auth.CreateProfile(newNameField.text, newPinField.text, out string err)) return;
            if (authMessageText != null) authMessageText.text = err;
        });

        authMessageText = MakeText(authPanel.transform, "", 24, new Vector2(0f, -275f), TextAnchor.MiddleCenter, Color.red);
        MakeText(authPanel.transform, "PINs are stored as a hash, never as plain text. Guest needs no typing.",
            20, new Vector2(0f, -310f), TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
    }

    private void BuildLobbyPanel()
    {
        MakeText(lobbyPanel.transform, "PAVILION  •  DRESSING ROOM", 40, new Vector2(0f, 270f), TextAnchor.MiddleCenter, Color.yellow);
        lobbyWelcomeText = MakeText(lobbyPanel.transform, "Welcome", 34, new Vector2(0f, 215f), TextAnchor.MiddleCenter, Color.white);
        lobbyProfileText = MakeText(lobbyPanel.transform, "", 24, new Vector2(0f, 175f), TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));

        // Modes are labelled as places, so the lobby feels like a room,
        // not a flat menu — matching the approved UI preview (Option A).
        MakeButton(lobbyPanel.transform, "Walk Out: Match vs AI Bowler", new Vector2(0f, 95f), new Vector2(640f, 70f),
            () => StartBowling(true));
        MakeButton(lobbyPanel.transform, "Go to Nets: Practice", new Vector2(0f, 10f), new Vector2(640f, 70f),
            () => StartBowling(false));
        MakeButton(lobbyPanel.transform, "Training Room", new Vector2(-170f, -75f), new Vector2(300f, 70f), () =>
        {
            ShowInfo("Training Room\n\n"
                + "Cognitive training modules are not in SampleScene yet.\n"
                + "ModuleSelectorUI exists in code, but no scene object uses it.\n\n"
                + "To enable this button for real, add a GameObject with\n"
                + "ModuleSelectorUI + a Canvas + button prefab in the Unity\n"
                + "Editor, then point this button at it. Until then this\n"
                + "button will not pretend training has started.");
        });
        MakeButton(lobbyPanel.transform, "How to Play", new Vector2(170f, -75f), new Vector2(300f, 70f), () =>
        {
            ShowInfo("How to Play\n\n"
                + "• Grab the bat with your controller and swing.\n"
                + "• Keys 1-9 switch camera. Esc returns to this lobby (pause).\n"
                + "• The bowler waits until the last ball is dead or past\n"
                + "  the boundary — a hit ball never vanishes mid-flight.\n"
                + "• In VR, use Guest if you do not want to type a PIN.");
        });
        MakeButton(lobbyPanel.transform, "Sign Out", new Vector2(0f, -170f), new Vector2(300f, 70f), () =>
        {
            // If returning from a paused match, unpause first so the
            // auth screen is not frozen behind a paused game.
            Time.timeScale = 1f;
            auth.SignOut();
        });
        MakeText(lobbyPanel.transform, "Esc during a match returns here (pause).",
            20, new Vector2(0f, -240f), TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
    }

    private void BuildInfoPanel()
    {
        infoText = MakeText(infoPanel.transform, "", 26, new Vector2(0f, 60f), TextAnchor.MiddleCenter, Color.white);
        // Info text is multi-line, so give it a tall box (MakeText defaults to 60 high).
        infoText.GetComponent<RectTransform>().sizeDelta = new Vector2(1050f, 380f);
        MakeButton(infoPanel.transform, "Back to Lobby", new Vector2(0f, -240f), new Vector2(360f, 70f), ShowLobby);
    }

    private void BuildPavilionShell()
    {
        // Preview-matched dressing room, in root space: player at the
        // origin, facing +Z. Panel sits at z = 1.6, so the glass wall at
        // z = 2.6 sits behind the panel, and the REAL stadium is visible
        // through the glass. No fake field is drawn here — the old fake
        // field/pitch painted a second ground over the real pitch.
        // Everything here is a code primitive — no model files are needed.
        // Colours use the deck/preview palette: stadium green, wood brown,
        // cream, amber, deep red sofa. All objects are children of
        // Shell objects are children of lobbyRoot, but the building is
        // kept visible during a match: StartBowling/ResumeMatch hide only
        // the lobby canvas, never the shell (real-world rule).

        // Room: wooden floor, ceiling, side walls.
        MakeBox("PavilionFloor", new Vector3(0f, 0.02f, 1.5f), new Vector3(10f, 0.1f, 6f), new Color(0.42f, 0.28f, 0.16f));
        MakeBox("PavilionCeiling", new Vector3(0f, 3.25f, 1.5f), new Vector3(10f, 0.1f, 6f), new Color(0.20f, 0.18f, 0.16f));
        MakeBox("PavilionLeftWall", new Vector3(-4.9f, 1.6f, 1.5f), new Vector3(0.2f, 3.2f, 6f), new Color(0.23f, 0.32f, 0.26f));
        MakeBox("PavilionRightWall", new Vector3(4.9f, 1.6f, 1.5f), new Vector3(0.2f, 3.2f, 6f), new Color(0.23f, 0.32f, 0.26f));

        // Glass wall in front of the player: frame, transparent pane,
        // and an amber door frame in the middle marking the walk-out door.
        MakeBox("GlassPillarLeft", new Vector3(-2.2f, 1.6f, 2.6f), new Vector3(0.25f, 3.2f, 0.25f), new Color(0.30f, 0.20f, 0.12f));
        MakeBox("GlassPillarRight", new Vector3(2.2f, 1.6f, 2.6f), new Vector3(0.25f, 3.2f, 0.25f), new Color(0.30f, 0.20f, 0.12f));
        MakeBox("GlassBeamTop", new Vector3(0f, 3.15f, 2.6f), new Vector3(4.65f, 0.25f, 0.25f), new Color(0.30f, 0.20f, 0.12f));
        MakeBox("GlassSill", new Vector3(0f, 0.1f, 2.6f), new Vector3(4.65f, 0.2f, 0.25f), new Color(0.30f, 0.20f, 0.12f));
        MakeGlass("GlassPane", new Vector3(0f, 1.6f, 2.6f), new Vector3(4.15f, 2.9f, 0.05f));
        MakeBox("WalkOutDoorLeft", new Vector3(-0.55f, 1.6f, 2.55f), new Vector3(0.12f, 2.9f, 0.12f), new Color(0.91f, 0.64f, 0.09f));
        MakeBox("WalkOutDoorRight", new Vector3(0.55f, 1.6f, 2.55f), new Vector3(0.12f, 2.9f, 0.12f), new Color(0.91f, 0.64f, 0.09f));
        MakeBox("WalkOutDoorHandle", new Vector3(0.35f, 1.1f, 2.52f), new Vector3(0.08f, 0.3f, 0.08f), new Color(0.91f, 0.64f, 0.09f));

        // No fake stadium here on purpose: the glass looks onto the
        // real SampleScene ground. (Oct 30 fix — the old fake field and
        // pitch strip sat on top of the real pitch and looked like a
        // green slab dumped on the ground.)

        // Kit wall on the left: two detailed lockers (body, door, handle).
        MakeLocker("Locker1", -3.6f, new Color(0.30f, 0.35f, 0.45f));
        MakeLocker("Locker2", -2.6f, new Color(0.35f, 0.40f, 0.50f));
        MakeBox("KitBag", new Vector3(-1.6f, 0.22f, 0.9f), new Vector3(0.9f, 0.35f, 0.4f), new Color(0.15f, 0.15f, 0.18f));
        MakeBox("KitBagStrap", new Vector3(-1.6f, 0.42f, 0.9f), new Vector3(0.7f, 0.06f, 0.3f), new Color(0.91f, 0.64f, 0.09f));

        // Sofa on the right, team-talk board on the left wall.
        MakeBox("SofaBase", new Vector3(3.2f, 0.35f, 2.0f), new Vector3(1.9f, 0.5f, 0.9f), new Color(0.55f, 0.15f, 0.15f));
        MakeBox("SofaBack", new Vector3(3.2f, 0.85f, 2.35f), new Vector3(1.9f, 0.7f, 0.25f), new Color(0.55f, 0.15f, 0.15f));
        MakeBox("SofaArmLeft", new Vector3(2.35f, 0.65f, 2.0f), new Vector3(0.25f, 0.6f, 0.9f), new Color(0.55f, 0.15f, 0.15f));
        MakeBox("SofaArmRight", new Vector3(4.05f, 0.65f, 2.0f), new Vector3(0.25f, 0.6f, 0.9f), new Color(0.55f, 0.15f, 0.15f));
        MakeBox("SofaCushionLeft", new Vector3(2.9f, 0.65f, 1.95f), new Vector3(0.55f, 0.15f, 0.7f), new Color(0.65f, 0.25f, 0.25f));
        MakeBox("SofaCushionRight", new Vector3(3.5f, 0.65f, 1.95f), new Vector3(0.55f, 0.15f, 0.7f), new Color(0.65f, 0.25f, 0.25f));
        MakeBox("TeamTalkBoard", new Vector3(-4.75f, 1.7f, 0.5f), new Vector3(0.08f, 1.3f, 2.0f), new Color(0.10f, 0.25f, 0.18f));
        MakeBox("TeamTalkBoardHeader", new Vector3(-4.75f, 2.4f, 0.5f), new Vector3(0.1f, 0.15f, 2.0f), new Color(0.91f, 0.64f, 0.09f));

        // Bench in the middle, below panel height so it never blocks it.
        MakeBox("PavilionBenchTop", new Vector3(0f, 0.48f, 1.0f), new Vector3(2.2f, 0.08f, 0.5f), new Color(0.50f, 0.36f, 0.22f));
        MakeBox("PavilionBenchLegLeft", new Vector3(-0.9f, 0.24f, 1.0f), new Vector3(0.12f, 0.48f, 0.45f), new Color(0.30f, 0.20f, 0.12f));
        MakeBox("PavilionBenchLegRight", new Vector3(0.9f, 0.24f, 1.0f), new Vector3(0.12f, 0.48f, 0.45f), new Color(0.30f, 0.20f, 0.12f));

        // Ceiling lights: glowing strips plus two real point lights
        // (no shadows, low count, so VR performance stays safe).
        MakeEmissiveBox("CeilingLightLeft", new Vector3(-2f, 3.18f, 1.5f), new Vector3(1.0f, 0.06f, 0.3f));
        MakeEmissiveBox("CeilingLightRight", new Vector3(2f, 3.18f, 1.5f), new Vector3(1.0f, 0.06f, 0.3f));
        MakePointLight("PavilionLightLeft", new Vector3(-2f, 2.8f, 1.5f));
        MakePointLight("PavilionLightRight", new Vector3(2f, 2.8f, 1.5f));
    }

    private void MakeLocker(string name, float x, Color bodyColor)
    {
        MakeBox(name, new Vector3(x, 1.0f, 2.2f), new Vector3(0.9f, 2f, 0.55f), bodyColor);
        MakeBox(name + "Door", new Vector3(x, 1.0f, 1.91f), new Vector3(0.7f, 1.7f, 0.05f), new Color(0.45f, 0.50f, 0.60f));
        MakeBox(name + "Handle", new Vector3(x + 0.25f, 1.0f, 1.87f), new Vector3(0.06f, 0.25f, 0.06f), new Color(0.91f, 0.64f, 0.09f));
    }

    private void MakeBox(string name, Vector3 localPos, Vector3 size, Color color)
    {
        MakePrimitive(name, PrimitiveType.Cube, localPos, size, color, false, 0f);
    }

    private void MakeGlass(string name, Vector3 localPos, Vector3 size)
    {
        MakePrimitive(name, PrimitiveType.Cube, localPos, size, new Color(0.65f, 0.80f, 0.85f, 0.25f), true, 0f);
    }

    private void MakeEmissiveBox(string name, Vector3 localPos, Vector3 size)
    {
        MakePrimitive(name, PrimitiveType.Cube, localPos, size, new Color(1f, 0.95f, 0.7f), false, 2f);
    }

    private void MakeCylinder(string name, Vector3 localPos, Vector3 size, Color color)
    {
        MakePrimitive(name, PrimitiveType.Cylinder, localPos, size, color, false, 0f);
    }

    private void MakePointLight(string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(lobbyRoot.transform, false);
        go.transform.localPosition = localPos;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.95f, 0.8f);
        light.intensity = 0.7f;
        light.range = 9f;
        light.shadows = LightShadows.None;
    }

    private void MakePrimitive(string name, PrimitiveType type, Vector3 localPos, Vector3 size, Color color, bool transparent, float emissiveIntensity)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(lobbyRoot.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return;
        var material = new Material(shader);
        material.color = color;
        if (transparent)
        {
            // URP transparent surface, Standard transparent mode as fallback.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.renderQueue = 3000;
        }
        if (emissiveIntensity > 0f && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.7f) * emissiveIntensity);
        }
        renderer.material = material;
    }

    // ---------- Small UI helpers (legacy UI.Text, like CricketMatchManager) ----------

    private static Font DefaultFont()
    {
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private GameObject MakePanel(Transform parent, string name, Color color, Vector2 anchored, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;
        rt.sizeDelta = size;
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private Text MakeText(Transform parent, string text, int fontSize, Vector2 anchored, TextAnchor alignment, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;
        rt.sizeDelta = new Vector2(1050f, 60f);
        var t = go.AddComponent<Text>();
        Font font = DefaultFont();
        if (font != null) t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = alignment;
        t.color = color;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    private Button MakeButton(Transform parent, string label, Vector2 anchored, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;
        rt.sizeDelta = size;
        var image = go.AddComponent<Image>();
        image.color = new Color(0.15f, 0.45f, 0.35f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null) button.onClick.AddListener(onClick);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.sizeDelta = Vector2.zero;
        var t = labelGo.AddComponent<Text>();
        Font font = DefaultFont();
        if (font != null) t.font = font;
        t.fontSize = 26;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = label;
        return button;
    }

    private InputField MakeInput(Transform parent, string placeholder, Vector2 anchored, bool isPin)
    {
        var go = new GameObject("InputField", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;
        rt.sizeDelta = new Vector2(640f, 70f);
        var image = go.AddComponent<Image>();
        image.color = Color.white;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(16f, 8f);
        textRt.offsetMax = new Vector2(-16f, -8f);
        var text = textGo.AddComponent<Text>();
        Font font = DefaultFont();
        if (font != null) text.font = font;
        text.fontSize = 26;
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleLeft;

        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.transform.SetParent(go.transform, false);
        var phRt = phGo.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(16f, 8f);
        phRt.offsetMax = new Vector2(-16f, -8f);
        var ph = phGo.AddComponent<Text>();
        if (font != null) ph.font = font;
        ph.fontSize = 26;
        ph.color = new Color(0.4f, 0.4f, 0.4f);
        ph.alignment = TextAnchor.MiddleLeft;
        ph.text = placeholder;

        var input = go.AddComponent<InputField>();
        input.textComponent = text;
        input.placeholder = ph;
        input.targetGraphic = image;
        if (isPin)
        {
            input.contentType = InputField.ContentType.Password;
            input.characterLimit = 4;
        }
        else
        {
            input.characterLimit = 24;
        }
        return input;
    }
}
