using UnityEngine;

/// <summary>
/// Compatibility binding for one legacy, fieldless MonoBehaviour serialized on the
/// authored Hub StartLevel1Button/ButtonTrigger object.
///
/// The original script asset for this GUID was never tracked in the repository and
/// the component has no serialized state or runtime references. The real interaction
/// is owned by PhysicalButton -> SectorDoor.Travel. Keeping this marker deliberately
/// inert resolves Unity's Missing (Mono Script) entry without changing gameplay.
/// </summary>
[AddComponentMenu("")]
public sealed class LegacyHubButtonMarker : MonoBehaviour
{
}
