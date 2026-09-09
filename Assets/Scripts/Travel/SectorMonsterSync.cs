using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace RunawayChimps.Travel
{
    // Explicit messages avoid scene PhotonViews/RPCs that are absent on Hub clients.
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(MonsterNavigation))]
    public sealed class SectorMonsterSync : MonoBehaviour, IOnEventCallback
    {
        public const byte StateEvent = 180;
        public const byte RequestEvent = 181;
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
        private float nextSend;
        private float nextRequest;

        private void Awake() => navigation = GetComponent<MonsterNavigation>();
        private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            HasAuthority = false;
        }

        private void Update()
        {
            int elected = PhotonNetwork.InRoom ? SectorPresence.ElectController(PhotonNetwork.PlayerList, sector) : 0;
            if (elected != controller)
            {
                controller = elected;
                bool wasAuthority = HasAuthority;
                HasAuthority = elected != 0 && elected == PhotonNetwork.LocalPlayer.ActorNumber;
                if (HasAuthority && !wasAuthority && hasState)
                    transform.SetPositionAndRotation(targetPosition, targetRotation);
                lastStateTime = double.MinValue;
                nextRequest = nextSend = 0;
            }
            else HasAuthority = elected != 0 && elected == PhotonNetwork.LocalPlayer.ActorNumber;

            if (HasAuthority)
            {
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
                    PhotonNetwork.RaiseEvent(RequestEvent, new object[] { (int)sector, monsterId },
                        new RaiseEventOptions { TargetActors = new[] { controller } }, SendOptions.SendReliable);
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

        public void FlushState()
        {
            if (HasAuthority) SendState(null, true);
        }

        private void SendState(int[] recipients, bool reliable)
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.RaiseEvent(StateEvent,
                new object[] { (int)sector, monsterId, PhotonNetwork.Time, transform.position, transform.rotation, navigation.IsChasing },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others, TargetActors = recipients },
                new SendOptions { Reliability = reliable });
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != StateEvent && photonEvent.Code != RequestEvent) return;
            if (!(photonEvent.CustomData is object[] data) || data.Length < 2 ||
                !(data[0] is int eventSector) || eventSector != (int)sector ||
                !(data[1] is int id) || id != monsterId) return;
            int owner = SectorPresence.ElectController(PhotonNetwork.PlayerList, sector);
            if (photonEvent.Code == RequestEvent)
            {
                if (owner == PhotonNetwork.LocalPlayer.ActorNumber && owner != 0)
                    SendState(new[] { photonEvent.Sender }, true);
                return;
            }
            if (owner == 0 || photonEvent.Sender != owner || data.Length != 6 ||
                !(data[2] is double time) || !(data[3] is Vector3 position) ||
                !(data[4] is Quaternion rotation) || !(data[5] is bool chasing)) return;
            if (controller == owner && time <= lastStateTime) return;
            lastStateTime = time;
            targetPosition = position;
            targetRotation = rotation;
            if (!hasState) transform.SetPositionAndRotation(position, rotation);
            hasState = true;
            navigation.ApplyRemoteChasing(chasing);
        }
    }
}
