using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RunawayChimps.Tests
{
    // Reflection bridges this dedicated fixture to Assembly-CSharp without
    // reorganizing the game's runtime assemblies. PhysX queries and AudioSource
    // configuration below are the real Unity implementations, not test doubles.
    public sealed class HandImpactAudioPlayModeTests
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Instance | BindingFlags.Static;
        private readonly List<Object> created = new List<Object>();
        private readonly List<object> samples = new List<object>();
        private readonly Vector3 origin = new Vector3(110f, 30f, 110f);
        private Random.State randomState;

        private static Type Production(string name) => Type.GetType(name + ", Assembly-CSharp", true);

        private static MethodInfo Method(Type type, string name, int arguments)
        {
            foreach (MethodInfo method in type.GetMethods(Members))
                if (method.Name == name && method.GetParameters().Length == arguments) return method;
            Assert.Fail("Missing production method " + type.Name + "." + name);
            return null;
        }

        private static object Call(object target, string name, params object[] arguments)
        {
            return Method(target.GetType(), name, arguments.Length).Invoke(target, arguments);
        }

        private static T Field<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, Members);
            Assert.That(field, Is.Not.Null, "Missing production field " + name);
            return (T)field.GetValue(target);
        }

        private static void Set(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Members);
            Assert.That(field, Is.Not.Null, "Missing production field " + name);
            field.SetValue(target, value);
        }

        private static T Property<T>(object target, string name)
        {
            return (T)target.GetType().GetProperty(name, Members).GetValue(target);
        }

        [SetUp]
        public void Setup()
        {
            randomState = Random.state;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index] != null) Object.DestroyImmediate(created[index]);
            created.Clear();
            samples.Clear();
            Random.state = randomState;
            // SurfaceImpactEmitter owns a detached pool and schedules its cleanup
            // through Unity's normal runtime Destroy lifecycle.
            yield return null;
        }

        private GameObject Root(string name, bool active = true)
        {
            var root = new GameObject(name);
            root.SetActive(active);
            created.Add(root);
            return root;
        }

        private AudioClip Clip(string name)
        {
            AudioClip clip = AudioClip.Create(name, 44100, 1, 44100, false);
            created.Add(clip);
            return clip;
        }

        private ScriptableObject Profile(params AudioClip[] clips)
        {
            ScriptableObject profile = ScriptableObject.CreateInstance(Production("SurfaceAudioProfile"));
            created.Add(profile);
            Set(profile, "clips", clips);
            Set(profile, "pitchMin", 1f);
            Set(profile, "pitchMax", 1f);
            return profile;
        }

        private Component Emitter(string name = "HandImpactEmitterFixture")
        {
            Component emitter = Root(name).AddComponent(Production("SurfaceImpactEmitter"));
            // Retain explicit ownership so cleanup still runs if an assertion fails.
            GameObject pool = Field<GameObject>(emitter, "voiceRoot");
            Assert.That(pool, Is.Not.Null);
            created.Add(pool);
            return emitter;
        }

        private static AudioSource[] Voices(Component emitter) => Field<AudioSource[]>(emitter, "voices");

        private static bool Play(Component emitter, ScriptableObject profile, Vector3 position, float speed, float now)
        {
            return (bool)Call(emitter, "TryPlay", profile, position, speed, now);
        }

        [Test]
        public void SharedProfileSkipsNullsDuplicatesAndThePreviousRecording()
        {
            AudioClip first = Clip("First strike");
            AudioClip second = Clip("Second strike");
            ScriptableObject profile = Profile(null, first, first, null, second, second);
            Assert.That(Property<bool>(profile, "HasPlayableClips"), Is.True);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                object[] arguments = { first, null };
                Assert.That((bool)Call(profile, "TryChooseClip", arguments), Is.True);
                Assert.That(arguments[1], Is.SameAs(second), "A repeated slot must not repeat the previous recording.");
            }
            Set(profile, "clips", new[] { first, null, first });
            object[] single = { first, null };
            Assert.That((bool)Call(profile, "TryChooseClip", single), Is.True);
            Assert.That(single[1], Is.SameAs(first), "A one-clip profile remains usable.");
            Set(profile, "clips", new AudioClip[] { null, null });
            Assert.That(Property<bool>(profile, "HasPlayableClips"), Is.False);
            object[] empty = { first, null };
            Assert.That((bool)Call(profile, "TryChooseClip", empty), Is.False);
            Assert.That(empty[1], Is.Null);
        }

        [Test]
        public void NearestUsableSurfaceOverrideInheritsThroughEmptyAndDisabledComponents()
        {
            ScriptableObject fallback = Profile(Clip("Fallback"));
            ScriptableObject parentProfile = Profile(Clip("Parent"));
            ScriptableObject childProfile = Profile(Clip("Child"));
            ScriptableObject emptyProfile = Profile(null, null);
            GameObject parent = Root("SurfaceParent");
            Component parentAudio = parent.AddComponent(Production("SurfaceAudio"));
            Set(parentAudio, "profile", parentProfile);
            GameObject child = Root("SurfaceChild");
            child.transform.SetParent(parent.transform, false);
            Collider collider = child.AddComponent<BoxCollider>();
            Component childAudio = child.AddComponent(Production("SurfaceAudio"));
            Set(childAudio, "profile", emptyProfile);
            MethodInfo resolve = Method(Production("SurfaceAudio"), "ResolveProfile", 2);
            Assert.That(resolve.Invoke(null, new object[] { collider, fallback }), Is.SameAs(parentProfile));
            Set(childAudio, "profile", childProfile);
            Assert.That(resolve.Invoke(null, new object[] { collider, fallback }), Is.SameAs(childProfile));
            ((Behaviour)childAudio).enabled = false;
            Assert.That(resolve.Invoke(null, new object[] { collider, fallback }), Is.SameAs(parentProfile));
            ((Behaviour)parentAudio).enabled = false;
            Assert.That(resolve.Invoke(null, new object[] { collider, fallback }), Is.SameAs(fallback));
            Assert.That(resolve.Invoke(null, new object[] { collider, emptyProfile }), Is.Null);
            Assert.That(resolve.Invoke(null, new object[] { null, fallback }), Is.SameAs(fallback));
        }

        [Test]
        public void SurfaceResponseHasABoundedMonotonicVolumeAndSafePitchRange()
        {
            ScriptableObject profile = Profile(Clip("Response"));
            Set(profile, "minImpact", 0.5f);
            Set(profile, "fullImpactSpeed", 2.5f);
            Set(profile, "volume", 0.8f);
            Assert.That((float)Call(profile, "EvaluateVolume", 0.49f), Is.Zero);
            float prior = -1f;
            for (int step = 0; step <= 100; step++)
            {
                float volume = (float)Call(profile, "EvaluateVolume", 0.5f + step * 0.05f);
                Assert.That(volume, Is.InRange(0f, 0.8f));
                Assert.That(volume, Is.GreaterThanOrEqualTo(prior));
                prior = volume;
            }
            Assert.That(prior, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That((float)Call(profile, "EvaluateVolume", float.NaN), Is.Zero);
            Assert.That((float)Call(profile, "EvaluateVolume", float.PositiveInfinity), Is.Zero);
            Set(profile, "pitchMin", 1.3f);
            Set(profile, "pitchMax", 0.8f);
            for (int attempt = 0; attempt < 32; attempt++)
                Assert.That((float)Call(profile, "ChoosePitch"), Is.InRange(0.8f, 1.3f));
            Set(profile, "minImpact", float.NaN);
            Set(profile, "minInterval", float.PositiveInfinity);
            Assert.That(Property<float>(profile, "EffectiveMinImpact"), Is.EqualTo(0.25f));
            Assert.That(Property<float>(profile, "EffectiveMinInterval"), Is.EqualTo(0.06f));
        }

        [Test]
        public void SeparateImpactVoicesKeepTheirOriginalPositionAndPitchAsTheHandMoves()
        {
            Component emitter = Emitter();
            ScriptableObject first = Profile(Clip("First sound"));
            ScriptableObject second = Profile(Clip("Second sound"));
            Set(second, "pitchMin", 1.5f);
            Set(second, "pitchMax", 1.5f);
            Vector3 firstPoint = origin;
            Vector3 secondPoint = origin + Vector3.right;
            Assert.That(Play(emitter, first, firstPoint, 2f, 1f), Is.True);
            emitter.transform.position += Vector3.one * 8f;
            Assert.That(Play(emitter, second, secondPoint, 2f, 1.1f), Is.True);
            AudioSource[] voices = Voices(emitter);
            Assert.That(voices.Length, Is.EqualTo(4));
            Assert.That(voices[0].transform.position, Is.EqualTo(firstPoint));
            Assert.That(voices[0].pitch, Is.EqualTo(1f));
            Assert.That(voices[1].transform.position, Is.EqualTo(secondPoint));
            Assert.That(voices[1].pitch, Is.EqualTo(1.5f));
            foreach (AudioSource voice in voices)
            {
                Assert.That(voice.dopplerLevel, Is.Zero);
                Assert.That(voice.spatialBlend, Is.EqualTo(1f));
                Assert.That(voice.loop, Is.False);
                Assert.That(voice.playOnAwake, Is.False);
            }
        }

        [Test]
        public void EmitterIntervalsAreIndependentAndDoNotCarryAcrossStopOrDisable()
        {
            Component left = Emitter("LeftEmitterFixture");
            Component right = Emitter("RightEmitterFixture");
            ScriptableObject profile = Profile(Clip("Tap"));
            Set(profile, "minInterval", 0.1f);
            Assert.That(Play(left, profile, origin, 2f, 1f), Is.True);
            Assert.That(Play(right, profile, origin, 2f, 1f), Is.True);
            Assert.That(Play(left, profile, origin, 2f, 1.05f), Is.False);
            Assert.That(Play(left, profile, origin, 2f, 1.2f), Is.True);
            Call(left, "StopAll");
            foreach (AudioSource source in Voices(left)) Assert.That(source.clip, Is.Null);
            Assert.That(Play(left, profile, origin, 2f, 1.21f), Is.True);
            ((Behaviour)left).enabled = false;
            foreach (AudioSource source in Voices(left)) Assert.That(source.clip, Is.Null);
            Assert.That(Play(left, profile, origin, 2f, 2f), Is.False);
            Assert.That(Play(right, profile, origin, 0.1f, 2f), Is.False);
            Assert.That(Play(right, profile, new Vector3(float.NaN, 0f, 0f), 2f, 2f), Is.False);
        }

        [Test]
        public void SaturatingTheVoicePoolReusesOnlyTheOldestVoiceAndDoesNotAllocateMoreSources()
        {
            Component emitter = Emitter();
            ScriptableObject profile = Profile(Clip("Pooled impact"));
            for (int strike = 0; strike < 6; strike++)
                Assert.That(Play(emitter, profile, origin + Vector3.right * strike, 2f, 1f + strike * 0.1f), Is.True);
            AudioSource[] voices = Voices(emitter);
            Assert.That(Field<GameObject>(emitter, "voiceRoot").GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(4));
            Assert.That(voices[0].transform.position, Is.EqualTo(origin + Vector3.right * 4f));
            Assert.That(voices[1].transform.position, Is.EqualTo(origin + Vector3.right * 5f));
            Assert.That(voices[2].transform.position, Is.EqualTo(origin + Vector3.right * 2f));
            Assert.That(voices[3].transform.position, Is.EqualTo(origin + Vector3.right * 3f));
        }

        [UnityTest]
        public IEnumerator DestroyingTheEmitterAlsoDestroysItsDetachedVoicePool()
        {
            Component emitter = Emitter();
            GameObject pool = Field<GameObject>(emitter, "voiceRoot");
            Object.Destroy(emitter.gameObject);
            yield return null;
            yield return null;
            Assert.That(pool == null, Is.True, "Detached impact voices survived their owner.");
        }

        private Transform RigChild(GameObject root, string name, Vector3 position)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = position;
            return child.transform;
        }

        private Component Rig(out Transform left, out Transform right)
        {
            // Keep the rig inactive and advance Update explicitly. This avoids XR,
            // Photon, singleton replacement and unrelated scene Update scheduling.
            GameObject root = Root("HandImpactSolverFixture", false);
            root.transform.position = origin;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            Transform head = RigChild(root, "Head", new Vector3(0f, 1.5f, 0f));
            Transform torso = RigChild(root, "Body", new Vector3(0f, 0.9f, 0f));
            SphereCollider headCollider = head.gameObject.AddComponent<SphereCollider>();
            headCollider.radius = 0.1f;
            CapsuleCollider bodyCollider = torso.gameObject.AddComponent<CapsuleCollider>();
            bodyCollider.radius = 0.2f;
            bodyCollider.height = 1.2f;
            left = RigChild(root, "LeftTracked", new Vector3(-0.2f, 0.3f, 0f));
            right = RigChild(root, "RightTracked", new Vector3(0.2f, 0.5f, 0f));
            Transform leftFollower = RigChild(root, "LeftFollower", left.localPosition);
            Transform rightFollower = RigChild(root, "RightFollower", right.localPosition);
            Component player = root.AddComponent(Production("GorillaLocomotion.Player"));
            Set(player, "headCollider", headCollider);
            Set(player, "bodyCollider", bodyCollider);
            Set(player, "leftHandTransform", left);
            Set(player, "rightHandTransform", right);
            Set(player, "leftHandFollower", leftFollower);
            Set(player, "rightHandFollower", rightFollower);
            Set(player, "velocityHistorySize", 5);
            Set(player, "maxArmLength", 5f);
            Set(player, "velocityLimit", 1000f);
            Set(player, "locomotionEnabledLayers", (LayerMask)1);
            Call(player, "InitializeValues");
            EventInfo updated = player.GetType().GetEvent("HandContactUpdated", Members);
            Type sampleType = player.GetType().GetNestedType("HandContactSample", Members);
            MethodInfo capture = GetType().GetMethod("Capture", Members).MakeGenericMethod(sampleType);
            updated.AddEventHandler(player, Delegate.CreateDelegate(updated.EventHandlerType, this, capture));
            return player;
        }

        private void Capture<T>(T sample)
        {
            samples.Add(sample);
        }

        private BoxCollider Plane(Vector3 normal, Vector3 offset = default(Vector3), bool trigger = false)
        {
            GameObject root = Root(trigger ? "IgnoredContactTrigger" : "ImpactSurface");
            root.transform.position = origin + offset - normal * 0.05f;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(5f, 0.1f, 5f);
            collider.isTrigger = trigger;
            return collider;
        }

        private static bool Solve(Component player, Vector3 start, Vector3 movement, out Vector3 end, out RaycastHit hit)
        {
            object[] arguments = { start, 0.05f, movement, 0.995f, default(Vector3), true, default(RaycastHit) };
            Physics.SyncTransforms();
            bool contact = (bool)Call(player, "IterativeCollisionSphereCast", arguments);
            end = (Vector3)arguments[4];
            hit = (RaycastHit)arguments[6];
            return contact;
        }

        private static void Publish(Component player, bool touching, RaycastHit hit, bool isLeft = true, bool suppressed = false)
        {
            object sample = Activator.CreateInstance(player.GetType().GetNestedType("HandContactSample", Members));
            Set(sample, "IsLeft", isLeft);
            Set(sample, "IsTouching", touching);
            Set(sample, "Hit", hit);
            Set(sample, "Velocity", touching ? Vector3.down * 2f : Vector3.zero);
            Set(sample, "Suppressed", suppressed);
            Field<Delegate>(player, "HandContactUpdated").DynamicInvoke(sample);
        }

        private static int UsedVoices(Component emitter)
        {
            int count = 0;
            foreach (AudioSource voice in Voices(emitter))
                if (voice.clip != null) count++;
            return count;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ContactEventUsesItsSurfaceProfileAndRequiresFreshReleaseAfterPauseOrFocusLoss(bool pause)
        {
            Component player = Rig(out _, out _);
            AudioClip floorClip = Clip("Authored floor impact");
            ScriptableObject authored = Profile(floorClip);
            ScriptableObject fallback = Profile(Clip("Fallback impact"));
            Set(authored, "minInterval", 0f);
            Set(authored, "pitchMin", 1.3f);
            Set(authored, "pitchMax", 1.3f);
            BoxCollider floor = Plane(Vector3.up);
            Set(floor.gameObject.AddComponent(Production("SurfaceAudio")), "profile", authored);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(origin + Vector3.up, Vector3.down, out RaycastHit hit, 2f, 1,
                QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(hit.collider, Is.SameAs(floor));

            // A live controller object binds to the inactive, manually advanced
            // solver. No frame advances in this test, so XR and Update scheduling
            // cannot supply extra samples. Timing boundaries run in the managed
            // harness; zero release/cooldown isolate the event-to-playback route.
            GameObject hand = Root("HandImpactBoundController");
            Set(player, "leftHandTransform", hand.transform);
            Component audio = hand.AddComponent(Production("HandImpactAudio"));
            Set(audio, "locomotionPlayer", player);
            Set(audio, "defaultProfile", fallback);
            Set(audio, "slapLockout", 0f);
            Set(audio, "rearmOffSurfaceTime", 0f);
            Call(audio, "TryBind");
            Assert.That(Field<Component>(audio, "boundPlayer"), Is.SameAs(player));
            Component emitter = hand.GetComponent(Production("SurfaceImpactEmitter"));
            created.Add(Field<GameObject>(emitter, "voiceRoot"));

            Publish(player, false, default(RaycastHit));
            Publish(player, true, hit);
            Assert.That(UsedVoices(emitter), Is.EqualTo(1));
            Assert.That(Voices(emitter)[0].clip, Is.SameAs(floorClip));
            Assert.That(Voices(emitter)[0].pitch, Is.EqualTo(1.3f));
            Assert.That(Voices(emitter)[0].transform.position, Is.EqualTo(hit.point));
            Publish(player, true, hit);
            Publish(player, false, default(RaycastHit), false);
            Publish(player, true, hit);
            Assert.That(UsedVoices(emitter), Is.EqualTo(1), "A stay or the other hand's release duplicated this impact.");

            string callback = pause ? "OnApplicationPause" : "OnApplicationFocus";
            Call(audio, callback, pause);
            Assert.That(UsedVoices(emitter), Is.Zero, "Entering pause/focus loss did not stop old tails.");
            Publish(player, false, default(RaycastHit));
            Publish(player, true, hit);
            Assert.That(UsedVoices(emitter), Is.Zero, "Background contact samples rearmed audio.");
            Call(audio, callback, !pause);
            Publish(player, true, hit);
            Assert.That(UsedVoices(emitter), Is.Zero, "Regaining focus or resuming played a planted hand.");
            Publish(player, false, default(RaycastHit));
            Publish(player, true, hit);
            Assert.That(UsedVoices(emitter), Is.EqualTo(1), "A fresh released strike failed after resume.");
            Publish(player, true, hit, true, true);
            Assert.That(UsedVoices(emitter), Is.Zero, "A suppressed solver sample did not stop playback.");
        }

        [TestCase(0, false)]
        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        public void SolverPublishesTheActualFloorWallOrCeilingContactAndIgnoresTriggers(int direction, bool withTrigger)
        {
            Component player = Rig(out _, out _);
            Vector3 normal = direction == 0 ? Vector3.up : direction == 1 ? Vector3.left : Vector3.down;
            BoxCollider surface = Plane(normal);
            if (withTrigger) Plane(normal, normal * 0.15f, true);
            Assert.That(Solve(player, origin + normal * 0.3f, -normal * 0.6f, out Vector3 end, out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(surface));
            Assert.That(Vector3.Dot(hit.normal, normal), Is.GreaterThan(0.99f));
            Assert.That(Vector3.Dot(end - origin, normal), Is.InRange(0.04f, 0.06f));
        }

        [Test]
        public void SlidingIntoASecondColliderRetainsTheOriginalIncomingSurfaceForAudio()
        {
            Component player = Rig(out _, out _);
            BoxCollider floor = Plane(Vector3.up);
            Component surface = floor.gameObject.AddComponent(Production("GorillaLocomotion.Surface"));
            Set(surface, "slipPercentage", 1f);
            Plane(Vector3.left, Vector3.right * 0.35f);
            Assert.That(Solve(player, origin + Vector3.up * 0.3f,
                new Vector3(0.5f, -0.6f, 0f), out Vector3 end, out RaycastHit hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(floor), "The slide contact replaced the incoming floor hit.");
            Assert.That(hit.normal.y, Is.GreaterThan(0.99f));
            Assert.That(end.x - origin.x, Is.LessThan(0.32f), "Fixture must also be stopped by the second wall.");
        }

        [UnityTest]
        public IEnumerator OneSamplePerHandUsesTrackedVelocityAndExcludesPreviousBodyCorrection()
        {
            Component player = Rig(out Transform left, out _);
            BoxCollider floor = Plane(Vector3.up);
            yield return null;
            Assert.That(Time.deltaTime, Is.InRange(0.00001f, 0.15f));
            left.position = origin + new Vector3(-0.2f, 0.01f, 0f);
            Physics.SyncTransforms();
            Call(player, "Update");
            Assert.That(samples.Count, Is.EqualTo(2));
            Assert.That(Field<bool>(samples[0], "IsLeft"), Is.True);
            Assert.That(Field<bool>(samples[1], "IsLeft"), Is.False);
            Assert.That(Field<bool>(samples[0], "Suppressed"), Is.True, "First history sample must be silent.");
            Assert.That(Field<bool>(samples[1], "Suppressed"), Is.True);

            left.position = origin + new Vector3(-0.2f, 0.3f, 0f);
            Call(player, "Update");
            Assert.That(Field<bool>(samples[samples.Count - 2], "IsTouching"), Is.False);
            Vector3 before = left.position;
            left.position = origin + new Vector3(-0.2f, 0.01f, 0f);
            Vector3 expectedVelocity = (left.position - before) / Time.deltaTime;
            samples.Clear();
            Call(player, "Update");
            Assert.That(samples.Count, Is.EqualTo(2));
            Assert.That(Field<bool>(samples[0], "Suppressed"), Is.False);
            Assert.That(Field<bool>(samples[0], "IsTouching"), Is.True);
            Assert.That(Field<RaycastHit>(samples[0], "Hit").collider, Is.SameAs(floor));
            Assert.That(Vector3.Distance(Field<Vector3>(samples[0], "Velocity"), expectedVelocity), Is.LessThan(0.001f));
            Assert.That(Field<Vector3>(samples[1], "Velocity").sqrMagnitude, Is.LessThan(0.00001f));
            samples.Clear();
            Call(player, "Update");
            Assert.That(Field<Vector3>(samples[0], "Velocity").sqrMagnitude, Is.LessThan(0.00001f),
                "Solver body translation or artificial sticking gravity became another strike.");
            Assert.That(Field<Vector3>(samples[1], "Velocity").sqrMagnitude, Is.LessThan(0.00001f),
                "The other hand inherited the body correction as an impact.");
        }

        [UnityTest]
        public IEnumerator TeleportAndDisableResetHistoryAndNotifyAudioWithoutAnImpact()
        {
            Component player = Rig(out _, out _);
            int resets = 0;
            player.GetType().GetEvent("HandContactsReset", Members).AddEventHandler(player, (Action)(() => resets++));
            yield return null;
            Set(player, "hasPreviousAudioSample", true);
            player.transform.position += Vector3.one * 5f;
            Call(player, "ResetAfterTeleport");
            Assert.That(resets, Is.EqualTo(1));
            Assert.That(Field<bool>(player, "hasPreviousAudioSample"), Is.False);
            Call(player, "Update");
            Assert.That(samples.Count, Is.EqualTo(2));
            foreach (object sample in samples) Assert.That(Field<bool>(sample, "Suppressed"), Is.True);
            Call(player, "OnDisable");
            Assert.That(resets, Is.EqualTo(2));
            Assert.That(Field<bool>(player, "hasPreviousAudioSample"), Is.False);
        }

        [UnityTest]
        public IEnumerator TurningDoesNotBecomeHandVelocityAndTrackingJumpsAreSuppressed()
        {
            Component player = Rig(out Transform left, out _);
            yield return null;
            Assert.That(Time.deltaTime, Is.InRange(0.00001f, 0.15f));
            Set(player, "hasPreviousAudioSample", true);
            Call(player, "Turn", 90f);
            Vector3 previous = Field<Vector3>(player, "previousLeftAudioPosition");
            Vector3 current = (Vector3)Call(player, "CurrentLeftHandPosition");
            object[] stationary = { current, previous, default(Vector3) };
            Assert.That((bool)Call(player, "TryGetHandAudioVelocity", stationary), Is.True);
            Assert.That(((Vector3)stationary[2]).magnitude, Is.LessThan(0.001f));
            object[] jump = { current + Vector3.right, current, default(Vector3) };
            Assert.That((bool)Call(player, "TryGetHandAudioVelocity", jump), Is.False);
            object[] invalid = { new Vector3(float.NaN, left.position.y, left.position.z), current, default(Vector3) };
            Assert.That((bool)Call(player, "TryGetHandAudioVelocity", invalid), Is.False);
            Set(player, "disableMovement", true);
            Call(player, "Update");
            Assert.That(samples.Count, Is.EqualTo(2));
            foreach (object sample in samples) Assert.That(Field<bool>(sample, "Suppressed"), Is.True);
        }
    }
}
