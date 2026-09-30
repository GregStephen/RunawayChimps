using System;
using GorillaLocomotion;
using Photon.Pun;
using RunawayChimps.Travel;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace RunawayChimps.Toys
{
    /// <summary>Cosmetic LOCAL PER VIEWER. Never sends RPCs, claims ownership or changes gameplay.</summary>
    [DefaultExecutionOrder(700)]
    [DisallowMultipleComponent]
    public sealed class ReactiveSpecimenJar : MonoBehaviour
    {
        [Header("Authored references (specimen points along local +Z)")]
        public Transform specimen;
        public BoxCollider glass;
        [Tooltip("Allowed specimen CENTER, relative to this root. Reserve its entire mesh radius.")]
        public Vector3 motionExtents = new Vector3(0.095f, 0.14f, 0.095f);

        [Header("Curiosity")]
        [Min(0.1f)] public float interestRadius = 0.55f;
        [Min(0.1f)] public float responseSpeed = 6f;
        [Min(0.1f)] public float turnSpeed = 8f;
        [Range(0f, 0.02f)] public float idleDrift = 0.008f;
        [Min(0.5f)] public float viewerDistance = 3f;

        [Header("Tap (metres and seconds; sampled local hands only)")]
        [Min(0.01f)] public float handRadius = 0.05f;
        [Min(0.01f)] public float minimumTapSpeed = 0.3f;
        [Min(0f)] public float contactSkin = 0.008f;
        [Min(0.01f)] public float releaseMargin = 0.025f;
        [Min(0.02f)] public float rearmSeconds = 0.12f;
        [Min(0.05f)] public float reactionCooldown = 0.55f;
        [Min(0.05f)] public float recoilSeconds = 0.4f;
        [Min(0.05f)] public float maximumHandStep = 0.28f;
        [Min(1f)] public float maximumHandSpeed = 10f;

        [Header("Look-away surprise (head orientation, no eye tracking)")]
        [Range(0f, 1f)] public float watchChance = 0.35f;
        [Min(1f)] public float watchCooldown = 18f;
        [Min(0.2f)] public float awayDelay = 0.8f;
        [Min(0.5f)] public float watchSeconds = 4f;
        [Range(30f, 85f)] public float visibleHalfAngle = 75f;
        [Range(95f, 150f)] public float awayHalfAngle = 105f;

        [Header("Desktop preview (Editor Play Mode ONLY; ignored in builds)")]
        public bool editorPreview;
        public Transform previewHead;
        public Transform previewLeftHand;
        public Transform previewRightHand;

        public SpecimenMood CurrentMood => state.Mood;
        public int SelectedHand => state.Hand;
        public int ReactionCount => state.ReactionCount;
        public int WatchCount => state.WatchCount;

        private readonly SpecimenJarState state = new SpecimenJarState();
        private readonly HandHistory left = new HandHistory();
        private readonly HandHistory right = new HandHistory();
        private System.Random random;
        private Player rig;
        private XROrigin origin;
        private object room;
        private int actor;
        private Vector3 restPosition, restScale, recoilGoal, previousHead;
        private Quaternion restRotation, previousHeadRotation;
        private bool capturedRest, haveHead, paused, unfocused, wasPreview;
        private float quietUntil;

        private sealed class HandHistory
        {
            public readonly SpecimenTapGate gate = new SpecimenTapGate();
            public Vector3 previous;
            public Transform source;
            public bool sampled;
            public void Reset() { gate.Reset(); sampled = false; source = null; }
        }

        private void Awake()
        {
            random = new System.Random(GetInstanceID());
            CaptureRest();
        }
        private void OnEnable() { CaptureRest(); ResetState(); }
        private void OnDisable() { ResetState(); rig = null; origin = null; room = null; }
        private void OnApplicationPause(bool value) { paused = value; ResetState(); }
        private void OnApplicationFocus(bool value) { unfocused = !value; ResetState(); }

        private void CaptureRest()
        {
            if (capturedRest || specimen == null) return;
            restPosition = specimen.localPosition;
            restRotation = specimen.localRotation;
            restScale = specimen.localScale;
            capturedRest = true;
        }

        public void ResetState()
        {
            state.Reset(Time.unscaledTime, watchCooldown);
            left.Reset(); right.Reset(); haveHead = false;
            quietUntil = Time.unscaledTime + 0.2f;
            if (!capturedRest || specimen == null) return;
            specimen.localPosition = restPosition;
            specimen.localRotation = restRotation;
            specimen.localScale = restScale;
        }

        private void LateUpdate()
        {
            bool preview = false;
#if UNITY_EDITOR
            preview = editorPreview;
#endif
            if (preview != wasPreview) { ResetState(); wasPreview = preview; }
            if (specimen == null || specimen.parent != transform || glass == null ||
                !glass.enabled || !glass.gameObject.activeInHierarchy || paused || (!preview && unfocused) ||
                LoadingFlow.IsColdStartupPresentationActive ||
                (SectorTravelService.I != null && SectorTravelService.I.IsBusy))
            { ResetState(); return; }

            Transform head, lh, rh;
            bool leftTracked, rightTracked;
            if (!ReadViewer(preview, out head, out lh, out rh, out leftTracked, out rightTracked))
            { ResetState(); return; }
            float dt = Time.unscaledDeltaTime;
            Vector3 hp = head.position;
            if (!Finite(hp) || !Finite(head.forward) || dt <= 0f || dt > 0.1f ||
                (haveHead && ((hp - previousHead).magnitude > 0.3f ||
                 (hp - previousHead).magnitude / dt > 10f ||
                 Quaternion.Angle(previousHeadRotation, head.rotation) > 60f)))
            { ResetState(); return; }
            previousHead = hp; previousHeadRotation = head.rotation; haveHead = true;
            float now = Time.unscaledTime;
            if (now < quietUntil) { left.Reset(); right.Reset(); return; }
            if ((hp - transform.position).sqrMagnitude > viewerDistance * viewerDistance)
            { ResetState(); return; }

            bool ltap, rtap;
            float ld = SampleHand(lh, leftTracked, left, dt, out ltap);
            float rd = SampleHand(rh, rightTracked, right, dt, out rtap);
            int nearest = SpecimenJarState.Nearest(ld, rd, interestRadius);
            if (ltap || rtap)
            {
                Transform tapping = ltap ? lh : rh;
                if (state.TryTap(now, reactionCooldown, recoilSeconds))
                    recoilGoal = Bound(-transform.InverseTransformPoint(tapping.position) * 0.6f);
            }
            Vector3 toJar = transform.position - hp;
            float dot = toJar.sqrMagnitude > 0.000001f ? Vector3.Dot(head.forward, toJar.normalized) : 1f;
            state.Step(now, nearest, dot >= Mathf.Cos(visibleHalfAngle * Mathf.Deg2Rad),
                dot <= Mathf.Cos(awayHalfAngle * Mathf.Deg2Rad), awayDelay, watchCooldown,
                watchSeconds, watchChance, (float)random.NextDouble());

            Vector3 goal = restPosition;
            Vector3 facing = transform.forward;
            if (state.Mood == SpecimenMood.Interested)
            {
                Transform hand = nearest == 0 ? lh : rh;
                Vector3 localHand = transform.InverseTransformPoint(hand.position);
                float distance = nearest == 0 ? ld : rd;
                // Far curiosity stays central; near-glass curiosity reaches the safe boundary.
                goal = Bound(localHand) * Mathf.Lerp(1f, 0.22f, Mathf.Clamp01(distance / interestRadius));
                facing = hand.position - specimen.position;
            }
            else if (state.Mood == SpecimenMood.Recoiling)
            {
                goal = recoilGoal;
                facing = hp - specimen.position;
            }
            else if (state.Mood == SpecimenMood.Watching)
            {
                goal = Bound(transform.InverseTransformPoint(hp));
                facing = hp - specimen.position;
            }
            else
            {
                goal += new Vector3(Mathf.Sin(now * 0.7f), Mathf.Sin(now * 1.1f), Mathf.Cos(now * 0.6f)) * idleDrift;
            }
            // No snap/teleport path, including surprise entry/return. Convex bounds also
            // contain every interpolated point when switching hands on opposite sides.
            float blend = 1f - Mathf.Exp(-responseSpeed * dt);
            specimen.localPosition = Bound(Vector3.Lerp(specimen.localPosition, Bound(goal), blend));
            if (facing.sqrMagnitude > 0.000001f)
                specimen.rotation = Quaternion.Slerp(specimen.rotation,
                    Quaternion.LookRotation(facing, transform.up), 1f - Mathf.Exp(-turnSpeed * dt));
            float breath = state.Mood == SpecimenMood.Recoiling ? 0.86f : 1f + Mathf.Sin(now * 1.8f) * 0.015f;
            specimen.localScale = Vector3.Lerp(specimen.localScale, restScale * breath, blend);
        }

        private bool ReadViewer(bool preview, out Transform head, out Transform lh, out Transform rh,
            out bool leftTracked, out bool rightTracked)
        {
            head = lh = rh = null; leftTracked = rightTracked = false;
            object currentRoom = PhotonNetwork.CurrentRoom;
            int currentActor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
            if (!ReferenceEquals(room, currentRoom) || actor != currentActor)
            { ResetState(); room = currentRoom; actor = currentActor; }
#if UNITY_EDITOR
            if (preview)
            {
                head = previewHead; lh = previewLeftHand; rh = previewRightHand;
                leftTracked = lh != null && lh.gameObject.activeInHierarchy;
                rightTracked = rh != null && rh.gameObject.activeInHierarchy;
                return head != null && head.gameObject.activeInHierarchy && (leftTracked || rightTracked);
            }
#endif
            if (!PhotonNetwork.InRoom || (AppState.I != null && !AppState.I.IsReady) ||
                gameObject.scene != SceneManager.GetActiveScene()) return false;
            Player currentRig = Player.Instance;
            if (currentRig != rig)
            { ResetState(); rig = currentRig; origin = rig != null ? rig.GetComponentInParent<XROrigin>() : null; }
            if (rig == null || !rig.isActiveAndEnabled || rig.disableMovement || origin == null ||
                origin.Camera == null || !origin.Camera.isActiveAndEnabled ||
                rig.GetComponentInParent<LocalRigMarker>() == null || !Tracked(XRNode.Head)) return false;
            head = origin.Camera.transform;
            lh = rig.leftHandTransform; rh = rig.rightHandTransform;
            leftTracked = EligibleLocalHand(lh) && Tracked(XRNode.LeftHand);
            rightTracked = EligibleLocalHand(rh) && Tracked(XRNode.RightHand);
            // Loss of both controllers cannot leave an old interest/watch/contact alive.
            return leftTracked || rightTracked;
        }

        private bool EligibleLocalHand(Transform hand)
        {
            if (hand == null || !hand.gameObject.activeInHierarchy || !hand.IsChildOf(origin.transform)) return false;
            PhotonView view = hand.GetComponentInParent<PhotonView>();
            return view == null || view.IsMine;
        }
        private static bool Tracked(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked &&
                device.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState flags) &&
                (flags & (InputTrackingState.Position | InputTrackingState.Rotation)) ==
                (InputTrackingState.Position | InputTrackingState.Rotation);
        }

        private float SampleHand(Transform hand, bool tracked, HandHistory history, float dt, out bool tap)
        {
            tap = false;
            if (!tracked || hand == null || !Finite(hand.position))
            { history.Reset(); return float.PositiveInfinity; }
            Vector3 pos = hand.position;
            bool continuous = history.sampled && history.source == hand;
            Vector3 delta = pos - history.previous;
            if (continuous && (delta.magnitude > maximumHandStep || delta.magnitude / dt > maximumHandSpeed))
            { history.Reset(); return float.PositiveInfinity; }
            Vector3 closest = glass.ClosestPoint(pos);
            float gap = Vector3.Distance(pos, closest) - handRadius;
            bool contact = gap <= contactSkin;
            Vector3 normal = pos - closest;
            if (normal.sqrMagnitude < 0.000001f && continuous)
                normal = history.previous - glass.ClosestPoint(history.previous);
            // A legitimate fast pass through the thin pane still counts; only the single
            // authored glass collider is queried, never overlapping/duplicate hand colliders.
            if (continuous && delta.sqrMagnitude > 0.000001f &&
                glass.Raycast(new Ray(history.previous, delta.normalized), out RaycastHit hit, delta.magnitude))
            { contact = true; normal = hit.normal; }
            float inward = continuous ? Vector3.Dot(-delta / dt, normal.normalized) : 0f;
            tap = history.gate.Sample(continuous, contact, gap > contactSkin + releaseMargin,
                dt, inward, minimumTapSpeed, rearmSeconds);
            history.previous = pos; history.source = hand; history.sampled = true;
            return continuous ? Mathf.Max(0f, gap) : float.PositiveInfinity;
        }

        private Vector3 Bound(Vector3 point)
        {
            if (!Finite(point)) return Vector3.zero;
            return point * SpecimenJarState.BoundsScale(point.x, point.y, point.z,
                motionExtents.x, motionExtents.y, motionExtents.z);
        }
        private static bool Finite(Vector3 p) => SpecimenJarState.Finite(p.x) &&
            SpecimenJarState.Finite(p.y) && SpecimenJarState.Finite(p.z);

#if UNITY_EDITOR
        // Visual-only diagnostic. Real tap-gate acceptance uses moved handles / a headset.
        public void PreviewRecoil()
        {
            if (!Application.isPlaying || !editorPreview) return;
            if (state.TryTap(Time.unscaledTime, reactionCooldown, recoilSeconds))
                recoilGoal = Bound(Vector3.back);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.9f, 0.8f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, motionExtents * 2f);
            Gizmos.DrawWireSphere(Vector3.zero, interestRadius);
        }
        private void OnValidate()
        {
            motionExtents = new Vector3(Mathf.Clamp(motionExtents.x, 0.005f, 0.095f),
                Mathf.Clamp(motionExtents.y, 0.005f, 0.14f), Mathf.Clamp(motionExtents.z, 0.005f, 0.095f));
            interestRadius = Mathf.Max(0.1f, interestRadius);
            responseSpeed = Mathf.Max(0.1f, responseSpeed); turnSpeed = Mathf.Max(0.1f, turnSpeed);
            watchCooldown = Mathf.Max(1f, watchCooldown); awayDelay = Mathf.Max(0.2f, awayDelay);
            rearmSeconds = Mathf.Max(0.02f, rearmSeconds); minimumTapSpeed = Mathf.Max(0.01f, minimumTapSpeed);
        }
#endif
    }
}
