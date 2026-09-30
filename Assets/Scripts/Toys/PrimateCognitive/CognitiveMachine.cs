using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.Travel;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunawayChimps.Toys.PrimateCognitive
{
    [DisallowMultipleComponent]
    public sealed class CognitiveMachine : MonoBehaviour, IOnEventCallback
    {
        private const byte RequestEvent = 188, SnapshotEvent = 189;
        private const string Protocol = "rc.cognitive.1";
        private const int Sync = 0, StartCommand = 1, PressCommand = 2, HeartbeatCommand = 3, ReleaseCommand = 4;
        [Header("Placement identity (unique per sector)")]
        public string machineId = "hub-cognitive-01";
        public SectorId sector = SectorId.Hub;
        [Header("Authored references")]
        public CognitivePad[] pads;
        public TMP_Text display;
        public AudioSource audioSource;
        [Tooltip("Four distinct pad notes, success reserved at 4, failure at 5.")]
        public AudioClip[] clips;
        public Transform operatorAnchor;
        [Header("Pacing (new authority term)")]
        [Range(1, CognitiveGame.HardMaximum)] public int maximumLength = 8;
        [Range(.3f, 3f)] public float demonstrationLead = .7f;
        [Range(.3f, 2f)] public float stepSeconds = .65f;
        [Range(.15f, .8f)] public float flashSeconds = .32f;
        [Range(.5f, 4f)] public float successSeconds = 1.1f;
        [Header("Session recovery")]
        [Range(3f, 15f)] public float heartbeatTimeout = 4f;
        [Range(5f, 90f)] public float inputIdleTimeout = 20f;
        [Range(3f, 30f)] public float resultHoldSeconds = 10f;
        [Min(.5f)] public float operatorRadius = 1.8f;
        [Header("Editor-only opt-in; never enables build inputs")]
        public bool editorControls;

        private CognitiveGame game;
        private readonly CognitiveReplica replica = new CognitiveReplica();
        private readonly Dictionary<int, int> lastCommand = new Dictionary<int, int>();
        private readonly List<int> recipients = new List<int>(10);
        private Room observedRoom;
        // Survive scene/component replacement while another client retains authority.
        private static int localSerial;
        private string authorityEpoch = "";
        private double nextSync, nextHeartbeat;
        private int renderedRevision = -1, heardCue = -1;
        private string renderedEpoch = "";
        private bool paused, valid;
        public CognitiveSnapshot State => replica.State;
        public bool IsOperator => replica.Ready && State.Owner == LocalActor;
        private int LocalActor => PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 1;
        private bool IsAuthority => replica.Authority != 0 && replica.Authority == LocalActor;
        private double Now => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.unscaledTimeAsDouble;

        private void Awake()
        {
            valid = !string.IsNullOrWhiteSpace(machineId) && machineId.Length <= 80 && sector != SectorId.None &&
                pads != null && pads.Length == 5 && display != null && audioSource != null &&
                clips != null && clips.Length == 6 && operatorAnchor != null;
            if (valid)
            {
                for (int i = 0; i < 5; i++) valid &= pads[i] != null && pads[i].machine == this && pads[i].index == i;
                for (int i = 0; i < 6; i++) valid &= clips[i] != null;
            }
            if (!valid)
            {
                Debug.LogError("Cognitive machine has missing/invalid authored references. Use its shipped prefab.", this);
                enabled = false;
                return;
            }
            display.richText = false;
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
        }

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
            ResetTransport();
        }

        private void Start()
        {
            // Duplicate placement IDs fail closed rather than operating two models for one machine.
            foreach (var other in FindObjectsOfType<CognitiveMachine>(true))
            {
                if (other == this || other.sector != sector || other.machineId != machineId) continue;
                Debug.LogError("Duplicate cognitive machine identity: " + machineId + ". Assign a unique ID in the Inspector.", this);
                other.enabled = false;
                enabled = false;
                return;
            }
        }

        private CognitiveGame NewGame() => new CognitiveGame(maximumLength, unchecked((uint)Guid.NewGuid().GetHashCode()),
            demonstrationLead, stepSeconds, flashSeconds, successSeconds, heartbeatTimeout, inputIdleTimeout, resultHoldSeconds);

        private void ResetTransport()
        {
            replica.Reset();
            game = NewGame();
            authorityEpoch = "";
            lastCommand.Clear();
            nextSync = nextHeartbeat = 0;
            renderedRevision = heardCue = -1;
            renderedEpoch = "";
            RequireAllReleased();
            if (audioSource != null) audioSource.Stop();
        }

        private bool LocalSectorAvailable()
        {
            if (paused || !valid || !isActiveAndEnabled || !gameObject.scene.isLoaded ||
                gameObject.scene != SceneManager.GetActiveScene()) return false;
            var travel = SectorTravelService.I;
            if (travel != null && (travel.IsBusy || travel.CurrentSector != sector)) return false;
            if (LoadingFlow.IsColdStartupPresentationActive) return false;
            if (PhotonNetwork.InRoom) return SectorPresence.Get(PhotonNetwork.LocalPlayer) == sector;
#if UNITY_EDITOR
            return editorControls;
#else
            return false;
#endif
        }

        private bool NearControls()
        {
#if UNITY_EDITOR
            if (editorControls) return true; // Explicit Inspector test fixture; still sector/owner filtered.
#endif
            var rig = GorillaLocomotion.Player.Instance;
            return rig != null && rig.headCollider != null && operatorAnchor != null &&
                Vector3.Distance(rig.headCollider.transform.position, operatorAnchor.position) <= Mathf.Max(.5f, operatorRadius);
        }

        private void RefreshAuthority()
        {
            if (!ReferenceEquals(observedRoom, PhotonNetwork.CurrentRoom))
            {
                observedRoom = PhotonNetwork.CurrentRoom;
                ResetTransport();
            }
            int elected = LocalSectorAvailable() ? (PhotonNetwork.InRoom ?
                SectorPresence.ElectController(PhotonNetwork.PlayerList, sector) : LocalActor) : 0;
            if (elected == replica.Authority) return;
            if (!replica.Elect(elected, Guid.NewGuid().ToString("N"))) return;
            RequireAllReleased();
            audioSource.Stop();
            renderedRevision = heardCue = -1;
            renderedEpoch = "";
            nextSync = 0;
            lastCommand.Clear();
            if (IsAuthority)
            {
                game = NewGame();
                authorityEpoch = Guid.NewGuid().ToString("N");
                ApplySnapshot(LocalActor, authorityEpoch, replica.RequestNonce, game.Snapshot(), true);
                Broadcast();
            }
        }

        private void Update()
        {
            if (!valid) return;
            RefreshAuthority();
            if (!LocalSectorAvailable())
            {
                if (audioSource.isPlaying) audioSource.Stop();
                Render();
                return;
            }
            double now = Now;
            if (replica.Ready && State.Owner == LocalActor)
            {
                if (!NearControls()) SendCommand(ReleaseCommand, -1);
                else if (now >= nextHeartbeat)
                {
                    SendCommand(HeartbeatCommand, -1);
                    nextHeartbeat = now + .75;
                }
            }
            if (IsAuthority) TickGame();
            else if (replica.Authority != 0 && now >= nextSync)
            {
                replica.RequestSync(Guid.NewGuid().ToString("N"));
                SendCommand(Sync, -1);
                nextSync = now + (replica.Ready ? 2 : .5);
            }
            Render();
        }

        private void TickGame()
        {
            int before = game.Revision;
            bool present = !PhotonNetwork.InRoom || SectorPresence.Get(PhotonNetwork.CurrentRoom.GetPlayer(game.Owner)) == sector;
            game.Tick(Now, present);
            if (before != game.Revision) Broadcast();
        }

        // Both real local-hand edges and the Editor fixture enter here, never a separate game.
        public void ContactPressed(int index)
        {
            if (index < 0 || index > 4 || !LocalSectorAvailable() || !NearControls() || !replica.Ready) return;
            if (index == 4)
            {
                if (State.Owner == 0 || IsOperator) SendCommand(StartCommand, -1);
            }
            else if (IsOperator && State.Phase == CognitivePhase.Input) SendCommand(PressCommand, index);
        }

        private void SendCommand(int kind, int button)
        {
            if (replica.Authority == 0 || localSerial == int.MaxValue) return;
            var state = State;
            object[] data = { Protocol, machineId, (int)sector, kind, replica.Epoch,
                state == null ? 0 : state.Session, state == null ? 0 : state.PhaseToken,
                ++localSerial, button, kind == Sync ? replica.RequestNonce : "" };
            if (IsAuthority) HandleCommand(LocalActor, data);
            else if (PhotonNetwork.InRoom)
                PhotonNetwork.RaiseEvent(RequestEvent, data,
                    new RaiseEventOptions { TargetActors = new[] { replica.Authority } }, SendOptions.SendReliable);
        }

        private void HandleCommand(int sender, object[] d)
        {
            if (!IsAuthority || d.Length != 10 || !(d[3] is int kind) || kind < Sync || kind > ReleaseCommand ||
                !(d[4] is string epoch) || !(d[5] is int session) || !(d[6] is int phase) ||
                !(d[7] is int serial) || serial <= 0 || !(d[8] is int button) || !(d[9] is string nonce)) return;
            bool present = !PhotonNetwork.InRoom || SectorPresence.Get(PhotonNetwork.CurrentRoom.GetPlayer(sender)) == sector;
            if (!present && !(kind == ReleaseCommand && sender == game.Owner)) return;
            if (kind == Sync)
            {
                if (nonce.Length == 32) SendSnapshot(sender, nonce);
                return;
            }
            if (epoch != authorityEpoch) return;
            TickGame();
            // A departed/recreated client may have a new serial stream. An idle game
            // has no accepted operator commands to preserve; Start still checks tokens.
            if (game.Owner == 0) lastCommand.Clear();
            if (session != game.Session || (lastCommand.TryGetValue(sender, out int previous) && serial <= previous)) return;
            lastCommand[sender] = serial;
            int revision = game.Revision;
            switch (kind)
            {
                case StartCommand: game.Start(sender, session, phase, Now); break;
                case PressCommand: game.Press(sender, button, session, phase, Now); break;
                case HeartbeatCommand: game.Heartbeat(sender, Now); break;
                case ReleaseCommand: if (game.Owner == sender) game.Release(true); break;
            }
            if (revision != game.Revision) Broadcast();
        }

        private void Broadcast()
        {
            var snapshot = game.Snapshot();
            ApplySnapshot(LocalActor, authorityEpoch, "", snapshot, false);
            if (!PhotonNetwork.InRoom) return;
            recipients.Clear();
            foreach (var player in PhotonNetwork.PlayerList)
                if (!player.IsLocal && SectorPresence.Get(player) == sector) recipients.Add(player.ActorNumber);
            if (recipients.Count == 0) return;
            PhotonNetwork.RaiseEvent(SnapshotEvent,
                new object[] { Protocol, machineId, (int)sector, authorityEpoch, "", snapshot.Pack() },
                new RaiseEventOptions { TargetActors = recipients.ToArray() }, SendOptions.SendReliable);
        }

        private void SendSnapshot(int actor, string nonce)
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.RaiseEvent(SnapshotEvent,
                new object[] { Protocol, machineId, (int)sector, authorityEpoch, nonce, game.Snapshot().Pack() },
                new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
        }

        public void OnEvent(EventData e)
        {
            if (!valid || !PhotonNetwork.InRoom || (e.Code != RequestEvent && e.Code != SnapshotEvent)) return;
            if (!(e.CustomData is object[] d) || d.Length < 3 || !(d[0] is string tag) || tag != Protocol ||
                !(d[1] is string id) || id != machineId || !(d[2] is int eventSector) || eventSector != (int)sector) return;
            RefreshAuthority();
            if (!LocalSectorAvailable()) return;
            if (e.Code == RequestEvent) { HandleCommand(e.Sender, d); return; }
            if (d.Length != 6 || !(d[3] is string epoch) || !(d[4] is string echo) ||
                !CognitiveSnapshot.TryRead(d[5], out var snapshot)) return;
            ApplySnapshot(e.Sender, epoch, echo, snapshot, false);
        }

        private void ApplySnapshot(int sender, string epoch, string echo, CognitiveSnapshot snapshot, bool silent)
        {
            bool newTerm = !replica.Ready || replica.Epoch != epoch;
            int oldPhase = replica.Ready ? State.PhaseToken : -1;
            if (!replica.Accept(sender, epoch, echo, snapshot)) return;
            if (newTerm || oldPhase != snapshot.PhaseToken) RequireAllReleased();
            if (newTerm || silent)
            {
                heardCue = snapshot.CueSerial;
                audioSource.Stop();
            }
            else if (snapshot.CueSerial > heardCue)
            {
                heardCue = snapshot.CueSerial;
                double age = Now - snapshot.CueAt;
                if (snapshot.Cue >= 0 && snapshot.Cue < clips.Length && age >= -.15 && age <= .6 && LocalSectorAvailable())
                    audioSource.PlayOneShot(clips[snapshot.Cue]);
            }
        }

        private void RequireAllReleased()
        {
            if (pads == null) return;
            foreach (var pad in pads) if (pad != null) pad.RequireRelease();
        }

        private void Render()
        {
            var state = State;
            bool ready = replica.Ready && LocalSectorAvailable();
            double age = ready ? Now - state.CueAt : double.MaxValue;
            for (int i = 0; i < 5; i++)
                pads[i].Present(ready && (i == 4 ? state.Owner == 0 || state.Phase == CognitivePhase.Failure || state.Phase == CognitivePhase.Complete :
                    state.Cue == i && age >= 0 && age < state.FlashSeconds));
            int revision = ready ? state.Revision : -1;
            string epoch = ready ? replica.Epoch : "";
            if (renderedRevision == revision && renderedEpoch == epoch && display.text.Length != 0) return;
            renderedRevision = revision;
            renderedEpoch = epoch;
            if (!ready)
            {
                display.text = "ASSESSMENT OFFLINE\nWaiting for local sector / authority.\nEditor: enable the test controls.";
                return;
            }
            string instructions = state.Owner == 0 ? "PRESS START" : state.Phase == CognitivePhase.Demonstrating ? "WATCH - HANDS CLEAR" :
                state.Phase == CognitivePhase.Input ? "YOUR TURN  " + state.InputIndex + " / " + state.Round :
                state.Phase == CognitivePhase.Success ? "ROUND PASSED - NEXT TEST" : "PRESS RESTART TO TRY AGAIN";
            string owner = state.Owner == 0 ? "AVAILABLE" : "SUBJECT " + state.Owner + (state.Owner == LocalActor ? " (YOU)" : " - OBSERVE ONLY");
            display.text = "ROUND " + state.Round + " / " + state.Maximum + "   " + state.Phase.ToString().ToUpperInvariant() +
                "\n" + state.Assessment + "\n" + instructions + "\n" + owner;
        }

        private void ReleaseLocal()
        {
            if (IsOperator) SendCommand(ReleaseCommand, -1);
            RequireAllReleased();
            if (audioSource != null) audioSource.Stop();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) ReleaseLocal();
            paused = pause;
            RequireAllReleased();
        }

        private void OnDisable()
        {
            ReleaseLocal();
            PhotonNetwork.RemoveCallbackTarget(this);
            replica.Reset();
        }

#if UNITY_EDITOR
        public void EditorContact(int index, bool held)
        {
            if (pads == null || index < 0 || index >= pads.Length || (held && !editorControls)) return;
            pads[index].EditorContact(held);
        }
#endif
    }
}
