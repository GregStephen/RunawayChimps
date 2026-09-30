using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.Travel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Player = Photon.Realtime.Player;

namespace RunawayChimps.Toys.KnockBack
{
    [DisallowMultipleComponent]
    public sealed class KnockBackVent : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        // Namespaced payload makes accidental event-code sharing with other optional toys inert.
        public const byte EventCode = 189;
        public const string Protocol = "rc.knock.v1";
        private const byte Request = 0, AcceptedTap = 1, ReplyPlan = 2, Cancel = 3;
        private const int MaxParticipants = 10;
        private const double TapLead = 0.2d;

        [Header("Identity (unique per sector; same saved placement on every client)")]
        public string interactionId = "knock-back-vent-hub-01";
        public SectorId sector = SectorId.Hub;
        [Header("Rhythm (changes take effect next time this component enables)")]
        public VentSettings rhythm = new VentSettings();
        [Header("Assigned prefab references; front is local +Z")]
        public BoxCollider panelCollider;
        public Transform panelVisual;
        public AudioSource[] tapVoices;
        public AudioSource[] replyVoices;
        public AudioClip tapClip;
        public AudioClip replyClip;
        public AudioClip bangClip;
        [Header("Presentation")]
        [Range(0f, 1f)] public float tapVolume = 0.4f;
        [Range(0f, 1f)] public float replyVolume = 0.55f;
        [Range(0f, 1f)] public float bangVolume = 0.65f;
        [Range(0f, 0.006f)] public float movementMetres = 0.002f;
        [Header("Local hand filtering")]
        [Range(0.1f, 1f)] public float minimumTapSpeed = 0.3f;
        [Range(2f, 10f)] public float maximumHandSpeed = 7f;
        [Range(0.08f, 0.3f)] public float maximumFrameStep = 0.22f;
        [Range(0.04f, 0.2f)] public float releaseSeconds = 0.07f;

        public VentPhase State => IsAuthority ? model.Phase : playbackPhase;
        public int Controller => controller;
        public int Tapper => IsAuthority ? model.Owner : delivery.Owner;
        public bool IsAuthority => eligible && (diagnostic ||
            (PhotonNetwork.LocalPlayer != null && controller == PhotonNetwork.LocalPlayer.ActorNumber));
        public bool IsDiagnostic => diagnostic;

        private static readonly List<KnockBackVent> Active = new List<KnockBackVent>();
        private readonly KnockBackVentHandInput left = new KnockBackVentHandInput();
        private readonly KnockBackVentHandInput right = new KnockBackVentHandInput();
        private readonly VentDeliveryGate delivery = new VentDeliveryGate();
        private readonly System.Random variation = new System.Random();
        private readonly List<XRInputSubsystem> inputs = new List<XRInputSubsystem>();
        private VentModel model;
        private Room observedRoom;
        private int controller;
        private bool eligible, paused, diagnostic, rigSeeded;
        private int[] participants = Array.Empty<int>();
        private VentPhase playbackPhase;
        private VentReply playingReply;
        private bool replyAudible;
        private double recordingDeadline, previousClock, replyVisualStart;
        private Vector3 visualRest, previousRigPosition, previousHeadLocal;
        private Quaternion previousRigRotation;
        private Transform sampledRig;
        private double diagnosticStart;
        private int diagnosticTap;
        private static readonly double[] SampleOffsets = { 0d, 0.22d, 0.58d };
        private double Clock => diagnostic ? Time.unscaledTimeAsDouble : PhotonNetwork.Time;

        public override void OnEnable()
        {
            model = new VentModel(rhythm);
            minimumTapSpeed = Bounded(minimumTapSpeed, 0.1f, 1f, 0.3f);
            maximumHandSpeed = Bounded(maximumHandSpeed, 2f, 10f, 7f);
            maximumFrameStep = Bounded(maximumFrameStep, 0.08f, 0.3f, 0.22f);
            releaseSeconds = Bounded(releaseSeconds, 0.04f, 0.2f, 0.07f);
            if (panelVisual != null) visualRest = panelVisual.localPosition;
            if (!ReferencesValid())
            {
                Debug.LogError("Knock-Back Vent requires the complete prefab audio/collider/visual references.", this);
                enabled = false;
                return;
            }
            Active.Add(this);
            base.OnEnable();
            SubsystemManager.GetInstances(inputs);
            foreach (var input in inputs) input.trackingOriginUpdated += TrackingOriginChanged;
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
            ResetAll(Clock);
            RefreshContext();
        }

        public override void OnDisable()
        {
            if (model != null && IsAuthority && model.Count > 0) SendCancellation();
            base.OnDisable();
            Active.Remove(this);
            foreach (var input in inputs) input.trackingOriginUpdated -= TrackingOriginChanged;
            inputs.Clear();
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            ResetAll(Clock);
            diagnostic = eligible = false;
            observedRoom = null;
            controller = 0;
        }

        private bool ReferencesValid()
        {
            Vector3 scale = transform.lossyScale;
            if (!VentModel.Finite(scale.x) || !VentModel.Finite(scale.y) || !VentModel.Finite(scale.z) || scale.x <= 0f ||
                Mathf.Abs(scale.x - scale.y) > 0.001f || Mathf.Abs(scale.x - scale.z) > 0.001f) return false;
            if (panelCollider == null || panelCollider.transform != transform || panelCollider.isTrigger ||
                panelVisual == null || panelVisual == transform || !panelVisual.IsChildOf(transform) ||
                GetComponent<BlockHandSurfaceAudio>() == null || tapClip == null || replyClip == null || bangClip == null ||
                tapClip.length > VentModel.AudioTail || replyClip.length > VentModel.AudioTail || bangClip.length > VentModel.AudioTail ||
                tapVoices == null || tapVoices.Length != VentModel.HardTapLimit ||
                replyVoices == null || replyVoices.Length != VentModel.ReplyLimit) return false;
            var unique = new HashSet<AudioSource>();
            foreach (var voice in tapVoices)
                if (!ValidVoice(voice, unique)) return false;
            foreach (var voice in replyVoices)
                if (!ValidVoice(voice, unique) || transform.InverseTransformPoint(voice.transform.position).z >= 0f) return false;
            return true;
        }

        private static float Bounded(float value, float low, float high, float fallback) =>
            VentModel.Finite(value) ? Mathf.Clamp(value, low, high) : fallback;

        private bool ValidVoice(AudioSource voice, HashSet<AudioSource> unique) =>
            voice != null && voice.transform.IsChildOf(transform) && !voice.playOnAwake && !voice.loop &&
            voice.spatialBlend == 1f && unique.Add(voice);

        private bool UniqueIdentity()
        {
            if (string.IsNullOrWhiteSpace(interactionId) || interactionId.Length > 80 || sector == SectorId.None) return false;
            foreach (var other in Active)
                if (other != this && other != null && other.sector == sector && other.interactionId == interactionId)
                    return false;
            return true;
        }

        private void RefreshContext()
        {
            // Tests without a headset are explicitly offline, never a second network authority.
            if (diagnostic && PhotonNetwork.InRoom) diagnostic = false;
            Room room = PhotonNetwork.CurrentRoom;
            var travel = SectorTravelService.I;
            bool nextEligible = isActiveAndEnabled && panelCollider != null && panelCollider.enabled &&
                !paused && UniqueIdentity() && gameObject.scene == SceneManager.GetActiveScene() &&
                (diagnostic || (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null &&
                    SectorPresence.Get(PhotonNetwork.LocalPlayer) == sector &&
                    (travel == null || !travel.IsBusy) && !LoadingFlow.IsColdStartupPresentationActive &&
                    (AppState.I == null || AppState.I.IsReady)));
            int elected = nextEligible && !diagnostic
                ? SectorPresence.ElectController(PhotonNetwork.PlayerList, sector) : 0;
            double now = Clock;
            if (!ReferenceEquals(room, observedRoom) || nextEligible != eligible || elected != controller ||
                now < previousClock || now - previousClock > VentModel.FrameGapLimit)
            {
                // No replay on room replacement, sector return, authority A -> B -> A or long stalls.
                if (ReferenceEquals(room, observedRoom) && IsAuthority && model.Count > 0) SendCancellation();
                ResetAll(now);
                observedRoom = room;
                eligible = nextEligible;
                controller = elected;
            }
            previousClock = now;
            if (eligible && !diagnostic && delivery.Owner != 0 &&
                SectorPresence.Get(room.GetPlayer(delivery.Owner)) != sector)
            {
                if (IsAuthority) SendCancellation();
                ResetAll(now);
            }
        }

        private void LateUpdate()
        {
            RefreshContext();
            if (!eligible) return;
            double now = Clock;
            if (IsAuthority) AdvanceAuthority(now);
            if (playingReply != null)
            {
                playbackPhase = now >= playingReply.End ? VentPhase.Cooldown : VentPhase.Replying;
                if (now >= playingReply.CooldownUntil)
                {
                    double finished = playingReply.CooldownUntil;
                    ClearPlayback();
                    delivery.Retire(finished);
                }
                else if (replyAudible) UpdateVisual(now);
            }
            else if (playbackPhase == VentPhase.Recording && now > recordingDeadline)
                ResetAll(now); // A missing authority cannot leave an infinite recording.

            if (diagnostic)
            {
                if (diagnosticTap < SampleOffsets.Length && now >= diagnosticStart + SampleOffsets[diagnosticTap])
                {
                    AcceptRequest(1, diagnosticStart + SampleOffsets[diagnosticTap], now);
                    diagnosticTap++;
                }
            }
            else SampleHands(now);
        }

        private void AdvanceAuthority(double now)
        {
            VentReply previous = model.Reply;
            VentReply plan = model.Advance(now, model.ReadyToReply(now) ? variation.NextDouble() : 1d);
            if (previous != null && now >= previous.CooldownUntil)
            {
                // Retire at the actual deadline, not the next render frame. Reject packets
                // sent during cooldown without rejecting a fresh tap just after that deadline.
                ClearPlayback();
                delivery.Retire(previous.CooldownUntil);
                participants = Array.Empty<int>();
            }
            if (plan != null)
                Broadcast(ReplyPlan, new object[] { plan.Start, plan.CooldownUntil, plan.Offsets, plan.HeavyIndex });
        }

        private void SampleHands(double now)
        {
            var player = GorillaLocomotion.Player.Instance;
            var marker = player != null ? player.GetComponentInParent<LocalRigMarker>() : null;
            if (player == null || !player.isActiveAndEnabled || player.disableMovement ||
                player.headCollider == null || marker == null)
            { ResetHands(); return; }
            Transform rig = marker.transform;
            Vector3 headLocal = rig.InverseTransformPoint(player.headCollider.transform.position);
            bool continuous = rigSeeded && sampledRig == rig &&
                Vector3.Distance(rig.position, previousRigPosition) <= maximumFrameStep &&
                Vector3.Distance(headLocal, previousHeadLocal) <= maximumFrameStep &&
                Quaternion.Angle(rig.rotation, previousRigRotation) <= 15f;
            sampledRig = rig;
            previousRigPosition = rig.position;
            previousRigRotation = rig.rotation;
            previousHeadLocal = headLocal;
            rigSeeded = true;
            if (!continuous) { left.Reset(); right.Reset(); return; }
            bool tappedLeft = left.Sample(player, rig, player.leftHandTransform, player.leftHandOffset, XRNode.LeftHand,
                panelCollider, now, minimumTapSpeed, maximumHandSpeed, maximumFrameStep, releaseSeconds);
            bool tappedRight = right.Sample(player, rig, player.rightHandTransform, player.rightHandOffset, XRNode.RightHand,
                panelCollider, now, minimumTapSpeed, maximumHandSpeed, maximumFrameStep, releaseSeconds);
            if (tappedLeft || tappedRight) RequestTap(now); // both hands in one frame are one audible tap
        }

        private void RequestTap(double time)
        {
            if (State == VentPhase.Replying || State == VentPhase.Cooldown || controller <= 0) return;
            if (IsAuthority) AcceptRequest(PhotonNetwork.LocalPlayer.ActorNumber, time, Clock);
            else PhotonNetwork.RaiseEvent(EventCode,
                new object[] { Protocol, (int)sector, interactionId, Request, observedRoom.Name, time },
                new RaiseEventOptions { TargetActors = new[] { controller }, CachingOption = EventCaching.DoNotCache },
                SendOptions.SendReliable);
        }

        private void AcceptRequest(int actor, double inputTime, double now)
        {
            if (!IsAuthority) return;
            AdvanceAuthority(now);
            if (!delivery.AcceptsInput(inputTime) || !model.TryTap(actor, inputTime, now)) return;
            if (model.Count == 1)
            {
                // Snapshot the original audience. Late sector/room arrivals do not receive even
                // the remainder of an old reply, and no cached events or sync requests exist.
                var audience = new List<int>(MaxParticipants);
                foreach (var member in PhotonNetwork.PlayerList)
                    if (audience.Count < MaxParticipants && member != null && !member.IsLocal &&
                        SectorPresence.Get(member) == sector) audience.Add(member.ActorNumber);
                participants = audience.ToArray();
            }
            Broadcast(AcceptedTap, new object[] { model.Count - 1, inputTime + TapLead });
        }

        private void Broadcast(byte kind, object[] payload)
        {
            var data = new object[] { Protocol, (int)sector, interactionId, kind,
                observedRoom != null ? observedRoom.Name : "", model.Cycle, Clock, model.Owner, payload };
            if (!diagnostic && participants.Length > 0 &&
                !PhotonNetwork.RaiseEvent(EventCode, data,
                    new RaiseEventOptions { TargetActors = participants, CachingOption = EventCaching.DoNotCache },
                    SendOptions.SendReliable))
            {
                SendCancellation();
                ResetAll(Clock); // Do not play a private authority-only answer after a failed send.
                return;
            }
            ReceiveAuthority(data, Clock);
        }

        private void SendCancellation()
        {
            if (!diagnostic && PhotonNetwork.InRoom && observedRoom != null &&
                ReferenceEquals(PhotonNetwork.CurrentRoom, observedRoom) &&
                model != null && model.Count > 0 && participants.Length > 0)
                PhotonNetwork.RaiseEvent(EventCode,
                    new object[] { Protocol, (int)sector, interactionId, Cancel, observedRoom.Name,
                        model.Cycle, Clock, model.Owner, Array.Empty<object>() },
                    new RaiseEventOptions { TargetActors = participants, CachingOption = EventCaching.DoNotCache },
                    SendOptions.SendReliable);
        }

        public void OnEvent(EventData message)
        {
            if (message.Code != EventCode || !(message.CustomData is object[] data) || data.Length < 6 ||
                !(data[0] is string protocol) || protocol != Protocol || !(data[1] is int targetSector) ||
                targetSector != (int)sector || !(data[2] is string id) || id != interactionId ||
                !(data[3] is byte kind) || !(data[4] is string roomName)) return;
            RefreshContext();
            if (!eligible || diagnostic || observedRoom == null || roomName != observedRoom.Name ||
                SectorPresence.Get(observedRoom.GetPlayer(message.Sender)) != sector) return;
            if (kind == Request)
            {
                if (data.Length == 6 && data[5] is double inputTime) AcceptRequest(message.Sender, inputTime, Clock);
            }
            else if (message.Sender == controller) ReceiveAuthority(data, Clock);
        }

        private void ReceiveAuthority(object[] data, double now)
        {
            if (data.Length != 9 || !(data[3] is byte kind) || !(data[5] is double cycle) ||
                !(data[6] is double issued) || !(data[7] is int owner) || !(data[8] is object[] payload)) return;
            VentReply plan = null;
            if (kind == AcceptedTap)
            {
                if (issued - cycle > 5.1d || payload.Length != 2 || !(payload[0] is int ordinal) || ordinal < 0 || ordinal >= VentModel.HardTapLimit ||
                    !(payload[1] is double at) || !VentModel.Finite(at) || at < issued - 0.56d || at > issued + TapLead + 0.11d) return;
            }
            else if (kind == ReplyPlan)
            {
                if (issued - cycle > VentModel.LatestPlanIssue || payload.Length != 4 || !(payload[0] is double start) || !(payload[1] is double cooldownEnd) ||
                    !(payload[2] is float[] offsets) || !(payload[3] is int heavy)) return;
                plan = new VentReply(start, cooldownEnd, offsets, heavy);
                if (!VentDeliveryGate.ValidReply(plan, issued)) return;
            }
            else if (kind != Cancel || payload.Length != 0) return;
            if (!diagnostic && SectorPresence.Get(observedRoom.GetPlayer(owner)) != sector) return;
            bool newCycle = cycle > delivery.Cycle;
            if (!delivery.Header(cycle, issued, now, owner)) return;
            if (newCycle) ClearPlayback();
            if (kind == Cancel)
            {
                // A cancellation retires input through its authority issue time, not
                // arrival time. Otherwise transit latency also discards a newer live cycle.
                ResetAll(now, issued);
                return;
            }
            if (kind == AcceptedTap)
            {
                int ordinal = (int)payload[0];
                if (!delivery.Tap(ordinal)) return;
                playbackPhase = VentPhase.Recording;
                recordingDeadline = cycle + VentModel.RecordingTimeout;
                Schedule(tapVoices[ordinal], tapClip, (double)payload[1], tapVolume, now);
            }
            else if (delivery.Seal())
            {
                playingReply = plan;
                playbackPhase = VentPhase.Replying;
                // Drop an excessively late plan as a whole, never compress old knocks into a burst.
                replyAudible = plan.Start >= now - 0.08d;
                if (!replyAudible) return;
                // One DSP anchor preserves the complete rhythm even if the audio clock
                // advances while scheduling. A tolerably late plan shifts as a whole;
                // clamping each knock separately would compress its first interval.
                double lead = Math.Max(0.01d, plan.Start - now);
                double dspStart = AudioSettings.dspTime + lead;
                replyVisualStart = now + lead;
                for (int i = 0; i < plan.Offsets.Length; i++)
                    ScheduleAtDsp(replyVoices[i], i == plan.HeavyIndex ? bangClip : replyClip,
                        dspStart + plan.Offsets[i], i == plan.HeavyIndex ? bangVolume : replyVolume);
            }
        }

        private static void Schedule(AudioSource voice, AudioClip clip, double at, float volume, double now)
        {
            if (at < now - 0.08d) return;
            ScheduleAtDsp(voice, clip, AudioSettings.dspTime + Math.Max(0.01d, at - now), volume);
        }

        private static void ScheduleAtDsp(AudioSource voice, AudioClip clip, double at, float volume)
        {
            voice.Stop();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume);
            voice.pitch = 1f;
            voice.PlayScheduled(at);
        }

        private void UpdateVisual(double now)
        {
            float displacement = 0f;
            for (int i = 0; i < playingReply.Offsets.Length; i++)
            {
                double elapsed = now - replyVisualStart - playingReply.Offsets[i];
                if (elapsed >= 0d && elapsed < 0.18d)
                    displacement += Mathf.Sin((float)(elapsed / 0.18d) * Mathf.PI * 2f) *
                        (1f - (float)(elapsed / 0.18d)) * (i == playingReply.HeavyIndex ? 1.5f : 1f);
            }
            panelVisual.localPosition = visualRest + Vector3.forward *
                Mathf.Clamp(displacement * Mathf.Clamp(movementMetres, 0f, 0.006f), -0.006f, 0.006f);
        }

        private void ClearPlayback()
        {
            if (tapVoices != null) foreach (var voice in tapVoices) if (voice != null) voice.Stop();
            if (replyVoices != null) foreach (var voice in replyVoices) if (voice != null) voice.Stop();
            if (panelVisual != null) panelVisual.localPosition = visualRest;
            playingReply = null;
            replyAudible = false;
            replyVisualStart = 0d;
            playbackPhase = VentPhase.Idle;
            recordingDeadline = 0d;
        }
        private void ResetHands() { left.Reset(); right.Reset(); rigSeeded = false; sampledRig = null; }
        private void ResetAll(double now, double? retireThrough = null)
        {
            // Pending diagnostic input belongs to the cancelled run too. Only the
            // explicit PlayEditorSample command may seed a fresh sample sequence.
            diagnosticTap = SampleOffsets.Length;
            model?.Cancel();
            delivery.Reset(retireThrough ?? now);
            participants = Array.Empty<int>();
            ClearPlayback();
            ResetHands();
            previousClock = now;
        }
        private void TrackingOriginChanged(XRInputSubsystem subsystem) => ResetHands();
        private void AudioConfigurationChanged(bool deviceWasChanged)
        {
            if (IsAuthority && model.Count > 0) SendCancellation();
            ResetAll(Clock);
        }
        private void OnApplicationPause(bool pause) { paused = pause; RefreshContext(); }
        public override void OnLeftRoom() => RefreshContext();
        public override void OnJoinedRoom() => RefreshContext();
        public override void OnDisconnected(DisconnectCause cause) => RefreshContext();
        public override void OnPlayerLeftRoom(Player otherPlayer) => RefreshContext();
        public override void OnPlayerEnteredRoom(Player newPlayer) => RefreshContext();
        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) => RefreshContext();
        public override void OnMasterClientSwitched(Player newMasterClient) => RefreshContext();

#if UNITY_EDITOR
        public void PlayEditorSample()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || model == null || PhotonNetwork.InRoom ||
                gameObject.scene != SceneManager.GetActiveScene())
            { Debug.LogWarning("Select an active vent in the active scene, enter Play Mode and disconnect Photon for the sample.", this); return; }
            diagnostic = true;
            ResetAll(Clock);
            RefreshContext();
            diagnosticTap = 0;
            diagnosticStart = Clock + 0.25d;
        }
        public void CancelEditorSample()
        {
            if (!diagnostic) return;
            ResetAll(Clock);
            diagnostic = false;
            RefreshContext();
        }
#endif
    }
}
