using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace RunawayChimps.Level2
{
    // Turns the v0.4 fuse/socket blockout markers into a functional prototype at runtime.
    // This deliberately avoids final art/audio and Listener AI while the interaction scale is being tested.
    public static class Level2FuseRuntimeBootstrap
    {
        const string SceneName = "Level2_BehavioralConditioning_Blockout";

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

            for (int i = 1; i <= 4; i++)
            {
                var start = GameObject.Find($"Fuse{i}Start");
                if (start != null) CreateFuse(i, start.transform.position, scene);

                var placeholder = GameObject.Find($"Fuse_{i}_Personal_Cylindrical_Placeholder");
                if (placeholder != null) placeholder.SetActive(false);

                var marker = GameObject.Find($"FuseSocket{i}Marker");
                if (marker != null)
                {
                    var socket = marker.AddComponent<Level2FuseSocket>();
                    socket.socketId = i;
                    socket.chargeSeconds = 10f;
                    var trigger = marker.AddComponent<BoxCollider>();
                    trigger.size = new Vector3(.7f, .7f, .65f);
                    trigger.isTrigger = true;
                    objective.RegisterSocket(socket);

                    var leverObject = GameObject.Find($"Fuse_Socket_{i}_Lever_Handle");
                    if (leverObject != null)
                    {
                        var leverCollider = leverObject.GetComponent<Collider>() ?? leverObject.AddComponent<BoxCollider>();
                        leverCollider.isTrigger = true;
                        var lever = leverObject.AddComponent<Level2ChargeLever>();
                        lever.socket = socket;
                    }
                }

                var search = FindByPrefix($"Fuse_Cache_{i}_", "_DoorOrDrawer");
                if (search != null && search.GetComponent<Level2NoisySearchContainer>() == null)
                    search.AddComponent<Level2NoisySearchContainer>();
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
            root.transform.position = position + Vector3.up * .12f;
            SceneManager.MoveGameObjectToScene(root, scene);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = .65f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = .065f;
            capsule.height = .42f;
            capsule.direction = 1;

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = true;
            root.AddComponent<HeldItemCollisionMode>();
            var fuse = root.AddComponent<Level2Fuse>();
            fuse.fuseId = id;

            var dark = MakeMaterial(new Color(.09f, .11f, .12f));
            var metal = MakeMaterial(new Color(.28f, .31f, .32f));
            var amber = MakeMaterial(new Color(.94f, .51f, .13f));

            AddCylinder(root.transform, "Core", .115f, .34f, dark, Vector3.zero);
            AddCylinder(root.transform, "EndCap_A", .145f, .055f, metal, new Vector3(0, .195f, 0));
            AddCylinder(root.transform, "EndCap_B", .145f, .055f, metal, new Vector3(0, -.195f, 0));
            AddCylinder(root.transform, "StatusBand", .126f, .045f, amber, new Vector3(0, .08f, 0));
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
            var material = new Material(shader) { color = color };
            material.enableInstancing = true;
            return material;
        }
    }
}
