using UnityEngine;

public class GroundDebug : MonoBehaviour
{
    public float rayUp = 0.5f;
    public float rayDown = 2f;

    private string _last;

    void Update()
    {
        var start = transform.position + Vector3.up * rayUp;
        if (Physics.Raycast(start, Vector3.down, out var hit, rayUp + rayDown, ~0, QueryTriggerInteraction.Ignore))
        {
            var col = hit.collider;
            var pm = col.sharedMaterial;
            string s = $"Ground: {col.name} | layer={LayerMask.LayerToName(col.gameObject.layer)} | physMat={(pm ? pm.name : "none")}";
            if (s != _last)
            {
                Debug.Log("[GroundDebug] " + s);
                _last = s;
            }
        }
    }
}
