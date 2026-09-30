using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.Travel;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

namespace RunawayChimps.Hub
{
    [DefaultExecutionOrder(100)]
    public sealed class PrimateStrengthTester : MonoBehaviour, IOnEventCallback
    {
        // Transient, cosmetic Hub event. Never cached and never awards currency.
        public const byte HitEvent = 182;
        [SerializeField] private int stationId = 1;
        [Header("Authored prefab references")]
        [SerializeField] private Transform strikeFace;
        [SerializeField] private Transform padVisual;
        [SerializeField] private Transform meterFill;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text assessmentText;
        [SerializeField] private TMP_Text bestText;
        [SerializeField] private TMP_Text subjectText;
        [SerializeField] private AudioSource speaker;
        [SerializeField] private AudioClip impactClip;
        [SerializeField] private AudioClip peakClip;
        [Header("Hand tuning (metres / seconds)")]
        [SerializeField] private Vector2 padHalfSize = new Vector2(0.25f, 0.18f);
        [SerializeField, Min(0.01f)] private float handRadius = 0.06f;
        [SerializeField, Min(0.1f)] private float minimumSpeed = 0.4f;
        [SerializeField, Min(1f)] private float maximumScoreSpeed = 5.5f;
        [SerializeField, Min(0.75f)] private float hitCooldown = 0.8f;

        private readonly StrengthHitState[] hands = { new StrengthHitState(), new StrengthHitState() };
        private readonly Vector3[] previousRelativeHand = new Vector3[2];
        private readonly bool[] sampledHands = new bool[2];
        private readonly Dictionary<int, float> remoteCooldowns = new Dictionary<int, float>();
        private GorillaLocomotion.Player rig;
        private Room room;
        private Vector3 previousRigPosition, padRest, meterScale, meterPosition;
        private Quaternion previousRigRotation;
        private bool sampledRig, focused = true, paused;
        private float nextHit, meterValue, targetMeter, hitAt = -10f, peakAt = -1f;
        private int best, lastStamp, lastActor;
        private bool hasStamp;

        private void Awake()
        {
            if (strikeFace == null || padVisual == null || meterFill == null ||
                scoreText == null || assessmentText == null || bestText == null ||
                subjectText == null || speaker == null || impactClip == null || peakClip == null)
            {
                Debug.LogError("PrimateStrengthTester: incomplete prefab references.", this);
                enabled = false;
                return;
            }
            padRest = padVisual.localPosition;
            meterScale = meterFill.localScale;
            meterPosition = meterFill.localPosition;
            speaker.playOnAwake = false;
            speaker.spatialBlend = 1f;
            speaker.loop = false;
            ResetVisit();
        }

        private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            if (speaker != null) speaker.Stop();
            ResetVisit();
        }

        private void OnApplicationFocus(bool value) { focused = value; ResetHands(); }
        private void OnApplicationPause(bool value) { paused = value; ResetHands(); }

        private bool CanInteract()
        {
            if (!focused || paused || LoadingFlow.IsColdStartupPresentationActive) return false;
            var travel = SectorTravelService.I;
            if (travel != null && (travel.IsBusy || travel.CurrentSector != SectorId.Hub)) return false;
            return gameObject.scene.name == "Hub_Base" &&
                (!PhotonNetwork.InRoom || SectorPresence.Get(PhotonNetwork.LocalPlayer) == SectorId.Hub);
        }

        private void ResetHands()
        {
            sampledRig = false;
            for (int i = 0; i < hands.Length; i++)
            {
                hands[i].Reset();
                sampledHands[i] = false;
            }
        }

        private void ResetVisit()
        {
            ResetHands();
            best = 0;
            nextHit = 0f;
            hasStamp = false;
            remoteCooldowns.Clear();
            targetMeter = meterValue = 0f;
            peakAt = -1f;
            hitAt = -10f;
            if (scoreText != null) scoreText.text = "---";
            if (assessmentText != null) assessmentText.text = "SLAP THE PAD\nFOR ASSESSMENT";
            if (subjectText != null) subjectText.text = "AWAITING SUBJECT";
            if (bestText != null) bestText.text = "YOUR VISIT BEST  ---";
        }

        private void LateUpdate()
        {
            if (!ReferenceEquals(room, PhotonNetwork.CurrentRoom))
            {
                room = PhotonNetwork.CurrentRoom;
                ResetVisit();
            }
            if (!CanInteract())
            {
                ResetHands();
                peakAt = -1f;
                if (speaker.isPlaying) speaker.Stop();
                return;
            }
            AnimatePresentation();
            var local = GorillaLocomotion.Player.Instance;
            if (local == null || !local.isActiveAndEnabled || local.disableMovement)
            {
                ResetHands();
                return;
            }
            if (rig != local) { rig = local; ResetHands(); }
            Vector3 position = rig.transform.position;
            Quaternion rotation = rig.transform.rotation;
            bool continuous = sampledRig && (position - previousRigPosition).sqrMagnitude < 0.0225f &&
                Quaternion.Angle(rotation, previousRigRotation) < 10f;
            previousRigPosition = position;
            previousRigRotation = rotation;
            if (!continuous) ResetHands();
            sampledRig = true;
            SampleHand(0, rig.leftHandTransform, rig.leftHandOffset, XRNode.LeftHand);
            SampleHand(1, rig.rightHandTransform, rig.rightHandOffset, XRNode.RightHand);
        }

        private void SampleHand(int index, Transform hand, Vector3 offset, XRNode node)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            // Permit the existing non-headset editor rig, but never score a stale XR pose.
            if (hand == null || !hand.gameObject.activeInHierarchy ||
                (XRSettings.isDeviceActive && (!device.isValid ||
                 !device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked)))
            {
                hands[index].Reset();
                sampledHands[index] = false;
                return;
            }
            Vector3 world = hand.position + hand.rotation * offset;
            Vector3 relative = rig.transform.InverseTransformPoint(world);
            Vector3 pad = strikeFace.InverseTransformPoint(world);
            float dt = Time.unscaledDeltaTime;
            if (!sampledHands[index])
            {
                previousRelativeHand[index] = relative;
                sampledHands[index] = true;
                return;
            }
            // Subtract rig locomotion: walking into the pad with a still hand is not a swing.
            Vector3 velocity = rig.transform.TransformVector(relative - previousRelativeHand[index]) / Mathf.Max(dt, 0.001f);
            previousRelativeHand[index] = relative;
            float into = Vector3.Dot(velocity, -strikeFace.forward);
            if (!hands[index].Sample(pad.x, pad.y, pad.z, dt, into, padHalfSize.x, padHalfSize.y,
                handRadius, minimumSpeed, maximumScoreSpeed, out int score) || Time.unscaledTime < nextHit)
                return;

            nextHit = Time.unscaledTime + Mathf.Max(0.75f, hitCooldown);
            best = Mathf.Max(best, score);
            bestText.text = "YOUR VISIT BEST  " + best.ToString("000");
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                device.SendHapticImpulse(0, Mathf.Lerp(0.2f, 0.7f, score / 999f), 0.055f);
            int actor = PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
            int stamp = PhotonNetwork.ServerTimestamp;
            Present(score, actor, stamp);
            if (PhotonNetwork.InRoom)
            {
                var recipients = new List<int>();
                foreach (var player in PhotonNetwork.PlayerList)
                    if (!player.IsLocal && SectorPresence.Get(player) == SectorId.Hub)
                        recipients.Add(player.ActorNumber);
                if (recipients.Count > 0)
                    PhotonNetwork.RaiseEvent(HitEvent, new object[] { 1, stationId, score, stamp },
                        new RaiseEventOptions { TargetActors = recipients.ToArray() }, SendOptions.SendReliable);
            }
        }

        public void OnEvent(EventData data)
        {
            if (data.Code != HitEvent || !isActiveAndEnabled || !CanInteract() || !PhotonNetwork.InRoom ||
                !ReferenceEquals(room, PhotonNetwork.CurrentRoom) ||
                !(data.CustomData is object[] payload) || payload.Length != 4 ||
                !(payload[0] is int version) || version != 1 ||
                !(payload[1] is int id) || id != stationId ||
                !(payload[2] is int score) || score < 1 || score > 999 ||
                !(payload[3] is int stamp)) return;
            var sender = PhotonNetwork.CurrentRoom.GetPlayer(data.Sender);
            if (sender == null || sender.IsLocal || SectorPresence.Get(sender) != SectorId.Hub) return;
            int age = unchecked(PhotonNetwork.ServerTimestamp - stamp);
            if (age < -250 || age > 2000) return;
            if (remoteCooldowns.TryGetValue(data.Sender, out float until) && Time.unscaledTime < until) return;
            // Dictionary is limited to current room occupants, even through repeated rejoins.
            var departed = new List<int>();
            foreach (int actor in remoteCooldowns.Keys)
                if (PhotonNetwork.CurrentRoom.GetPlayer(actor) == null) departed.Add(actor);
            foreach (int actor in departed) remoteCooldowns.Remove(actor);
            remoteCooldowns[data.Sender] = Time.unscaledTime + 0.65f;
            Present(score, data.Sender, stamp);
        }

        private void Present(int score, int actor, int stamp)
        {
            // Stable last-hit ordering for simultaneous swings; wrap-safe server timestamps.
            int order = unchecked(stamp - lastStamp);
            if (hasStamp && (order < 0 || (order == 0 && actor <= lastActor))) return;
            hasStamp = true;
            lastStamp = stamp;
            lastActor = actor;
            scoreText.text = score.ToString("000");
            assessmentText.text = StrengthHitState.Assessment(score);
            subjectText.text = actor == 0 || (PhotonNetwork.LocalPlayer != null && actor == PhotonNetwork.LocalPlayer.ActorNumber)
                ? "YOUR ASSESSMENT" : "SUBJECT " + actor.ToString("00");
            targetMeter = score / 999f;
            hitAt = Time.unscaledTime;
            peakAt = score >= 900 ? hitAt + 0.16f : -1f;
            speaker.pitch = Mathf.Lerp(0.92f, 1.08f, targetMeter);
            speaker.PlayOneShot(impactClip, Mathf.Lerp(0.4f, 0.8f, targetMeter));
        }

        private void AnimatePresentation()
        {
            meterValue = Mathf.MoveTowards(meterValue, targetMeter, Time.unscaledDeltaTime * 2.8f);
            float fill = Mathf.Max(0.002f, meterValue);
            meterFill.localScale = new Vector3(meterScale.x, meterScale.y * fill, meterScale.z);
            meterFill.localPosition = meterPosition - Vector3.up * (meterScale.y * (1f - fill) * 0.5f);
            float age = Time.unscaledTime - hitAt;
            padVisual.localPosition = padRest - Vector3.forward * (0.018f * Mathf.Clamp01(1f - age / 0.18f));
            if (peakAt >= 0f && Time.unscaledTime >= peakAt)
            {
                peakAt = -1f;
                speaker.PlayOneShot(peakClip, 0.45f);
            }
        }
    }
}
