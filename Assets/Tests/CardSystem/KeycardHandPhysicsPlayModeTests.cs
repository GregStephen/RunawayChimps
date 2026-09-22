using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using Object = UnityEngine.Object;

namespace RunawayChimps.Tests
{
    // Actual production queries and forces run in Unity here. Reflection only
    // bridges the test asmdef to the project's existing Assembly-CSharp layout.
    public sealed class KeycardHandPhysicsPlayModeTests
    {
        readonly List<GameObject> created = new List<GameObject>();
        readonly Vector3 origin = new Vector3(80f, 20f, 80f);
        static readonly Vector3 castDirectionSeed = new Vector3(0.23f, -0.17f, 0.61f);
        static readonly Vector3[] finiteCastDirections =
        {
            castDirectionSeed * 0.037f,
            castDirectionSeed * 0.0001f,
            castDirectionSeed * 0.000001f,
            castDirectionSeed * 1200f,
            castDirectionSeed * 1e-30f,
            castDirectionSeed * 1e30f,
        };
        static readonly Vector3[] invalidCastDirections =
        {
            Vector3.zero,
            new Vector3(float.NaN, 0f, 1f),
            new Vector3(0f, float.NaN, 1f),
            new Vector3(0f, 1f, float.NaN),
            new Vector3(float.PositiveInfinity, 0f, 1f),
            new Vector3(0f, float.NegativeInfinity, 1f),
            new Vector3(0f, 1f, float.PositiveInfinity),
        };
        XRInteractionManager manager;
        static Type Production(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        static object Call(string method, params object[] arguments) =>
            Production("KeycardHandPhysics").GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, arguments);

        [SetUp]
        public void Setup()
        {
            var root = new GameObject("HandPhysicsTestManager");
            created.Add(root);
            manager = root.AddComponent<XRInteractionManager>();
        }

        [TearDown]
        public void Teardown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index] != null) Object.DestroyImmediate(created[index]);
            created.Clear();
        }

        Rigidbody Card(Vector3 offset, Vector3? size = null)
        {
            var root = new GameObject("HandPhysicsCard");
            root.SetActive(false);
            root.transform.position = origin + offset;
            created.Add(root);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = size ?? new Vector3(0.1f, 0.005f, 0.06f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            root.AddComponent(Production("KeyCard"));
            root.GetComponent<XRGrabInteractable>().interactionManager = manager;
            root.SetActive(true);
            Rigidbody body = root.GetComponent<Rigidbody>();
            body.useGravity = false;
            return body;
        }

        BoxCollider Solid(Vector3 offset, bool trigger = false)
        {
            var root = new GameObject("HandPhysicsWorldSolid");
            created.Add(root);
            root.transform.position = origin + offset;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 1f, 0.05f);
            collider.isTrigger = trigger;
            return collider;
        }

        bool EnvironmentHit(bool sphere, out RaycastHit hit)
        {
            return EnvironmentHit(sphere, Vector3.forward, out hit);
        }

        bool EnvironmentHit(bool sphere, Vector3 direction, out RaycastHit hit)
        {
            Physics.SyncTransforms();
            object[] args = sphere
                ? new object[] { origin, 0.025f, direction, default(RaycastHit), 1f, 1 }
                : new object[] { origin, direction, default(RaycastHit), 1f, 1 };
            bool found = (bool)Call(sphere ? "SphereCastEnvironment" : "RaycastEnvironment", args);
            hit = (RaycastHit)args[sphere ? 3 : 2];
            return found;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LooseCardAndTriggerCannotHideTheWallBehindThem(bool sphere)
        {
            Card(Vector3.forward * 0.25f);
            Solid(Vector3.forward * 0.1f, true);
            BoxCollider wall = Solid(Vector3.forward * 0.75f);
            Assert.That(EnvironmentHit(sphere, out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(wall));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullQueryBufferStillFindsTheWorldSurface(bool sphere)
        {
            for (int index = 0; index < 40; index++)
                Card(Vector3.forward * (0.1f + index * 0.015f), new Vector3(0.02f, 0.02f, 0.003f));
            BoxCollider wall = Solid(Vector3.forward * 0.9f);
            Assert.That(EnvironmentHit(sphere, out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(wall), "Saturated unordered hits must not hide the wall.");
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void DirectionScalePreservesTheWallBehindLooseCards(bool sphere, bool saturated)
        {
            Vector3 unitDirection = castDirectionSeed.normalized;
            if (saturated)
            {
                for (int index = 0; index < 40; index++)
                    Card(unitDirection * (0.1f + index * 0.015f), new Vector3(0.02f, 0.02f, 0.003f));
            }
            else
                Card(unitDirection * 0.25f);

            BoxCollider wall = Solid(unitDirection * 0.9f);
            wall.transform.rotation = Quaternion.LookRotation(unitDirection);
            Assert.That(EnvironmentHit(sphere, unitDirection, out RaycastHit baseline), Is.True);
            Assert.That(baseline.collider, Is.SameAs(wall));

            // Direction magnitude is independent of maximum cast distance. In
            // Gorilla's solver even a tiny movement has a meaningful padded cast.
            // Include directions above/below Vector3.Normalize's epsilon and
            // finite values whose squared magnitude would underflow or overflow.
            foreach (Vector3 direction in finiteCastDirections)
            {
                string context = "Direction " + direction.ToString("G9") + ", sphere=" + sphere + ", saturated=" + saturated;
                Assert.That(EnvironmentHit(sphere, direction, out RaycastHit hit), Is.True, context);
                Assert.That(hit.collider, Is.SameAs(wall), context);
                Assert.That(hit.distance, Is.EqualTo(baseline.distance).Within(0.0001f), context);
                Assert.That(Vector3.Distance(hit.point, baseline.point), Is.LessThan(0.0001f), context);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ZeroAndNonfiniteDirectionsReturnNoHitWithoutPhysicsAssertions(bool sphere)
        {
            Card(Vector3.forward * 0.25f);
            Solid(Vector3.forward * 0.75f);
            foreach (Vector3 direction in invalidCastDirections)
            {
                Assert.That(EnvironmentHit(sphere, direction, out RaycastHit hit), Is.False);
                Assert.That(hit.collider, Is.Null);
                Assert.That(hit.distance, Is.Zero);
                Assert.That(hit.point, Is.EqualTo(Vector3.zero));
                Assert.That(hit.normal, Is.EqualTo(Vector3.zero));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void SphereFallbackNeverReturnsAnInitialOverlapWorldOriginSentinel(bool wallAhead, bool saturated)
        {
            // Far from world origin so PhysX's Vector3.zero overlap sentinel is
            // clearly distinguishable from a real surface in this test scene.
            Solid(Vector3.zero);
            if (saturated)
            {
                for (int index = 0; index < 40; index++)
                    Card(Vector3.forward * (0.1f + index * 0.015f), new Vector3(0.02f, 0.02f, 0.003f));
            }
            else
                Card(Vector3.forward * 0.25f);
            BoxCollider wall = wallAhead ? Solid(Vector3.forward * 0.9f) : null;
            Assert.That(EnvironmentHit(true, out RaycastHit hit), Is.EqualTo(wallAhead));
            if (wallAhead)
            {
                Assert.That(hit.collider, Is.SameAs(wall));
                Assert.That(Vector3.Distance(hit.point, origin), Is.LessThan(1f));
            }
        }

        [Test]
        public void KinematicCardRemainsAnObstacle()
        {
            Rigidbody card = Card(Vector3.forward * 0.25f);
            card.isKinematic = true;
            Assert.That(EnvironmentHit(true, out RaycastHit hit), Is.True);
            Assert.That(hit.rigidbody, Is.SameAs(card));
        }

        [Test]
        public void OtherDynamicPropsKeepTheirExistingLocomotionBehavior()
        {
            BoxCollider prop = Solid(Vector3.forward * 0.25f);
            prop.gameObject.AddComponent<Rigidbody>().useGravity = false;
            Assert.That(EnvironmentHit(true, out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(prop));
        }

        [UnityTest]
        public IEnumerator OffCenterHandSweepMovesAndRotatesASleepingLooseCard()
        {
            Rigidbody card = Card(Vector3.forward * 0.18f);
            card.Sleep();
            Physics.SyncTransforms();
            Vector3 start = origin + Vector3.right * 0.04f;
            Call("PushLooseCards", start, start + Vector3.forward * 0.3f, 0.05f, 0.02f, 1);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(card.velocity.z, Is.GreaterThan(0.1f));
            Assert.That(card.velocity.magnitude, Is.LessThanOrEqualTo(1.501f), "Hand impulses must stay bounded.");
            Assert.That(Mathf.Abs(card.angularVelocity.y), Is.GreaterThan(0.1f), "Off-center contact should tip/turn the card.");
        }

        [UnityTest]
        public IEnumerator HandSweepCannotNudgeACardThroughAWorldWall()
        {
            Rigidbody card = Card(Vector3.forward * 0.4f);
            Solid(Vector3.forward * 0.2f);
            Physics.SyncTransforms();
            Call("PushLooseCards", origin, origin + Vector3.forward * 0.5f, 0.05f, 0.02f, 1);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(card.velocity.sqrMagnitude, Is.LessThan(0.000001f));
        }

        [UnityTest]
        public IEnumerator TeleportSizedHandJumpDoesNotLaunchACard()
        {
            Rigidbody card = Card(Vector3.forward * 0.18f);
            Physics.SyncTransforms();
            Call("PushLooseCards", origin, origin + Vector3.forward * 2f, 0.05f, 0.02f, 1);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(card.velocity.sqrMagnitude, Is.LessThan(0.000001f));
        }

        [UnityTest]
        public IEnumerator RecentlyDroppedCardCanMoveBeforeItsHeldLayerRestores()
        {
            Rigidbody card = Card(Vector3.forward * 0.18f);
            int heldLayer = LayerMask.NameToLayer("HeldItem");
            Assert.That(heldLayer, Is.GreaterThanOrEqualTo(0));
            card.gameObject.layer = heldLayer;
            Physics.SyncTransforms();
            Call("PushLooseCards", origin, origin + Vector3.forward * 0.3f, 0.05f, 0.02f, 1);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(card.velocity.z, Is.GreaterThan(0.1f));
        }

        [UnityTest]
        public IEnumerator SeveralRenderUpdatesCannotStackImpulsesBeforePhysics()
        {
            Rigidbody card = Card(Vector3.forward * 0.18f);
            Physics.SyncTransforms();
            for (int update = 0; update < 5; update++)
                Call("PushLooseCards", origin, origin + Vector3.forward * 0.3f, 0.05f, 0.011f, 1);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(card.velocity.z, Is.GreaterThan(0.1f));
            Assert.That(card.velocity.magnitude, Is.LessThanOrEqualTo(1.501f));
        }

        [UnityTest]
        public IEnumerator RetractingATouchingHandDoesNotPullAnUngrippedCard()
        {
            Rigidbody card = Card(Vector3.forward * 0.07f);
            Physics.SyncTransforms();
            Call("PushLooseCards", origin, origin - Vector3.forward * 0.15f, 0.05f, 0.02f, 1);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(card.velocity.sqrMagnitude, Is.LessThan(0.000001f));
        }

        [UnityTest]
        public IEnumerator NonClosingFirstHandDoesNotSuppressASecondHandContact()
        {
            Rigidbody card = Card(Vector3.forward * 0.18f);
            card.velocity = Vector3.forward * 2f;
            Physics.SyncTransforms();
            Call("PushLooseCards", origin, origin + Vector3.forward * 0.3f, 0.05f, 0.02f, 1);
            Call("PushLooseCards", origin + Vector3.forward * 0.35f, origin + Vector3.forward * 0.05f, 0.05f, 0.02f, 1);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(card.velocity.z, Is.EqualTo(0.5f).Within(0.01f));
        }
    }
}
