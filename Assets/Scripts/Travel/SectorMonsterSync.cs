using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.Monsters;
using UnityEngine;

namespace RunawayChimps.Travel
{
    // Explicit messages avoid scene PhotonViews/RPCs that are absent on Hub clients.
    [DefaultExecutionOrder(-10)]
    public sealed class SectorMonsterSync : MonoBehaviour, IOnEventCallback
    {
        public const byte StateEvent = 180;
        public const byte RequestEvent = 181;
        private const int ProtocolVersion = 4;

        public SectorId sector = SectorId.Containment;
        public int monsterId = 1;
        public float updatesPerSecond = 10;
        [Min(0.25f)] public float pursuitStateTimeout = 1.5f;
        public bool HasAuthority { get; private set; }

        private readonly List<MonoBehaviour> providerBuffer = new List<MonoBehaviour>();
        private readonly Dictionary<int, double> retiredAuthorityEpochFloor = new Dictionary<int, double>();
        private IMonsterPursuitSyncTarget pursuit;
        private int controller;
        private bool hasState;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private double authorityEpoch = double.MinValue;
        private double lastGeneratedAuthorityEpoch = double.MinValue;
        private double acceptedAuthorityEpoch = double.MinValue;
        private int outgoingStateRevision;
        private int lastReceivedStateRevision = -1;
        private float lastReceiveUnscaled = float.NegativeInfinity;
        private float nextSend;
        private float nextRequest;
        private Room observedRoom;

        private void Awake()
        {
            ResolvePursuitTarget();
            if (pursuit == null)
            {
                Debug.LogError("SectorMonsterSync requires a monster brain implementing IMonsterPursuitSyncTarget on the same object.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
            if (pursuit == null)
                ResolvePursuitTarget();
            if (pursuit != null)
                pursuit.PursuitChanged += HandlePursuitChanged;
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            if (pursuit != null)
            {
                pursuit.PursuitChanged -= HandlePursuitChanged;
                pursuit.ApplyRemotePursuit(false, 0);
            }
            HasAuthority = false;
        }

        private void ResolvePursuitTarget()
        {
            providerBuffer.Clear();
            GetComponents(providerBuffer);
            for (int i = 0; i < providerBuffer.Count; i++)
            {
                if (providerBuffer[i] is IMonsterPursuitSyncTarget target)
                {
                    pursuit = target;
                    providerBuffer.Clear();
                    return;
                }
            }
            providerBuffer.Clear();
            pursuit = null;
        }

        private void RefreshRoom()
        {
            Room room = PhotonNetwork.CurrentRoom;
            if (ReferenceEquals(room, observedRoom))
                return;

            observedRoom = room;
            controller = 0;
            hasState = HasAuthority = false;
            authorityEpoch = double.MinValue;
            lastGeneratedAuthorityEpoch = double.MinValue;
            acceptedAuthorityEpoch = double.MinValue;
            outgoingStateRevision = 0;
            lastReceivedStateRevision = -1;
            retiredAuthorityEpochFloor.Clear();
            lastReceiveUnscaled = float.NegativeInfinity;
            nextRequest = nextSend = 0f;
            pursuit?.ApplyRemotePursuit(false, 0);
        }

        private void Update()
        {
            RefreshRoom();
            if (pursuit == null)
                return;

            int elected = PhotonNetwork.InRoom
                ? SectorPresence.ElectController(PhotonNetwork.PlayerList, sector)
                : 0;
            ApplyControllerElection(elected);

            if (HasAuthority)
            {
                if (Time.unscaledTime >= nextSend)
                {
                    SendState(null, false);
                    nextSend = Time.unscaledTime + 1f / Mathf.Max(1f, updatesPerSecond);
                }
                return;
            }

            if (controller != 0 && Time.unscaledTime >= nextRequest)
            {
                PhotonNetwork.RaiseEvent(
                    RequestEvent,
                    new object[] { (int)sector, monsterId },
                    new RaiseEventOptions { TargetActors = new[] { controller } },
                    SendOptions.SendReliable);
                nextRequest = Time.unscaledTime + (hasState ? 3f : 0.5f);
            }

            if (hasState)
            {
                float blend = 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime);
                transform.position = Vector3.Lerp(transform.position, targetPosition, blend);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);

                if (pursuit.PursuitState.IsPursuing &&
                    Time.unscaledTime - lastReceiveUnscaled > Mathf.Max(0.25f, pursuitStateTimeout))
                {
                    // Pose may remain useful while packets recover, but personal threat identity
                    // must fail closed rather than remaining stuck on an old target.
                    pursuit.ApplyRemotePursuit(false, 0);
                    nextRequest = 0f;
                }
            }
        }

        private void ApplyControllerElection(int elected)
        {
            if (elected == controller)
            {
                HasAuthority = elected != 0 && PhotonNetwork.LocalPlayer != null &&
                    elected == PhotonNetwork.LocalPlayer.ActorNumber;
                return;
            }

            int previousController = controller;
            bool wasAuthority = HasAuthority;
            if (previousController != 0 && acceptedAuthorityEpoch != double.MinValue)
                RetireAuthorityEpoch(previousController, acceptedAuthorityEpoch);

            controller = elected;
            HasAuthority = elected != 0 && PhotonNetwork.LocalPlayer != null &&
                elected == PhotonNetwork.LocalPlayer.ActorNumber;

            // Each elected-controller term gets a new epoch and its own revision sequence.
            // Retired epoch floors prevent a delayed packet from Actor A's old term being
            // accepted after an A -> B -> A handoff.
            acceptedAuthorityEpoch = double.MinValue;
            outgoingStateRevision = 0;
            lastReceivedStateRevision = -1;

            if (HasAuthority && !wasAuthority)
            {
                if (hasState)
                    transform.SetPositionAndRotation(targetPosition, targetRotation);

                double candidate = PhotonNetwork.Time;
                if (candidate <= lastGeneratedAuthorityEpoch)
                    candidate = lastGeneratedAuthorityEpoch + 0.000001d;
                authorityEpoch = candidate;
                lastGeneratedAuthorityEpoch = candidate;

                // A newly elected controller must make a fresh local target decision.
                // Never rebroadcast the previous controller's player identity as its own.
                pursuit.ApplyRemotePursuit(false, 0);
            }
            else
            {
                authorityEpoch = double.MinValue;
                if (!HasAuthority)
                    pursuit.ApplyRemotePursuit(false, 0);
            }

            lastReceiveUnscaled = float.NegativeInfinity;
            nextRequest = nextSend = 0f;
        }

        private void RetireAuthorityEpoch(int actorNumber, double epoch)
        {
            if (actorNumber <= 0 || double.IsNaN(epoch) || double.IsInfinity(epoch))
                return;

            if (!retiredAuthorityEpochFloor.TryGetValue(actorNumber, out double existing) || epoch > existing)
                retiredAuthorityEpochFloor[actorNumber] = epoch;
        }

        private void HandlePursuitChanged(MonsterPursuitState state)
        {
            if (HasAuthority)
                SendState(null, true);
        }

        public void FlushState()
        {
            if (HasAuthority)
                SendState(null, true);
        }

        private void SendState(int[] recipients, bool reliable)
        {
            if (!PhotonNetwork.InRoom || pursuit == null || !HasAuthority || authorityEpoch == double.MinValue)
                return;

            MonsterPursuitState state = pursuit.PursuitState;
            int revision = ++outgoingStateRevision;
            PhotonNetwork.RaiseEvent(
                StateEvent,
                new object[]
                {
                    (int)sector,
                    monsterId,
                    ProtocolVersion,
                    authorityEpoch,
                    revision,
                    PhotonNetwork.Time,
                    transform.position,
                    transform.rotation,
                    state.IsPursuing,
                    state.TargetActorNumber,
                },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others, TargetActors = recipients },
                new SendOptions { Reliability = reliable });
        }

        public void OnEvent(EventData photonEvent)
        {
            if (!PhotonNetwork.InRoom || pursuit == null)
                return;

            RefreshRoom();
            if (photonEvent.Code != StateEvent && photonEvent.Code != RequestEvent)
                return;
            if (!(photonEvent.CustomData is object[] data) || data.Length < 2 ||
                !(data[0] is int eventSector) || eventSector != (int)sector ||
                !(data[1] is int id) || id != monsterId)
                return;

            int owner = SectorPresence.ElectController(PhotonNetwork.PlayerList, sector);
            ApplyControllerElection(owner);

            if (photonEvent.Code == RequestEvent)
            {
                Player sender = PhotonNetwork.CurrentRoom.GetPlayer(photonEvent.Sender);
                if (data.Length == 2 && SectorPresence.Get(sender) == sector &&
                    PhotonNetwork.LocalPlayer != null && owner == PhotonNetwork.LocalPlayer.ActorNumber && owner != 0)
                    SendState(new[] { photonEvent.Sender }, true);
                return;
            }

            if (owner == 0 || photonEvent.Sender != owner || data.Length != 10 ||
                !(data[2] is int version) || version != ProtocolVersion ||
                !(data[3] is double incomingAuthorityEpoch) ||
                !(data[4] is int revision) || revision <= 0 ||
                !(data[5] is double time) || !(data[6] is Vector3 position) ||
                !(data[7] is Quaternion rotation) || !(data[8] is bool pursuing) ||
                !(data[9] is int targetActorNumber))
                return;
            if (double.IsNaN(incomingAuthorityEpoch) || double.IsInfinity(incomingAuthorityEpoch) ||
                targetActorNumber < 0 || (pursuing && targetActorNumber == 0) ||
                double.IsNaN(time) || double.IsInfinity(time) ||
                !Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
                !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w) ||
                Quaternion.Dot(rotation, rotation) < 0.0001f)
                return;

            if (retiredAuthorityEpochFloor.TryGetValue(owner, out double retiredFloor) &&
                incomingAuthorityEpoch <= retiredFloor)
                return;

            if (acceptedAuthorityEpoch == double.MinValue || incomingAuthorityEpoch > acceptedAuthorityEpoch)
            {
                acceptedAuthorityEpoch = incomingAuthorityEpoch;
                lastReceivedStateRevision = -1;
            }
            else if (incomingAuthorityEpoch < acceptedAuthorityEpoch)
            {
                return;
            }

            if (revision <= lastReceivedStateRevision)
                return;

            lastReceivedStateRevision = revision;
            lastReceiveUnscaled = Time.unscaledTime;
            targetPosition = position;
            targetRotation = rotation;
            if (!hasState)
                transform.SetPositionAndRotation(position, rotation);
            hasState = true;
            pursuit.ApplyRemotePursuit(pursuing, pursuing ? targetActorNumber : 0);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
