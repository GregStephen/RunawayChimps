using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RunawayChimps.Tests
{
    // Production lives in Assembly-CSharp. Keep reflection at this fixture boundary
    // rather than changing the runtime assembly layout to make these tests possible.
    public sealed class VentBlowerPlayModeTests
    {
        private const string ResourcePath = "RunawayChimps_VentBlower";
        private const string RuntimeRootName = "Level1_VentRoom_Blower";
        private const float PositionTolerance = 0.00025f;
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<Scene> scenes = new List<Scene>();

        private static Type Production => Type.GetType("VentBlowerSetPiece, Assembly-CSharp", true);

        private static MethodInfo Method(string name)
        {
            MethodInfo method = Production.GetMethod(name, Members);
            Assert.That(method, Is.Not.Null, "Missing production method: " + name);
            return method;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name) return candidate;
            return null;
        }

        private GameObject Root(string name, bool active = false)
        {
            var root = new GameObject(name);
            root.SetActive(active);
            created.Add(root);
            return root;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index] != null) Object.DestroyImmediate(created[index]);
            created.Clear();
            foreach (Scene scene in scenes)
                if (scene.IsValid() && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            scenes.Clear();
        }

        private Component ImportedFixture(out Transform visual, out Transform rotor)
        {
            GameObject host = Root("VentBlowerFixture");
            host.transform.position = new Vector3(12f, 3f, -9f);
            host.transform.rotation = Quaternion.Euler(23f, 157f, -12f);
            host.transform.localScale = Vector3.one * 1.3f;
            Component component = host.AddComponent(Production);
            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            Assert.That(prefab, Is.Not.Null, "The actual approved Resources FBX must import.");
            visual = Object.Instantiate(prefab, host.transform, false).transform;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            rotor = Find(visual, "FanRotor");
            Assert.That(rotor, Is.Not.Null);
            // The host stays inactive, so Awake cannot also build a second blower.
            return component;
        }

        private sealed class MeshSnapshot
        {
            private readonly Transform transform;
            private readonly Vector3[] localPoints;
            private readonly Vector3[] worldPoints;

            public MeshSnapshot(MeshFilter filter)
            {
                transform = filter.transform;
                Bounds bounds = filter.sharedMesh.bounds;
                localPoints = new Vector3[8];
                worldPoints = new Vector3[8];
                for (int index = 0; index < 8; index++)
                {
                    localPoints[index] = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (index & 1) == 0 ? -1f : 1f,
                        (index & 2) == 0 ? -1f : 1f,
                        (index & 4) == 0 ? -1f : 1f));
                    worldPoints[index] = transform.TransformPoint(localPoints[index]);
                }
            }

            public void AssertUnchanged()
            {
                for (int index = 0; index < localPoints.Length; index++)
                    Assert.That(Vector3.Distance(transform.TransformPoint(localPoints[index]), worldPoints[index]),
                        Is.LessThan(PositionTolerance), transform.name + " moved or changed shape.");
            }
        }

        private static List<MeshSnapshot> CaptureMeshes(Transform root, Transform excludedRoot = null)
        {
            var snapshots = new List<MeshSnapshot>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && (excludedRoot == null || !filter.transform.IsChildOf(excludedRoot)))
                    snapshots.Add(new MeshSnapshot(filter));
            Assert.That(snapshots.Count, Is.GreaterThan(0));
            return snapshots;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CenteringPreservesEveryImportedMeshIncludingAuthoredLocalTransforms(bool variedRotorTransform)
        {
            Component component = ImportedFixture(out Transform visual, out Transform rotor);
            if (variedRotorTransform)
            {
                rotor.localPosition = new Vector3(0.21f, 0.34f, -0.17f);
                rotor.localRotation = Quaternion.Euler(27f, 45f, -18f);
                rotor.localScale = new Vector3(-0.7f, 1.1f, 1.4f);
            }

            Transform originalParent = rotor.parent;
            Transform hub = Find(rotor, "FanHub");
            Assert.That(hub, Is.Not.Null);
            Vector3 originalHub = hub.position;
            List<MeshSnapshot> before = CaptureMeshes(visual);
            Method("ConfigureRotor").Invoke(component, new object[] { rotor });

            Assert.That(rotor.parent.name, Is.EqualTo("FanRotor_CenteredPivot"));
            Assert.That(rotor.parent.parent, Is.SameAs(originalParent));
            Assert.That(Vector3.Distance(rotor.parent.position, originalHub), Is.LessThan(PositionTolerance));
            foreach (MeshSnapshot snapshot in before) snapshot.AssertUnchanged();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ImportedFanSpinsForTenMinutesWithoutMovingItsHubPlaneOrHousing()
        {
            Component component = ImportedFixture(out Transform visual, out Transform rotor);
            Transform hub = Find(rotor, "FanHub");
            Transform blade = Find(rotor, "Blade_01");
            Assert.That(hub, Is.Not.Null);
            Assert.That(blade, Is.Not.Null);
            MeshFilter bladeMesh = blade.GetComponentInChildren<MeshFilter>(true);
            Assert.That(bladeMesh, Is.Not.Null);
            Assert.That(bladeMesh.sharedMesh, Is.Not.Null);

            Vector3 hubPosition = hub.position;
            Vector3 normal = rotor.TransformVector(Vector3.up).normalized;
            Vector3 bladePoint = bladeMesh.sharedMesh.bounds.center;
            Vector3 initialBladePosition = bladeMesh.transform.TransformPoint(bladePoint);
            Vector3 offset = initialBladePosition - hubPosition;
            float radius = Vector3.ProjectOnPlane(offset, normal).magnitude;
            float planeOffset = Vector3.Dot(offset, normal);
            Assert.That(radius, Is.GreaterThan(0.01f), "Use a blade sample away from the spindle.");
            List<MeshSnapshot> stationaryMeshes = CaptureMeshes(visual, rotor);
            Method("ConfigureRotor").Invoke(component, new object[] { rotor });

            MethodInfo animate = Method("AnimateRotor");
            object[] step = { 1f / 60f };
            for (int frame = 1; frame <= 36000; frame++)
            {
                animate.Invoke(component, step);
                if (frame == 15)
                    Assert.That(Vector3.Distance(bladeMesh.transform.TransformPoint(bladePoint), initialBladePosition),
                        Is.GreaterThan(radius * 0.5f), "A stationary fan must not pass the pivot checks.");
                if (frame % 120 != 0) continue;

                Vector3 currentOffset = bladeMesh.transform.TransformPoint(bladePoint) - hubPosition;
                Assert.That(Vector3.Distance(hub.position, hubPosition), Is.LessThan(PositionTolerance), "Hub drift.");
                Assert.That(Vector3.Dot(rotor.TransformVector(Vector3.up).normalized, normal), Is.GreaterThan(0.99999f),
                    "The rotor must spin around its spindle, not tumble around another axis.");
                Assert.That(Vector3.ProjectOnPlane(currentOffset, normal).magnitude,
                    Is.EqualTo(radius).Within(PositionTolerance), "The blade left its original radial envelope.");
                Assert.That(Vector3.Dot(currentOffset, normal), Is.EqualTo(planeOffset).Within(PositionTolerance),
                    "The blade left its original depth plane.");
            }
            foreach (MeshSnapshot snapshot in stationaryMeshes) snapshot.AssertUnchanged();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void HousingSideTrimIsMovedOutsideTheCaseAndCorrectionIsIdempotent()
        {
            Component component = ImportedFixture(out Transform visual, out Transform rotor);
            Transform left = Find(visual, "Housing_LeftLip");
            Transform right = Find(visual, "Housing_RightLip");
            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);
            float leftY = left.localPosition.y;
            float leftZ = left.localPosition.z;
            float rightY = right.localPosition.y;
            float rightZ = right.localPosition.z;

            MethodInfo correction = Method("CorrectHousingLipOverlap");
            correction.Invoke(null, new object[] { visual });
            correction.Invoke(null, new object[] { visual });

            Assert.That(left.localPosition.x, Is.EqualTo(-0.662f).Within(PositionTolerance));
            Assert.That(right.localPosition.x, Is.EqualTo(0.662f).Within(PositionTolerance));
            Assert.That(left.localPosition.y, Is.EqualTo(leftY).Within(PositionTolerance));
            Assert.That(left.localPosition.z, Is.EqualTo(leftZ).Within(PositionTolerance));
            Assert.That(right.localPosition.y, Is.EqualTo(rightY).Within(PositionTolerance));
            Assert.That(right.localPosition.z, Is.EqualTo(rightZ).Within(PositionTolerance));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MissingHubReportsTheAssetProblemWithoutMovingTheRemainingGeometry()
        {
            Component component = ImportedFixture(out Transform visual, out Transform rotor);
            Transform hub = Find(rotor, "FanHub");
            Assert.That(hub, Is.Not.Null);
            Object.DestroyImmediate(hub.gameObject);
            List<MeshSnapshot> before = CaptureMeshes(visual);

            LogAssert.Expect(LogType.Error, new Regex("missing required 'FanHub'"));
            Method("ConfigureRotor").Invoke(component, new object[] { rotor });
            Method("AnimateRotor").Invoke(component, new object[] { 3f });

            Assert.That(Find(visual, "FanRotor_CenteredPivot"), Is.Null);
            foreach (MeshSnapshot snapshot in before) snapshot.AssertUnchanged();
            LogAssert.NoUnexpectedReceived();
        }

        private Scene VisitScene(string name, out Transform room)
        {
            Scene scene = SceneManager.CreateScene(name);
            scenes.Add(scene);
            GameObject roomObject = Root("VentRoom", true);
            SceneManager.MoveGameObjectToScene(roomObject, scene);
            room = roomObject.transform;
            return scene;
        }

        private static Transform EnsureOnceDespiteRepeatedCalls(Scene scene, Transform room)
        {
            MethodInfo ensure = Method("EnsureBlower");
            ensure.Invoke(null, new object[] { scene });
            ensure.Invoke(null, new object[] { scene });
            int count = 0;
            foreach (Transform child in room)
                if (child.name == RuntimeRootName) count++;
            Assert.That(count, Is.EqualTo(1), "Repeated setup must not duplicate the set piece.");
            Transform root = room.Find(RuntimeRootName);
            Assert.That(Vector3.Distance(root.localPosition, new Vector3(-0.25f, 0.72f, 22.46f)),
                Is.LessThan(PositionTolerance));
            Assert.That(root.GetComponents(Production).Length, Is.EqualTo(1));
            Assert.That(Find(root, "FanRotor").parent.name, Is.EqualTo("FanRotor_CenteredPivot"));
            AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
            Assert.That(sources.Length, Is.EqualTo(1));
            Assert.That(sources[0].clip, Is.Not.Null);
            Assert.That(sources[0].loop, Is.True);
            Assert.That(sources[0].spatialBlend, Is.EqualTo(1f));
            Assert.That(sources[0].volume, Is.GreaterThanOrEqualTo(0.7f));
            Assert.That(sources[0].minDistance, Is.GreaterThanOrEqualTo(1.4f));
            Assert.That(sources[0].maxDistance, Is.GreaterThanOrEqualTo(9.5f));
            var samples = new float[Mathf.Min(2048, sources[0].clip.samples)];
            Assert.That(sources[0].clip.GetData(samples, 0), Is.True);
            float peak = 0f;
            foreach (float sample in samples)
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            Assert.That(peak, Is.GreaterThan(0.08f), "Motor loop is too quiet at the clip level.");
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            Assert.That(lights.Length, Is.EqualTo(1));
            Assert.That(lights[0].transform.parent.name, Is.EqualTo("RedLightLens"));
            Assert.That(lights[0].shadows, Is.EqualTo(LightShadows.None));
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                Assert.That(collider.enabled, Is.False, "Decorative geometry must not obstruct the vent route.");

            int texturedMaterials = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.name.Contains("RedMaintenanceLens"))
                        continue;
                    Assert.That(material.mainTexture, Is.Not.Null, material.name + " must keep its authored surface texture.");
                    texturedMaterials++;
                }
            }
            Assert.That(texturedMaterials, Is.GreaterThan(0));
            return root;
        }

        [UnityTest]
        public IEnumerator RepeatedSetupAndSceneReentryKeepOneWorkingFanLightAndMotorPerVisit()
        {
            // A distinct test scene avoids invoking unrelated full-Level1 gameplay bootstraps.
            // Exercise the real scene-scoped factory and Awake/Update using the actual FBX.
            string name = "VentBlowerTest_" + Guid.NewGuid().ToString("N");
            Scene firstScene = VisitScene(name, out Transform firstRoom);
            Transform firstRoot = EnsureOnceDespiteRepeatedCalls(firstScene, firstRoom);
            Transform firstHub = Find(firstRoot, "FanHub");
            Transform firstBlade = Find(firstRoot, "Blade_01");
            Vector3 originalHub = firstHub.position;
            Quaternion originalBladeRotation = firstBlade.rotation;
            AudioSource firstMotor = firstRoot.GetComponent<AudioSource>();
            Light firstLight = firstRoot.GetComponentInChildren<Light>();
            AudioClip cachedLoop = firstMotor.clip;
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(firstHub.position, originalHub), Is.LessThan(PositionTolerance));
            Assert.That(Quaternion.Angle(firstBlade.rotation, originalBladeRotation), Is.GreaterThan(0.01f),
                "Update must actually advance the centered rotor.");

            yield return SceneManager.UnloadSceneAsync(firstScene);
            Assert.That(firstRoot == null && firstMotor == null && firstLight == null, Is.True,
                "The previous visit's visual, motor and light must unload with its scene.");
            Scene secondScene = VisitScene(name, out Transform secondRoom);
            Transform secondRoot = EnsureOnceDespiteRepeatedCalls(secondScene, secondRoom);
            Assert.That(secondRoot.GetComponent<AudioSource>().clip, Is.SameAs(cachedLoop));
            Transform secondHub = Find(secondRoot, "FanHub");
            Assert.That(Vector3.Distance(secondHub.position, originalHub), Is.LessThan(PositionTolerance));
            Transform secondBlade = Find(secondRoot, "Blade_01");
            Quaternion reentryBladeRotation = secondBlade.rotation;
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(secondHub.position, originalHub), Is.LessThan(PositionTolerance));
            Assert.That(Quaternion.Angle(secondBlade.rotation, reentryBladeRotation), Is.GreaterThan(0.01f));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
