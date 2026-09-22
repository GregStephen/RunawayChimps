/// <summary>
/// Shared physical credential identity used by gameplay keycards and matching readers.
/// Auto exists only as a compatibility fallback for legacy/name-authored content.
/// Numeric values are serialized in Unity assets/scenes, so existing values must remain stable.
/// </summary>
public enum KeycardCredential
{
    Auto = 0,
    AmberTriangle = 1,
    CyanThreeBars = 2,
    RedCircle = 3,
    VioletDiamond = 4,
}
