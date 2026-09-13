using UnityEngine;
using UnityEngine.Rendering;

namespace RunawayChimps.ThreatFeedback
{
    public sealed class ThreatVignetteView : MonoBehaviour
    {
        private const string ViewName = "Local_Threat_Vignette";
        private static readonly int ThreatId = Shader.PropertyToID("_Threat");
        private Material material;
        private MeshRenderer meshRenderer;

        public static ThreatVignetteView Ensure(Camera camera)
        {
            if (camera == null) return null;
            var existing = camera.transform.Find(ViewName);
            if (existing != null)
            {
                var view = existing.GetComponent<ThreatVignetteView>();
                if (view != null) return view;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = ViewName;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = Vector3.forward * Mathf.Max(0.32f, camera.nearClipPlane + 0.08f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(4f, 4f, 1f);
            var viewComponent = go.AddComponent<ThreatVignetteView>();
            viewComponent.Initialize();
            return viewComponent;
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null || material != null) return;
            var shader = Shader.Find("RunawayChimps/ThreatVignette");
            if (shader == null) { Debug.LogError("Threat vignette shader is missing.", this); enabled = false; return; }
            material = new Material(shader) { name = "Runtime Threat Vignette" };
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.sortingOrder = short.MaxValue - 10;
            SetThreat(0f);
        }

        public void SetThreat(float threat)
        {
            Initialize();
            threat = Mathf.Clamp01(threat);
            if (material != null) material.SetFloat(ThreatId, threat);
            if (meshRenderer != null) meshRenderer.enabled = threat > 0.001f;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
