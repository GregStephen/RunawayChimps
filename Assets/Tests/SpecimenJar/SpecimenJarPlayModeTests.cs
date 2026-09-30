#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RunawayChimps.Tests
{
    // Uses the serialized asset and real Unity collider queries. Production remains in
    // Assembly-CSharp; reflection at this test boundary avoids changing project assemblies.
    public sealed class SpecimenJarPlayModeTests
    {
        private const string Prefab = "Assets/RunawayChimps/Toys/ReactiveSpecimenJar/ReactiveSpecimenJar.prefab";
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly List<GameObject> created = new List<GameObject>();
        private static Type Production => Type.GetType("RunawayChimps.Toys.ReactiveSpecimenJar, Assembly-CSharp", true);

        private GameObject Root(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }
        private Component Fixture()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            Assert.That(asset, Is.Not.Null, "Actual prefab must import.");
            var instance = Object.Instantiate(asset);
            created.Add(instance);
            // Manual calls keep these tests deterministic, independent of startup/Photon.
            var jar = (Behaviour)instance.GetComponent(Production);
            Assert.That(jar, Is.Not.Null);
            jar.enabled = false;
            Physics.SyncTransforms();
            return jar;
        }
        private static object Invoke(Component c, string name, params object[] args)
            => Production.GetMethod(name, All).Invoke(c, args);
        private static T Field<T>(Component c, string name) => (T)Production.GetField(name, All).GetValue(c);
        private static void Set(Component c, string name, object value) => Production.GetField(name, All).SetValue(c, value);
        private static T Property<T>(Component c, string name) => (T)Production.GetProperty(name, All).GetValue(c);
        private static bool Sample(Component jar, Transform hand, object history, bool tracked = true, float dt = .02f)
        {
            Physics.SyncTransforms();
            object[] args = { hand, tracked, history, dt, false };
            Invoke(jar, "SampleHand", args);
            return (bool)args[4];
        }
        private static void Arm(Component jar, Transform hand, object history)
        {
            hand.position = new Vector3(0, 0, .32f);
            for (int i = 0; i < 12; i++) Assert.That(Sample(jar, hand, history), Is.False);
        }
        [TearDown]
        public void Teardown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void SerializedAssetHasOriginalMeshesMaterialsAndNoNetworkOrGrabComponents()
        {
            var jar = Fixture();
            Transform eye = Field<Transform>(jar, "specimen");
            Assert.That(eye.parent, Is.SameAs(jar.transform));
            Assert.That(Field<BoxCollider>(jar, "glass").isTrigger, Is.False);
            Assert.That(jar.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(3));
            foreach (var mf in jar.GetComponentsInChildren<MeshFilter>())
            {
                Assert.That(mf.sharedMesh, Is.Not.Null);
                Assert.That(mf.sharedMesh.vertexCount, Is.GreaterThan(0));
                foreach (var material in mf.GetComponent<Renderer>().sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null);
                    Assert.That(material.shader.name, Is.EqualTo("Standard"));
                }
            }
            foreach (Component c in jar.GetComponentsInChildren<Component>())
            {
                Assert.That(c, Is.Not.Null, "No missing scripts.");
                Assert.That(c.GetType().Name, Is.Not.EqualTo("PhotonView"));
                Assert.That(c.GetType().Name, Is.Not.EqualTo("XRGrabInteractable"));
                Assert.That(c, Is.Not.InstanceOf<Rigidbody>());
            }
        }

        [Test]
        public void ProjectedMotionContainsWholeSpecimenForAllRotationsAndBreathing()
        {
            var jar = Fixture();
            Vector3 ext = Field<Vector3>(jar, "motionExtents");
            var mesh = Field<Transform>(jar, "specimen").GetComponent<MeshFilter>().sharedMesh;
            var rng = new System.Random(901);
            for (int i = 0; i < 200; ++i)
            {
                var point = new Vector3((float)rng.NextDouble()*4-2, (float)rng.NextDouble()*4-2, (float)rng.NextDouble()*4-2);
                Vector3 bounded = (Vector3)Invoke(jar, "Bound", point);
                float q = bounded.x*bounded.x/(ext.x*ext.x)+bounded.y*bounded.y/(ext.y*ext.y)+bounded.z*bounded.z/(ext.z*ext.z);
                Assert.That(q, Is.LessThanOrEqualTo(1.00001f));
                Quaternion rotation = Quaternion.Euler(i*17, i*31, i*13);
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 p = bounded + rotation * vertex * 1.015f;
                    Assert.That(Mathf.Abs(p.x), Is.LessThan(.17f));
                    Assert.That(Mathf.Abs(p.y), Is.LessThan(.23f));
                    Assert.That(Mathf.Abs(p.z), Is.LessThan(.17f));
                }
            }
        }

        [TestCase("left")]
        [TestCase("right")]
        public void GenuineTapRearmsOnlyAfterWithdrawalDespiteDuplicateHandColliders(string field)
        {
            var jar = Fixture();
            Transform hand = Root("Local sampled hand").transform;
            hand.gameObject.AddComponent<SphereCollider>();
            hand.gameObject.AddComponent<BoxCollider>();
            object history = Field<object>(jar, field);
            Arm(jar, hand, history);
            hand.position = new Vector3(0, 0, .222f);
            Assert.That(Sample(jar, hand, history), Is.True);
            for (int i = 0; i < 100; ++i) Assert.That(Sample(jar, hand, history), Is.False);
            Arm(jar, hand, history);
            hand.position = new Vector3(0, 0, .222f);
            Assert.That(Sample(jar, hand, history), Is.True);
        }

        [Test]
        public void SlowEntryAndRestingAccelerationAreNotTaps()
        {
            var jar = Fixture(); var hand = Root("Slow hand").transform; object history = Field<object>(jar, "left");
            Arm(jar, hand, history);
            for (float z = .319f; z > .20f; z -= .001f)
            { hand.position = new Vector3(0, 0, z); Assert.That(Sample(jar, hand, history), Is.False); }
            hand.position = Vector3.zero;
            Assert.That(Sample(jar, hand, history), Is.False);
        }

        [Test]
        public void LostTrackingAndPositionDiscontinuityDisarmBothSampleAndReacquisition()
        {
            var jar = Fixture(); var hand = Root("Tracking hand").transform; object history = Field<object>(jar, "left");
            Arm(jar, hand, history);
            hand.position = Vector3.zero; // > maximumHandStep: not player-authored contact.
            Assert.That(Sample(jar, hand, history), Is.False);
            Assert.That(Sample(jar, hand, history), Is.False);
            Arm(jar, hand, history);
            Assert.That(Sample(jar, hand, history, false), Is.False);
            hand.position = new Vector3(0, 0, .222f);
            Assert.That(Sample(jar, hand, history), Is.False);
        }

        [Test]
        public void ExplicitResetClearsGazeReactionAndContactAndRestoresAuthoredPose()
        {
            var jar = Fixture(); Transform eye = Field<Transform>(jar, "specimen");
            var state = Field<object>(jar, "state");
            state.GetType().GetMethod("TryTap").Invoke(state, new object[] { Time.unscaledTime, .5f, .4f });
            Assert.That(Property<int>(jar, "ReactionCount"), Is.EqualTo(1));
            eye.localPosition = Vector3.one;
            eye.localRotation = Quaternion.Euler(30, 40, 50);
            eye.localScale = Vector3.one * .5f;
            Invoke(jar, "ResetState");
            Assert.That(Property<int>(jar, "ReactionCount"), Is.Zero);
            Assert.That(Property<int>(jar, "WatchCount"), Is.Zero);
            Assert.That(Property<int>(jar, "SelectedHand"), Is.EqualTo(-1));
            Assert.That(eye.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(eye.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(eye.localScale, Is.EqualTo(Vector3.one));
        }

        [TestCase("OnDisable", false)]
        [TestCase("OnApplicationPause", true)]
        [TestCase("OnApplicationFocus", false)]
        public void LifecycleCallbacksRestoreNeutralState(string callback, bool value)
        {
            var jar = Fixture(); Transform eye = Field<Transform>(jar, "specimen");
            eye.localPosition = Vector3.one;
            if (callback == "OnDisable") Invoke(jar, callback); else Invoke(jar, callback, value);
            Assert.That(eye.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Property<int>(jar, "SelectedHand"), Is.EqualTo(-1));
        }

        [Test]
        public void UnrelatedRemoteHandHierarchyCannotBecomeAnEligibleLocalHand()
        {
            var jar = Fixture();
            var xrType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils", true);
            var localOrigin = Root("Test local origin").AddComponent(xrType);
            Set(jar, "origin", localOrigin);
            var remote = Root("Unrelated remote hand");
            remote.AddComponent<SphereCollider>();
            remote.AddComponent<BoxCollider>();
            Assert.That((bool)Invoke(jar, "EligibleLocalHand", remote.transform), Is.False);
            // This exercises hierarchy isolation, not Photon transport or real two-client behavior.
        }

        [Test]
        public void DesktopPreviewRequiresLiveHeadAndAtLeastOneHand()
        {
            var jar = Fixture();
            Set(jar, "previewHead", Root("Head").transform);
            var left = Root("Left"); Set(jar, "previewLeftHand", left.transform);
            object[] args = { true, null, null, null, false, false };
            Assert.That((bool)Invoke(jar, "ReadViewer", args), Is.True);
            left.SetActive(false);
            Assert.That((bool)Invoke(jar, "ReadViewer", args), Is.False);
            left.SetActive(true); Set(jar, "previewHead", null);
            Assert.That((bool)Invoke(jar, "ReadViewer", args), Is.False);
        }
    }
}
#endif
