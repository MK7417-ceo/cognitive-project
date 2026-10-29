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
///   Awakes run before any Start), so deactivating the bowlers here really
///   stops the first delivery until the player picks a mode in the lobby.
///   Do NOT move the bowler deactivation into Start() — by then the first
///   Bowl trigger has already fired.
/// - Flow: Auth panel -> Lobby panel -> Match / Nets / Training info /
///   How to Play. Sign Out returns to the Auth panel.
/// - The lobby is a WorldSpace panel inside a simple primitive Pavilion
///   shell (floor, back wall, side walls, lockers, bench) built from
///   cubes — no external models, no scene edits needed. The shell is a
///   functional stand-in for the preview's dressing room: it gives the
///   player a room to stand in, and it hides when the match starts so it
///   never blocks the pitch. A real art pass (proper pavilion models)
///   can replace the shell later without touching the auth/mode logic:
///   set buildPavilionShell = false and parent this canvas under a real
///   room, via pavilionAnchor if you assign one.
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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Must run in Awake (see header): bowlers' Start() fires the first
        // Bowl trigger, so they must be inactive before Start runs.
        CollectBowlers();
        bowlersWereActive = false;
        foreach (var b in bowlers)
        {
            if (b != null && b.gameObject.activeSelf)
            {
                bowlersWereActive = true;
                b.gameObject.SetActive(false);
            }
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
        foreach (var b in bowlers)
        {
            if (b != null) b.gameObject.SetActive(true);
        }
        if (isMatch && auth != null) auth.RecordSession();
        if (lobbyRoot != null) lobbyRoot.SetActive(false);
        if (lobbyCanvas != null) lobbyCanvas.gameObject.SetActive(false);
        State = LobbyState.Match;
    }

    public void ReturnToLobby()
    {
        // Pause, do not deactivate bowlers (see header for why).
        Time.timeScale = 0f;
        if (lobbyRoot != null) lobbyRoot.SetActive(true);
        if (lobbyCanvas != null) lobbyCanvas.gameObject.SetActive(true);
        ShowLobby();
        State = LobbyState.PausedInLobby;
    }

    public void ResumeMatch()
    {
        Time.timeScale = 1f;
        if (lobbyRoot != null) lobbyRoot.SetActive(false);
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

    // ---------- UI construction (runtime, no scene edits) ----------

    private void BuildLobby()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();

        // Frame: a root at the player's ground position, facing where the
        // player looks. All lobby objects are children of this root.
        Vector3 playerPos = cam != null ? cam.transform.position : Vector3.zero;
        Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();
        float groundY = 0f;
        Transform xrOrigin = GameObject.Find("XR Origin")?.transform;
        if (xrOrigin != null) groundY = xrOrigin.position.y;
        else if (cam != null && cam.transform.parent != null) groundY = cam.transform.parent.position.y;

        lobbyRoot = new GameObject("PavilionLobby");
        lobbyRoot.transform.position = new Vector3(playerPos.x, groundY, playerPos.z);
        lobbyRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

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
        MakePanel(canvasGo.transform, "Backdrop", new Color(0.05f, 0.08f, 0.1f, 0.92f),
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
        // Primitive room, centred on the root (player at origin, facing +Z
        // in root space). Panel sits at z = 1.6, back wall just behind it.
        MakeBox("PavilionFloor", new Vector3(0f, 0.02f, 1f), new Vector3(9f, 0.1f, 5f), new Color(0.15f, 0.35f, 0.15f));
        MakeBox("PavilionBackWall", new Vector3(0f, 1.5f, 2.1f), new Vector3(9f, 3f, 0.2f), new Color(0.45f, 0.32f, 0.2f));
        MakeBox("PavilionLeftWall", new Vector3(-4.4f, 1.5f, 0.5f), new Vector3(0.2f, 3f, 4.5f), new Color(0.35f, 0.25f, 0.16f));
        MakeBox("PavilionRightWall", new Vector3(4.4f, 1.5f, 0.5f), new Vector3(0.2f, 3f, 4.5f), new Color(0.35f, 0.25f, 0.16f));
        MakeBox("Locker1", new Vector3(-2.8f, 1f, 1.85f), new Vector3(0.9f, 2f, 0.5f), new Color(0.2f, 0.25f, 0.35f));
        MakeBox("Locker2", new Vector3(-1.8f, 1f, 1.85f), new Vector3(0.9f, 2f, 0.5f), new Color(0.25f, 0.3f, 0.4f));
        MakeBox("Locker3", new Vector3(2.8f, 1f, 1.85f), new Vector3(0.9f, 2f, 0.5f), new Color(0.2f, 0.25f, 0.35f));
        MakeBox("PavilionBench", new Vector3(0f, 0.3f, 1.5f), new Vector3(2.5f, 0.5f, 0.6f), new Color(0.5f, 0.36f, 0.22f));
    }

    private void MakeBox(string name, Vector3 localPos, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(lobbyRoot.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null) renderer.material = new Material(shader);
            renderer.material.color = color;
        }
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
