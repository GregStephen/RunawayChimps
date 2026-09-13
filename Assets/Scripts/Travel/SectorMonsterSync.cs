using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace RunawayChimps.Travel
{
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(MonsterNavigation))]
    public sealed class SectorMonsterSync : MonoBehaviour, IOnEventCallback
    {
        public const byte StateEvent = 180;
        public const byte RequestEvent = 181;
        private const int ProtocolVersion = 2;
        public SectorId sector = SectorId.Containment;
        public int monsterId = 1;
        public float updatesPerSecond = 10;
        public bool HasAuthority { get; private set; }
        private MonsterNavigation navigation;
        private int controller;
        private bool hasState;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private double lastStateTime = double.MinValue;
        private float nextSend, nextRequest;
        private Room observedRoom;
        private bool lastSentChasing;
        private int lastSentTarget;

        private void RefreshRoom()
        {
            var room = PhotonNetwork.CurrentRoom;
            if (ReferenceEquals(room, observedRoom)) return;
            observedRoom = room; controller = 0; hasState = HasAuthority = false;
            lastStateTime = double.MinValue; nextRequest = nextSend = 0;
            lastSentChasing = false; lastSentTarget = 0;
            navigation.ApplyRemotePursuit(false, 0);
        }

        private void Awake() => navigation = GetComponent<MonsterNavigation>();
        private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
        private void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); HasAuthority = false; navigation?.ApplyRemotePursuit(false, 0); }

        private void Update()
        {
            RefreshRoom();
            int elected = PhotonNetwork.InRoom ? SectorPresence.ElectController(PhotonNetwork.PlayerList, sector) : 0;
            if (elected != controller)
            {
                controller = elected;
                bool wasAuthority = HasAuthority;
                HasAuthority = elected != 0 && elected == PhotonNetwork.LocalPlayer.ActorNumber;
                if (HasAuthority && !wasAuthority && hasState) transform.SetPositionAndRotation(targetPosition, targetRotation);
                lastStateTime = double.MinValue; nextRequest = nextSend = 0;
                if (!HasAuthority) navigation.ApplyRemotePursuit(false, 0);
            }
            else HasAuthority = elected != 0 && elected == PhotonNetwork.LocalPlayer.ActorNumber;

            if (HasAuthority)
            {
                bool pursuitChanged = navigation.IsChasing != lastSentChasing || navigation.TargetActorNumber != lastSentTarget;
                if (pursuitChanged) SendState(null, true);
                if (Time.unscaledTime >= nextSend)
                {
                    SendState(null, false);
                    nextSend = Time.unscaledTime + 1f / Mathf.Max(1, updatesPerSecond);
                }
            }
            else
            {
                if (controller != 0 && Time.unscaledTime >= nextRequest)
                {
                    PhotonNetwork.RaiseEvent(RequestEvent, new object[] { (int)sector, monsterId }, new RaiseEventOptions { TargetActors = new[] { controller } }, SendOptions.SendReliable);
                    nextRequest = Time.unscaledTime + (hasState ? 3 : 0.5f);
                }
                if (hasState)
                {
                    float blend = 1 - Mathf.Exp(-15 * Time.unscaledDeltaTime);
                    transform.position = Vector3.Lerp(transform.position, targetPosition, blend);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
                }
            }
        }

        public void FlushState() { if (HasAuthority) SendState(null, true); }

        private void SendState(int[] recipients, bool reliable)
        {
            if (!PhotonNetwork.InRoom) return;
            lastSentChasing = navigation.IsChasing;
            lastSentTarget = navigation.TargetActorNumber;
            PhotonNetwork.RaiseEvent(StateEvent,
                new object[] { (int)sector, monsterId, ProtocolVersion, PhotonNetwork.Time, transform.position, transform.rotation, navigation.IsChasing, navigation.TargetActorNumber },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others, TargetActors = recipients }, new SendOptions { Reliability = reliable });
        }

        public void OnEvent(EventData photonEvent)
        {
            if (!PhotonNetwork.InRoom) return;
            RefreshRoom();
            if (photonEvent.Code != StateEvent && photonEvent.Code != RequestEvent) return;
            if (!(photonEvent.CustomData is object[] data) || data.Length < 2 || !(data[0] is int eventSector) || eventSector != (int)sector || !(data[1] is int id) || id != monsterId) return;
            int owner = SectorPresence.ElectController(PhotonNetwork.PlayerList, sector);
            if (photonEvent.Code == RequestEvent)
            {
                var sender = PhotonNetwork.CurrentRoom.GetPlayer(photonEvent.Sender);
                if (data.Length == 2 && SectorPresence.Get(sender) == sector && owner == PhotonNetwork.LocalPlayer.ActorNumber && owner != 0) SendState(new[] { photonEvent.Sender }, true);
                return;
            }
            if (owner == 0 || photonEvent.Sender != owner || data.Length != 8 || !(data[2] is int version) || version != ProtocolVersion ||
                !(data[3] is double time) || !(data[4] is Vector3 position) || !(data[5] is Quaternion rotation) || !(data[6] is bool chasing) || !(data[7] is int targetActor)) return;
            if (targetActor < 0 || (chasing && targetActor == 0) || double.IsNaN(time) || double.IsInfinity(time) || !Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
                !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w) || Quaternion.Dot(rotation, rotation) < 0.0001f) return;
            if (controller == owner && time <= lastStateTime) return;
            lastStateTime = time; targetPosition = position; targetRotation = rotation;
            if (!hasState) transform.SetPositionAndRotation(position, rotation);
            hasState = true;
            navigation.ApplyRemotePursuit(chasing, chasing ? targetActor : 0);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
