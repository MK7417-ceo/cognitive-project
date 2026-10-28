using UnityEngine;

/// <summary>
/// Corrects the cricket bat's size, placement, and orientation.
/// Automatically snaps the bat directly into the batsman's hands (BatHolder),
/// strips out any giant scale distortions or skyward offsets, scales the bat
/// to authentic real-world dimensions (~0.85m), and points the blade downwards
/// in the proper batting stance.
/// </summary>
[ExecuteAlways]
public class SnapSceneBatToHand : MonoBehaviour
{
    [Header("Bat & Hand Targets")]
    [Tooltip("Scene instance of the cricket bat")]
    public GameObject batInstance;

    [Tooltip("BatHolder transform located on the batsman's hand")]
    public Transform batHolder;

    [Header("Placement & Stance Offsets")]
    public Vector3 localPosOffset = Vector3.zero;
    public Vector3 localRotOffset = new Vector3(0f, 0f, 0f);

    [Header("Realistic Dimensions")]
    [Tooltip("Standard adult cricket bat length in meters (typically 0.85m / 33.5 inches)")]
    public float realBatLength = 0.85f;

    void Awake()
    {
        FixBatAndAttachToHand();
    }

    void OnEnable()
    {
        FixBatAndAttachToHand();
    }

    void Start()
    {
        FixBatAndAttachToHand();
    }

    void Update()
    {
        if (batInstance == null || batHolder == null || batInstance.transform.parent != batHolder)
        {
            FixBatAndAttachToHand();
        }
    }

    /// <summary>
    /// Snaps the bat directly into the batsman's hands and scales it correctly.
    /// </summary>
    public void FixBatAndAttachToHand()
    {
        ResolveReferences();

        if (batInstance == null || batHolder == null)
        {
            Debug.LogError("SnapSceneBatToHand: Could not find batInstance or batHolder in scene.");
            return;
        }

        // 1. Reset ALL child transform distortions (removes 400x scale & skyward position offsets)
        foreach (Transform child in batInstance.GetComponentsInChildren<Transform>(true))
        {
            if (child != batInstance.transform)
            {
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
            }
        }

        // 2. Parent the bat directly under the batsman's hand (BatHolder)
        batInstance.transform.SetParent(batHolder, false);
        batInstance.transform.localPosition = Vector3.zero;
        batInstance.transform.localRotation = Quaternion.identity;

        // 3. Compute realistic real-world uniform scale based on mesh bounds
        var mf = batInstance.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 meshBounds = mf.sharedMesh.bounds.size;
            float maxDim = Mathf.Max(meshBounds.x, Mathf.Max(meshBounds.y, meshBounds.z));

            if (maxDim > 0.001f)
            {
                // Account for the batsman/hand's lossy scale
                float parentScale = Mathf.Abs(batHolder.lossyScale.y);
                if (parentScale < 0.0001f) parentScale = 1f;

                float targetUniformScale = realBatLength / (maxDim * parentScale);
                batInstance.transform.localScale = Vector3.one * targetUniformScale;

                // Determine longest mesh axis (the bat blade)
                Vector3 longestAxis = Vector3.up;
                if (meshBounds.x > meshBounds.y && meshBounds.x > meshBounds.z)
                    longestAxis = Vector3.right;
                else if (meshBounds.z > meshBounds.y && meshBounds.z > meshBounds.x)
                    longestAxis = Vector3.forward;

                // Batting stance pose: blade points downward toward the crease
                Quaternion alignDown = Quaternion.FromToRotation(longestAxis, Vector3.down);
                batInstance.transform.localRotation = alignDown * Quaternion.Euler(localRotOffset);

                // Offset handle into the batsman's palm
                batInstance.transform.localPosition = new Vector3(0f, -0.1f, 0f) + localPosOffset;

                Debug.Log($"SnapSceneBatToHand: Bat scaled to {realBatLength}m (scale x{targetUniformScale:F4}) and placed in batsman's hand.");
            }
        }

        // 4. Ensure Rigidbody is kinematic so physics doesn't drop the bat
        var rb = batInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = true;
        }

        // 5. Ensure bat has BatSwingTracker attached for hit detection
        if (batInstance.GetComponent<BatSwingTracker>() == null)
        {
            batInstance.AddComponent<BatSwingTracker>();
        }
    }

    void ResolveReferences()
    {
        // 1. Locate BatHolder under batsman if missing
        if (batHolder == null)
        {
            var bh = GameObject.Find("BatHolder");
            if (bh != null)
            {
                batHolder = bh.transform;
            }
            else
            {
                // Search under batsman hierarchy
                var batsman = FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
                if (batsman != null)
                {
                    var allTransforms = batsman.GetComponentsInChildren<Transform>(true);
                    foreach (var t in allTransforms)
                    {
                        string n = t.name.ToLower();
                        if (n.Contains("righthand") || n.Contains("right_hand") || n.Contains("r_hand") || n.Contains("wrist"))
                        {
                            var holderObj = new GameObject("BatHolder");
                            holderObj.transform.SetParent(t, false);
                            holderObj.transform.localPosition = Vector3.zero;
                            holderObj.transform.localRotation = Quaternion.identity;
                            batHolder = holderObj.transform;
                            break;
                        }
                    }
                    if (batHolder == null) batHolder = batsman.transform;
                }
            }
        }

        // 2. Locate Bat instance if missing
        if (batInstance == null)
        {
            var batObj = GameObject.Find("Cricket bat 2_0");
            if (batObj != null)
            {
                batInstance = batObj;
            }
            else
            {
                batInstance = FindBatMeshObject();
            }
        }
    }

    static GameObject FindBatMeshObject()
    {
        foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
        {
            var go = mf.gameObject;
            if (mf.sharedMesh == null) continue;
            string n = go.name.ToLower();
            if (!n.Contains("bat") || n.Contains("holder") || n.Contains("manager")) continue;

            while (go.transform.parent != null &&
                   go.transform.parent.name.ToLower().Contains("bat") &&
                   !go.transform.parent.name.Contains("Holder") &&
                   !go.transform.parent.name.Contains("manager"))
            {
                go = go.transform.parent.gameObject;
            }
            return go;
        }
        return null;
    }
}
