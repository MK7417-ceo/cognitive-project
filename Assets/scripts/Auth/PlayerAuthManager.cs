using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Local player authentication for the VR cricket simulator.
///
/// Design decisions (read before changing this file):
/// - LOCAL ONLY, no server, no Google OAuth, no cloud account. The repo has no
///   auth backend, and adding one would need a secret / paid service. Google
///   sign-in is therefore NOT implemented here; it is a future step once a
///   backend exists. See README_AUTH_LOBBY.md.
/// - One profile = one player name + one 4-digit PIN. The PIN is NEVER stored
///   in plain text: only its SHA-256 hex hash is written to disk.
/// - Profiles live in Application.persistentDataPath/vr_cricket_profiles.json,
///   deliberately SEPARATE from GameManager's player_data.json, so this PR
///   cannot corrupt the existing save format (GameManager uses Newtonsoft
///   dictionaries; this file uses plain Unity JsonUtility structs).
/// - Guest mode is a real, unsaved profile ("Guest"). It lets a VR player
///   start immediately without typing anything, which matters in a headset.
/// - On sign-in, GameManager.PlayerName is updated if a GameManager exists,
///   so ModuleSelectorUI / stats keep showing the signed-in name. If no
///   GameManager exists, sign-in still succeeds; nothing crashes.
/// - VR typing limitation (honest): Unity's legacy InputField needs a
///   physical keyboard on PC, or an XR keyboard on Quest. This repo has no
///   XR keyboard yet, so in VR use Guest, or type name/PIN on PC first.
///   That limit is in the PR description, not hidden.
/// </summary>
public class PlayerAuthManager : MonoBehaviour
{
    public static PlayerAuthManager Instance { get; private set; }

    [Serializable]
    public class PlayerProfile
    {
        public string name;
        public string pinHash; // SHA-256 hex. Empty for Guest profiles.
        public bool isGuest;
        public int totalSessions;
        public int level = 1;
        public string createdUtc;
    }

    [Serializable]
    private class ProfileFile
    {
        public List<PlayerProfile> profiles = new List<PlayerProfile>();
    }

    public PlayerProfile CurrentProfile { get; private set; }
    public bool IsSignedIn => CurrentProfile != null;

    /// <summary>Fired after every sign-in, sign-out and profile creation.</summary>
    public event Action<PlayerProfile> OnAuthChanged;

    private List<PlayerProfile> profiles = new List<PlayerProfile>();
    private string ProfilesPath => Path.Combine(Application.persistentDataPath, "vr_cricket_profiles.json");

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LoadProfiles();
    }

    // ---------- Validation (pure, testable without Unity UI) ----------

    public static bool IsValidName(string name, out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Enter a player name.";
            return false;
        }
        name = name.Trim();
        if (name.Length < 2 || name.Length > 24)
        {
            error = "Name must be 2 to 24 characters.";
            return false;
        }
        error = null;
        return true;
    }

    public static bool IsValidPin(string pin, out string error)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length != 4)
        {
            error = "PIN must be exactly 4 digits.";
            return false;
        }
        foreach (char c in pin)
        {
            if (!char.IsDigit(c))
            {
                error = "PIN must be digits only.";
                return false;
            }
        }
        error = null;
        return true;
    }

    public static string HashPin(string pin)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(pin ?? ""));
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    // ---------- Profile operations ----------

    public bool CreateProfile(string name, string pin, out string error)
    {
        name = (name ?? "").Trim();
        if (!IsValidName(name, out error)) return false;
        if (!IsValidPin(pin, out error)) return false;
        if (FindProfile(name) != null)
        {
            error = "That name is already taken. Pick another name, or sign in.";
            return false;
        }

        var profile = new PlayerProfile
        {
            name = name,
            pinHash = HashPin(pin),
            isGuest = false,
            totalSessions = 0,
            level = 1,
            createdUtc = DateTime.UtcNow.ToString("o"),
        };
        profiles.Add(profile);
        SaveProfiles();
        SignInProfile(profile);
        return true;
    }

    public bool SignIn(string name, string pin, out string error)
    {
        name = (name ?? "").Trim();
        if (!IsValidName(name, out error)) return false;
        if (!IsValidPin(pin, out error)) return false;

        PlayerProfile profile = FindProfile(name);
        if (profile == null)
        {
            error = "No player with that name. Create a new player first.";
            return false;
        }
        if (profile.isGuest || !string.Equals(profile.pinHash, HashPin(pin), StringComparison.Ordinal))
        {
            error = "Wrong PIN for that player.";
            return false;
        }
        SignInProfile(profile);
        return true;
    }

    public void SignInAsGuest()
    {
        SignInProfile(new PlayerProfile
        {
            name = "Guest",
            pinHash = "",
            isGuest = true,
            totalSessions = 0,
            level = 1,
            createdUtc = DateTime.UtcNow.ToString("o"),
        });
    }

    public void SignOut()
    {
        CurrentProfile = null;
        OnAuthChanged?.Invoke(null);
    }

    public void RecordSession()
    {
        if (CurrentProfile == null || CurrentProfile.isGuest) return;
        CurrentProfile.totalSessions++;
        SaveProfiles();
    }

    private void SignInProfile(PlayerProfile profile)
    {
        CurrentProfile = profile;
        // Keep the existing GameManager in sync, if it exists. Never crash
        // the auth flow because GameManager is missing from the scene.
        try
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerName = profile.name;
                GameManager.Instance.TotalSessions = profile.totalSessions;
                GameManager.Instance.PlayerLevel = profile.level;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"PlayerAuthManager: could not sync GameManager: {e.Message}");
        }
        OnAuthChanged?.Invoke(profile);
    }

    private PlayerProfile FindProfile(string name)
    {
        foreach (var p in profiles)
        {
            if (!p.isGuest && string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }

    // ---------- Disk I/O (never throws out of Awake) ----------

    private void LoadProfiles()
    {
        try
        {
            if (!File.Exists(ProfilesPath)) return;
            string json = File.ReadAllText(ProfilesPath);
            if (string.IsNullOrWhiteSpace(json)) return;
            ProfileFile file = JsonUtility.FromJson<ProfileFile>(json);
            if (file != null && file.profiles != null)
                profiles = file.profiles;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"PlayerAuthManager: could not load profiles, starting empty: {e.Message}");
            profiles = new List<PlayerProfile>();
        }
    }

    private void SaveProfiles()
    {
        try
        {
            var file = new ProfileFile { profiles = profiles };
            File.WriteAllText(ProfilesPath, JsonUtility.ToJson(file, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"PlayerAuthManager: could not save profiles: {e.Message}");
        }
    }
}
