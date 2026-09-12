using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace RunawayChimps.Level2
{
    // PROTOTYPE-ONLY adapter: converts v0.4 generated markers into interactive objects at runtime.
    // Interaction dimensions are intentionally based on the active gorilla rig: ~10 cm hand-contact
    // diameter, ~36 cm body width, ~1.16 m body capsule height, and 1.5 m max arm length.
    // Replace with authored prefabs after Unity/XR scale validation.
    public static class Level2FuseRuntimeBootstrap
    {
        const string SceneName = "Level2_BehavioralConditioning_Blockout";

        // Hand-scale prototype dimensions. These should stay human/gorilla-hand sized even though
        // the surrounding room and Listener are intentionally oversized.
        const float FuseLength = .20f;
        const float FuseCoreDiameter = .052f;
        const float FuseEndCapDiameter = .068f;
        const float FuseColliderRadius = .031f;
        const float FuseColliderHeight = .22f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Setup(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Setup(scene);

        static void Setup(Scene scene)
        {
            if (!scene.IsValid() || scene.name != SceneName || Object.FindObjectOfType<Level2FuseObjective>() != null) return;

            var objective = new GameObject("Level2FuseObjective_Runtime").AddComponent<Level2FuseObjective>();
            SceneManager.MoveGameObjectToScene(objective.gameObject, scene);
            var power = objective.gameObject.AddComponent<Level2PowerState>();
            objective.OnPowerRestored.AddListener(power.RestorePower);
            objective.gameObject.AddComponent<Level2NoiseDebug>();

            for (int i = 1; i <= 4; i++)
            {
                var start = GameObject.Find($"Fuse{i}Start");
                if (start != null) CreateFuse(i, start.transform.position, scene);
                var placeholder = GameObject.Find($"Fuse_{i}_Personal_Cylindrical_Placeholder");
                if (placeholder != null) placeholder.SetActive(false);

                var marker = GameObject.Find($"FuseSocket{i}Marker");
                if (marker != null)
                {
                    var trigger = marker.GetComponent<BoxCollider>() ?? marker.AddComponent<BoxCollider>();
                    trigger.size = new Vector3(.26f, .28f, .22f);
                    trigger.isTrigger = true;
                    var socket = marker.AddComponent<Level2FuseSocket>();
                    socket.socketId = i;
                    socket.chargeSeconds = 10f;
                    var indicatorObject = GameObject.Find($"Fuse_Socket_{i}_Charge_Indicator");
                    if (indicatorObject != null) socket.indicator = indicatorObject.GetComponent<Renderer>();
                    objective.RegisterSocket(socket);

                    var leverObject = GameObject.Find($"Fuse_Socket_{i}_Lever_Handle");
                    if (leverObject != null)
                    {
                        // Keep the interaction volume in world-scale metres instead of inheriting the
                        // decorative lever mesh scale. A 10 cm hand proxy can contact this reliably.
                        var leverTrigger = new GameObject($"FuseLever{i}Trigger_Runtime");
                        leverTrigger.transform.SetPositionAndRotation(leverObject.transform.position, leverObject.transform.rotation);
                        SceneManager.MoveGameObjectToScene(leverTrigger, scene);
                        var leverCollider = leverTrigger.AddComponent<BoxCollider>();
                        leverCollider.size = new Vector3(.16f, .30f, .16f);
                        leverCollider.isTrigger = true;
                        var lever = leverTrigger.AddComponent<Level2ChargeLever>();
                        lever.socket = socket;
                    }
                }

                var search = FindByPrefix($"Fuse_Cache_{i}_", "_DoorOrDrawer");
                if (search != null)
                {
                    var noisy = search.GetComponent<Level2NoisySearchContainer>() ?? search.AddComponent<Level2NoisySearchContainer>();
                    noisy.movingPart = search.transform;
                    var handle = search.AddComponent<Level2SearchContainerHandle>();
                    handle.container = noisy;
                    var handleCollider = search.GetComponent<BoxCollider>() ?? search.AddComponent<BoxCollider>();
                    handleCollider.isTrigger = true;
                }
            }
        }

        static GameObject FindByPrefix(string prefix, string suffix)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>())
                if (t.name.StartsWith(prefix) && t.name.EndsWith(suffix)) return t.gameObject;
            return null;
        }

        static void CreateFuse(int id, Vector3 position, Scene scene)
        {
            var root = new GameObject($"Level2Fuse_{id}_Runtime");
            root.transform.position = position + Vector3.up * .06f;
            SceneManager.MoveGameObjectToScene(root, scene);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = .25f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = FuseColliderRadius;
            capsule.height = FuseColliderHeight;
            capsule.direction = 1;

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = true;
            root.AddComponent<HeldItemCollisionMode>();
            var fuse = root.AddComponent<Level2Fuse>();
            fuse.fuseId = id;

            var dark = MakeMaterial(new Color(.09f, .11f, .12f));
            var metal = MakeMaterial(new Color(.28f, .31f, .32f));
            var amber = MakeMaterial(new Color(.94f, .51f, .13f));

            const float endCapLength = .028f;
            AddCylinder(root.transform, "Core", FuseCoreDiameter, FuseLength - endCapLength * 2f, dark, Vector3.zero);
            AddCylinder(root.transform, "EndCap_A", FuseEndCapDiameter, endCapLength, metal,
                new Vector3(0, FuseLength * .5f - endCapLength * .5f, 0));
            AddCylinder(root.transform, "EndCap_B", FuseEndCapDiameter, endCapLength, metal,
                new Vector3(0, -FuseLength * .5f + endCapLength * .5f, 0));
            AddCylinder(root.transform, "StatusBand", .058f, .024f, amber, new Vector3(0, .035f, 0));
        }

        static void AddCylinder(Transform parent, string name, float diameter, float length, Material material, Vector3 localPosition)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(diameter, length * .5f, diameter);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { color = color };
            material.enableInstancing = true;
            return material;
        }
    }
}
