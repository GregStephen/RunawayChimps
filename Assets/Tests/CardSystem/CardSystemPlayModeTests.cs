using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using Object = UnityEngine.Object;

namespace RunawayChimps.Tests
{
    // Production currently lives in Assembly-CSharp. Test assemblies cannot reference
    // a predefined assembly, so reflection is confined to this fixture boundary.
    // No production APIs/assembly layout are changed merely to accommodate tests.
    [NonParallelizable]
    public sealed class CardSystemPlayModeTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<Object> created = new List<Object>();
        private XRInteractionManager manager;
        private readonly Vector3 origin = new Vector3(40f, 20f, 40f);
        private Material standby;
        private Material accepted;

        private static Type Production(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(Component component, string method, params object[] arguments)
        {
            MethodInfo member = component.GetType().GetMethod(method, Members);
            Assert.That(member, Is.Not.Null, "Missing production method: " + method);
            return member.Invoke(component, arguments);
        }
        private static void Set(Component component, string name, object value)
        {
            FieldInfo field = component.GetType().GetField(name, Members);
            Assert.That(field, Is.Not.Null, "Missing fixture field: " + name);
            field.SetValue(component, field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, value) : value);
        }
        private static T Property<T>(Component component, string name)
        {
            return (T)component.GetType().GetProperty(name, Members).GetValue(component);
        }
        private static void Observe(Component box, Action<int, int> callback)
        {
            box.GetType().GetEvent("ProgressChanged").AddEventHandler(box, callback);
        }
        private static void MarkHeldForAuthorizationFixture(Component card)
        {
            // Most cases isolate authorization/state. The separate XRI selection case
            // below exercises the real selection event rather than this fixture setter.
            Set(card, "<WasHeldByLocalPlayer>k__BackingField", true);
        }

        [SetUp]
        public void Setup()
        {
            GameObject managerObject = new GameObject("CardTestManager");
            created.Add(managerObject);
            manager = managerObject.AddComponent<XRInteractionManager>();
            Shader shader = Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null, "Run the project with its built-in renderer.");
            standby = new Material(shader);
            accepted = new Material(shader);
            created.Add(standby);
            created.Add(accepted);
        }

        [TearDown]
        public void Teardown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index] != null) Object.DestroyImmediate(created[index]);
            created.Clear();
        }

        private GameObject Root(string name)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.position = origin;
            created.Add(root);
            return root;
        }

        private static Renderer Visual(GameObject root, string name, Vector3 size)
        {
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = name;
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localScale = size;
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            return mesh.GetComponent<Renderer>();
        }

        private Component Box(int required = 2, bool requireReader = false)
        {
            GameObject root = Root("FixtureLock");
            Component box = root.AddComponent(Production("KeyBox"));
            Set(box, "keysNeeded", required);
            Set(box, "requireMatchingReader", requireReader);
            root.SetActive(true);
            return box;
        }

        private Component Card(int credential = 1, bool held = true, float scale = 1f, float yaw = 0f)
        {
            GameObject root = Root("FixtureCard");
            root.transform.localScale = Vector3.one * scale;
            root.transform.rotation = Quaternion.Euler(yaw * 0.6f, yaw, yaw * 0.25f);
            Visual(root, "BadgeMesh", new Vector3(0.10f, 0.005f, 0.06f));
            Component card = root.AddComponent(Production("KeyCard"));
            Set(card, "credential", credential);
            root.GetComponent<XRGrabInteractable>().interactionManager = manager;
            root.SetActive(true);
            root.GetComponent<Rigidbody>().useGravity = false;
            if (held) MarkHeldForAuthorizationFixture(card);
            return card;
        }

        private Component Reader(Component box, int credential = 1, bool nested = false)
        {
            GameObject root = Root("FixtureReader");
            Renderer led = Visual(root, "Status_LED", Vector3.one * 0.01f);
            if (nested) box.transform.SetParent(root.transform, false);
            GameObject scan = new GameObject("ScanTarget_FRONT");
            scan.transform.SetParent(root.transform, false);
            var volume = scan.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = Vector3.one * 0.4f;
            Component reader = scan.AddComponent(Production("KeycardReaderLightController"));
            Set(reader, "expectedCredential", credential);
            Set(reader, "readerRoot", root.transform);
            Set(reader, "statusLedRenderer", led);
            Set(reader, "standbyMaterial", standby);
            Set(reader, "acceptedMaterial", accepted);
            Set(reader, "acceptedFlashSeconds", 0f);
            if (!nested) Set(reader, "keyBox", box);
            root.SetActive(true);
            return reader;
        }

        private Component Panel(Component box, int lamps, out Renderer[] renderers)
        {
            GameObject root = Root("FixturePanel");
            renderers = new Renderer[lamps];
            for (int i = 0; i < lamps; i++) renderers[i] = Visual(root, "Lamp" + i, Vector3.one * 0.01f);
            Component panel = root.AddComponent(Production("KeycardLockProgressIndicator"));
            Set(panel, "keyBox", box);
            Set(panel, "progressLights", renderers);
            Set(panel, "standbyMaterial", standby);
            Set(panel, "acceptedMaterial", accepted);
            root.SetActive(true);
            return panel;
        }

        [TestCase(1,1)] [TestCase(1,2)] [TestCase(1,3)] [TestCase(1,4)]
        [TestCase(2,1)] [TestCase(2,2)] [TestCase(2,3)] [TestCase(2,4)]
        [TestCase(3,1)] [TestCase(3,2)] [TestCase(3,3)] [TestCase(3,4)]
        [TestCase(4,1)] [TestCase(4,2)] [TestCase(4,3)] [TestCase(4,4)]
        public void AllCredentialPairsRequireTheMatchingReader(int cardCredential, int readerCredential)
        {
            Component box = Box();
            Component reader = Reader(box, readerCredential);
            Component card = Card(cardCredential);
            bool result = (bool)Call(reader, "TrySubmitCard", card);
            Assert.That(result, Is.EqualTo(cardCredential == readerCredential));
            Assert.That(Property<int>(box, "CurrentKeys"), Is.EqualTo(result ? 1 : 0));
            Assert.That(Property<bool>(card, "IsInserted"), Is.EqualTo(result));
        }

        [Test]
        public void ReaderBoundNonLevelOneLockRejectsBothDirectEntrances()
        {
            Component box = Box();
            Reader(box);
            Component card = Card();
            Assert.That(Property<bool>(box, "RequiresMatchingReader"), Is.True);
            Assert.That((bool)Call(box, "TryAddKey", card), Is.False);
            Assert.That((bool)Call(card, "TryInsertInto", box), Is.False);
            Call(box, "AddKey");
            Assert.That(Property<int>(box, "CurrentKeys"), Is.Zero);
        }

        [Test]
        public void RejectedUnheldCardCanRetryAfterLocalPickup()
        {
            Component box = Box();
            Component reader = Reader(box);
            Component card = Card(1, false);
            Assert.That((bool)Call(reader, "TrySubmitCard", card), Is.False);
            MarkHeldForAuthorizationFixture(card);
            Assert.That((bool)Call(reader, "TrySubmitCard", card), Is.True);
        }

        [Test]
        public void ConsumptionIsCommittedBeforeObserversAndCannotCreditAnotherLock()
        {
            Component first = Box();
            Component second = Box();
            Component card = Card();
            bool alreadyConsumed = false;
            bool secondResult = true;
            Observe(first, (count, required) =>
            {
                alreadyConsumed = Property<bool>(card, "IsInserted");
                secondResult = (bool)Call(second, "TryAddKey", card);
            });
            Assert.That((bool)Call(first, "TryAddKey", card), Is.True);
            Assert.That(alreadyConsumed, Is.True);
            Assert.That(secondResult, Is.False);
            Assert.That(Property<int>(second, "CurrentKeys"), Is.Zero);
            Assert.That((bool)Call(second, "TryAddKey", card), Is.False);
        }

        [Test]
        public void ThrowingObserverDoesNotPreventConsumptionOrOtherObservers()
        {
            Component box = Box();
            Component card = Card();
            int notifications = 0;
            Observe(box, (count, required) => { throw new InvalidOperationException("fixture listener failure"); });
            Observe(box, (count, required) => notifications++);
            LogAssert.Expect(LogType.Exception, new Regex("fixture listener failure"));
            Assert.That((bool)Call(box, "TryAddKey", card), Is.True);
            Assert.That(Property<bool>(card, "IsInserted"), Is.True);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void TwoReadersCannotConsumeTheSameCardTwice()
        {
            Component box = Box();
            Component a = Reader(box);
            Component b = Reader(box);
            Component card = Card();
            Assert.That((bool)Call(a, "TrySubmitCard", card), Is.True);
            Assert.That((bool)Call(b, "TrySubmitCard", card), Is.False);
            Assert.That(Property<int>(box, "CurrentKeys"), Is.EqualTo(1));
        }

        [Test]
        public void ReaderCannotAuthorizeADifferentObjective()
        {
            Component intended = Box();
            Component other = Box();
            Component reader = Reader(intended);
            Component card = Card();
            Assert.That((bool)Call(card, "TryInsertFromReader", other, reader), Is.False);
            Assert.That(Property<int>(other, "CurrentKeys"), Is.Zero);
        }

        [Test]
        public void ExplicitUnbindDoesNotRediscoverANestedObjective()
        {
            Component box = Box();
            Component reader = Reader(box, 1, true);
            Call(reader, "BindObjective", new object[] { null });
            Assert.That((bool)Call(reader, "TrySubmitCard", Card()), Is.False);
            Assert.That(Property<Component>(reader, "Objective"), Is.Null);
            Assert.That(Property<bool>(box, "RequiresMatchingReader"), Is.True);
        }

        [Test]
        public void StaleOrAccessoryOverlapCannotSubmitTheCard()
        {
            Component box = Box();
            Component reader = Reader(box);
            Component card = Card(1, false);
            Call(reader, "OnTriggerEnter", Property<Collider>(card, "PhysicalCollider"));
            card.transform.position += Vector3.right;
            MarkHeldForAuthorizationFixture(card);
            GameObject accessory = new GameObject("MisconfiguredAccessory");
            accessory.transform.SetParent(card.transform, false);
            accessory.transform.position = origin;
            Collider extra = accessory.AddComponent<BoxCollider>();
            Call(reader, "OnTriggerEnter", extra);
            Call(reader, "LateUpdate");
            Assert.That(Property<int>(box, "CurrentKeys"), Is.Zero);
            Assert.That((bool)Call(reader, "TrySubmitCard", card), Is.False);
        }

        [Test]
        public void DisabledReaderCardOrBoxCannotSubmit()
        {
            Component box = Box();
            Component reader = Reader(box);
            Component card = Card();
            ((Behaviour)reader).enabled = false;
            Assert.That((bool)Call(reader, "TrySubmitCard", card), Is.False);
            ((Behaviour)reader).enabled = true;
            ((Behaviour)card).enabled = false;
            Assert.That((bool)Call(reader, "TrySubmitCard", card), Is.False);
            ((Behaviour)card).enabled = true;
            ((Behaviour)box).enabled = false;
            Assert.That((bool)Call(reader, "TrySubmitCard", card), Is.False);
            ((Behaviour)box).enabled = true;
            Assert.That((bool)Call(reader, "TrySubmitCard", card), Is.True);
        }

        [Test]
        public void UnknownCredentialValuesFailClosed()
        {
            Component box = Box();
            Component reader = Reader(box, 999);
            Assert.That((bool)Call(reader, "TrySubmitCard", Card(999)), Is.False);
        }

        [Test]
        public void ReboundPanelIgnoresAnOldInvocationListSnapshot()
        {
            Component a = Box();
            Component b = Box();
            Component panel = null;
            Observe(a, (count, required) => Call(panel, "Bind", b));
            panel = Panel(a, 2, out Renderer[] lamps);
            Assert.That((bool)Call(a, "TryAddKey", Card()), Is.True);
            Assert.That(lamps[0].sharedMaterial, Is.SameAs(standby));
        }

        [Test]
        public void TooFewPanelLampsCannotDisplayFalseCompletion()
        {
            Component box = Box(5);
            Panel(box, 4, out Renderer[] lamps);
            for (int count = 0; count < 4; count++) Call(box, "AddKey");
            foreach (Renderer lamp in lamps) Assert.That(lamp.sharedMaterial, Is.SameAs(standby));
        }

        [TestCase(1f,0f)] [TestCase(1f,45f)] [TestCase(1f,137f)]
        [TestCase(2f,0f)] [TestCase(2f,45f)] [TestCase(2f,137f)]
        public void ColliderThicknessDoesNotInflateWithRootRotation(float scale, float yaw)
        {
            Component card = Card(1, false, scale, yaw);
            var collider = (BoxCollider)Property<Collider>(card, "PhysicalCollider");
            Assert.That(collider.size.y * scale, Is.EqualTo(0.005f * scale + 0.004f).Within(0.0001f));
            Assert.That(card.GetComponent<XRGrabInteractable>().movementType,
                Is.EqualTo(XRBaseInteractable.MovementType.VelocityTracking));
        }

        [Test]
        public void RebindingDuringAcceptanceDoesNotFlashTheNewObjective()
        {
            Component first = Box();
            Component next = Box();
            Component reader = Reader(first);
            Set(reader, "acceptedFlashSeconds", 1f);
            Observe(first, (count, required) => Call(reader, "BindObjective", next));
            Assert.That((bool)Call(reader, "TrySubmitCard", Card()), Is.True);
            Assert.That(Property<Component>(reader, "Objective"), Is.SameAs(next));
            Renderer led = (Renderer)reader.GetType().GetField("statusLedRenderer", Members).GetValue(reader);
            Assert.That(led.sharedMaterial, Is.SameAs(standby));
        }

        [Test]
        public void DisabledRigidbodyCollisionCannotAuthorizeCachedOverlap()
        {
            Component box = Box();
            Component reader = Reader(box);
            Component card = Card();
            card.GetComponent<Rigidbody>().detectCollisions = false;
            Assert.That((bool)Call(reader, "TrySubmitCard", card), Is.False);
            Assert.That(Property<int>(box, "CurrentKeys"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReenabledReaderFindsASleepingCardWithoutReentry()
        {
            Component box = Box();
            Component reader = Reader(box);
            ((Behaviour)reader).enabled = false;
            Component card = Card(1, false);
            Rigidbody body = card.GetComponent<Rigidbody>();
            Physics.SyncTransforms();
            body.Sleep();
            yield return new WaitForFixedUpdate();
            MarkHeldForAuthorizationFixture(card);
            ((Behaviour)reader).enabled = true;
            for (int frame = 0; frame < 5; frame++) yield return new WaitForFixedUpdate();
            Assert.That(Property<int>(box, "CurrentKeys"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RecoveryWakesALooseCardSoGravityCanSettleIt()
        {
            Component card = Card(1, false);
            Rigidbody body = card.GetComponent<Rigidbody>();
            body.useGravity = true;
            Component respawn = card.gameObject.AddComponent(Production("RespawnToOriginalSpawn"));
            Set(respawn, "verboseLogging", false);
            body.Sleep();
            Call(respawn, "RespawnNow");
            float before = body.position.y;
            for (int frame = 0; frame < 5; frame++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.y, Is.LessThan(before));
        }

        private XRDirectInteractor SelectWithLocalHand(Component card)
        {
            GameObject rig = Root("FixtureLocalRig");
            rig.AddComponent(Production("LocalRigMarker"));
            GameObject hand = new GameObject("FixtureHand");
            hand.transform.SetParent(rig.transform, false);
            hand.AddComponent<SphereCollider>().isTrigger = true;
            Rigidbody rb = hand.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            hand.AddComponent<XRController>();
            XRDirectInteractor interactor = hand.AddComponent<XRDirectInteractor>();
            interactor.interactionManager = manager;
            rig.SetActive(true);
            manager.SelectEnter((IXRSelectInteractor)interactor, (IXRSelectInteractable)card.GetComponent<XRGrabInteractable>());
            return interactor;
        }

        [UnityTest]
        public IEnumerator XriSelectionSetsLocalHoldAndRecoverySurvivesDeactivation()
        {
            Component card = Card(1, false);
            SelectWithLocalHand(card);
            var grab = card.GetComponent<XRGrabInteractable>();
            Assert.That(grab.isSelected, Is.True);
            Assert.That(Property<bool>(card, "WasHeldByLocalPlayer"), Is.True);
            Component respawn = card.gameObject.AddComponent(Production("RespawnToOriginalSpawn"));
            Set(respawn, "verboseLogging", false);
            Call(respawn, "RespawnNow");
            Assert.That(grab.enabled, Is.False);
            card.gameObject.SetActive(false);
            card.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(grab.enabled, Is.True, "Recovery lost its temporary grab disable across reactivation.");
        }

        [Test]
        public void RuntimeAddedCardDoesNotInheritTheTemporaryHeldLayerForItsPickupTrigger()
        {
            GameObject root = Root("AlreadyHeldSetup");
            Visual(root, "BadgeMesh", new Vector3(0.1f, 0.005f, 0.06f));
            root.AddComponent<BoxCollider>();
            root.AddComponent<Rigidbody>();
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.interactionManager = manager;
            Component safety = root.AddComponent(Production("HeldItemCollisionMode"));
            root.SetActive(true);
            int originalLayer = root.layer;
            Call(safety, "ApplyHeldLayer");
            Component card = root.AddComponent(Production("KeyCard"));
            Collider affordance = Property<Collider>(card, "GrabAffordanceCollider");
            Assert.That(affordance.gameObject.layer, Is.EqualTo(originalLayer));
        }

        [UnityTest]
        public IEnumerator RuntimeAddedCardRegistersTheNewPickupColliderWithXri()
        {
            GameObject root = Root("AlreadyLiveGrab");
            Visual(root, "BadgeMesh", new Vector3(0.1f, 0.005f, 0.06f));
            Collider oldCollider = root.AddComponent<BoxCollider>();
            root.AddComponent<Rigidbody>().useGravity = false;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.interactionManager = manager;
            root.SetActive(true);
            yield return null;
            Assert.That(manager.IsRegistered((IXRInteractable)grab), Is.True);
            Component card = root.AddComponent(Production("KeyCard"));
            Collider affordance = Property<Collider>(card, "GrabAffordanceCollider");
            Assert.That(manager.TryGetInteractableForCollider(affordance, out IXRInteractable mapped), Is.True);
            Assert.That(mapped, Is.SameAs(grab));
            Assert.That(manager.TryGetInteractableForCollider(oldCollider, out IXRInteractable oldMapping), Is.False);
        }

        [UnityTest]
        public IEnumerator LoosePhysicalCardSettlesOnASolidFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(floor);
            floor.transform.position = origin + Vector3.down * 0.25f;
            floor.transform.localScale = new Vector3(1f, 0.1f, 1f);
            Component card = Card(1, false);
            card.transform.position += Vector3.up * 0.2f;
            card.GetComponent<Rigidbody>().useGravity = true;
            Physics.SyncTransforms();
            for (int frame = 0; frame < 40; frame++) yield return new WaitForFixedUpdate();
            float floorTop = floor.GetComponent<Collider>().bounds.max.y;
            float cardBottom = Property<Collider>(card, "PhysicalCollider").bounds.min.y;
            Assert.That(cardBottom, Is.InRange(floorTop - 0.005f, floorTop + 0.04f));
        }

        [Test]
        public void NewLocksRequireAReaderBeforeAnyReaderAwakes()
        {
            GameObject root = Root("UninitializedReaderLock");
            Component box = root.AddComponent(Production("KeyBox"));
            root.SetActive(true);
            Assert.That(Property<bool>(box, "RequiresMatchingReader"), Is.True);
            Assert.That((bool)Call(box, "TryAddKey", Card()), Is.False);
            Call(box, "AddKey");
            Assert.That(Property<int>(box, "CurrentKeys"), Is.Zero);
        }

        [Test]
        public void ConsumedCardsDisableTheirPhysicalAndPickupCollidersImmediately()
        {
            Component box = Box();
            Component card = Card();
            Assert.That((bool)Call(box, "TryAddKey", card), Is.True);
            Assert.That(Property<Collider>(card, "PhysicalCollider").enabled, Is.False);
            Assert.That(Property<Collider>(card, "GrabAffordanceCollider").enabled, Is.False);
            Assert.That(card.GetComponent<XRGrabInteractable>().enabled, Is.False);
        }

        [Test]
        public void DisableReenableDuringAcceptanceCannotRelightAnOldScan()
        {
            Component box = Box();
            Component reader = Reader(box);
            Set(reader, "acceptedFlashSeconds", 1f);
            Observe(box, (count, required) =>
            {
                ((Behaviour)reader).enabled = false;
                ((Behaviour)reader).enabled = true;
            });
            Assert.That((bool)Call(reader, "TrySubmitCard", Card()), Is.True);
            Renderer led = (Renderer)reader.GetType().GetField("statusLedRenderer", Members).GetValue(reader);
            Assert.That(led.sharedMaterial, Is.SameAs(standby));
        }

        [Test]
        public void DuplicateLampReferencesCannotImplyCompletion()
        {
            Component box = Box();
            Component panel = Panel(box, 2, out Renderer[] lamps);
            Set(panel, "progressLights", new Renderer[] { lamps[0], lamps[0] });
            Call(box, "AddKey");
            Call(box, "AddKey");
            Assert.That(lamps[0].sharedMaterial, Is.SameAs(standby));
            Assert.That(panel.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void DisablingOnlyHeldSafetyDoesNotExposeThePropToThePlayer()
        {
            Component card = Card();
            Component safety = card.GetComponent(Production("HeldItemCollisionMode"));
            Call(safety, "ApplyHeldLayer");
            int held = LayerMask.NameToLayer("HeldItem");
            Assert.That(held, Is.GreaterThanOrEqualTo(0));
            ((Behaviour)safety).enabled = false;
            Assert.That(card.gameObject.layer, Is.EqualTo(held));
            Assert.That(Property<Collider>(card, "GrabAffordanceCollider").gameObject.layer, Is.Not.EqualTo(held));
            ((Behaviour)safety).enabled = true;
            Assert.That(card.gameObject.layer, Is.EqualTo(held));
        }

        [Test]
        public void TenImmediateXriReleaseRegrabCyclesRetainAcquisitionMapping()
        {
            Component card = Card(1, false);
            XRDirectInteractor hand = SelectWithLocalHand(card);
            var grab = card.GetComponent<XRGrabInteractable>();
            Collider affordance = Property<Collider>(card, "GrabAffordanceCollider");
            for (int iteration = 0; iteration < 10; iteration++)
            {
                manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                Assert.That(grab.isSelected, Is.False);
                Assert.That(manager.TryGetInteractableForCollider(affordance, out IXRInteractable mapped), Is.True);
                Assert.That(mapped, Is.SameAs(grab));
                manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                Assert.That(grab.isSelected, Is.True);
            }
            Assert.That(Property<bool>(card, "WasHeldByLocalPlayer"), Is.True);
        }

        [UnityTest]
        public IEnumerator CardCannotCreditALockInAnotherAdditiveScene()
        {
            Component card = Card();
            Component box = Box();
            var scene = UnityEngine.SceneManagement.SceneManager.CreateScene("CardTest_" + Guid.NewGuid());
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(box.gameObject, scene);
            Assert.That((bool)Call(box, "TryAddKey", card), Is.False);
            Assert.That(Property<bool>(card, "IsInserted"), Is.False);
            Assert.That(Property<int>(box, "CurrentKeys"), Is.Zero);
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        }
    }
}
