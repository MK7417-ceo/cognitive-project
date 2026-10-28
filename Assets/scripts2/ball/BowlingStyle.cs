using UnityEngine;

/// <summary>
/// Bowling style data model and physical delivery presets for various bowler types.
/// </summary>
[System.Serializable]
public class BowlingStyle
{
    public BowlerType bowlerType;
    public string displayName;
    public bool isLeftArm;
    public bool isSpin;
    public float typicalSpeedKmh;
    public Color themeColor;

    public BowlingStyle() { }

    public BowlingStyle(BowlerType type, string name, bool leftArm, bool spin, float speedKmh, Color color)
    {
        bowlerType = type;
        displayName = name;
        isLeftArm = leftArm;
        isSpin = spin;
        typicalSpeedKmh = speedKmh;
        themeColor = color;
    }

    /// <summary>
    /// Factory preset helper for standard cricket bowling styles.
    /// </summary>
    public static BowlingStyle GetPreset(BowlerType type)
    {
        switch (type)
        {
            case BowlerType.RightArmFast:
                return new BowlingStyle(type, "Right-Arm Fast", false, false, 145f, Color.red);
            case BowlerType.LeftArmFast:
                return new BowlingStyle(type, "Left-Arm Fast", true, false, 142f, Color.red);
            case BowlerType.RightArmMediumFast:
                return new BowlingStyle(type, "Right-Arm Medium Fast", false, false, 125f, Color.yellow);
            case BowlerType.LeftArmMediumFast:
                return new BowlingStyle(type, "Left-Arm Medium Fast", true, false, 122f, Color.yellow);
            case BowlerType.RightArmOffSpin:
                return new BowlingStyle(type, "Right-Arm Off Spin", false, true, 88f, Color.cyan);
            case BowlerType.RightArmLegSpin:
                return new BowlingStyle(type, "Right-Arm Leg Spin", false, true, 84f, Color.magenta);
            case BowlerType.LeftArmOrthodoxSpin:
                return new BowlingStyle(type, "Left-Arm Orthodox Spin", true, true, 86f, Color.cyan);
            case BowlerType.LeftArmWristSpin:
                return new BowlingStyle(type, "Left-Arm Chinaman / Wrist Spin", true, true, 82f, Color.magenta);
            default:
                return new BowlingStyle(type, type.ToString(), false, false, 120f, Color.white);
        }
    }
}
