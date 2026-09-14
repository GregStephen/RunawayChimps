using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace RunawayChimps.Multiplayer
{
    public sealed class HubSpawnSlotAllocator : MonoBehaviourPunCallbacks
    {
        public const int SlotCount = 10;
        public const string PlayerSlotProperty = "rcHubSpawnSlot";
        private const string RoomSlotPrefix = "rcHubSlot";
        private const float ClaimRetrySeconds = 0.35f;
        private const float PendingClaimTimeoutSeconds = 0.8f;

        private static readonly Vector3[] SlotOffsets =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(-1.10f, 0f, 0.55f),
            new Vector3(1.10f, 0f, 0.55f),
            new Vector3(-0.55f, 0f, 1.55f),
            new Vector3(0.55f, 0f, 1.55f),
            new Vector3(-1.65f, 0f, 1.55f),
            new Vector3(1.65f, 0f, 1.55f),
            new Vector3(-1.10f, 0f, 2.55f),
            new Vector3(0f, 0f, 2.55f),
            new Vector3(1.10f, 0f, 2.55f),
        };

        private int localSlot = -1;
        private int pendingSlot = -1;
        private float pendingSince;
        private float nextClaimAt;

        public int LocalSlot => localSlot;
        public bool HasLocalSlot => localSlot >= 0 && localSlot < SlotCount;

        public static void AddInitialRoomProperties(Hashtable properties)
        {
            if (properties == null) return;
            for (int i = 0; i < SlotCount; i++)
            {
                string key = SlotKey(i);
                if (!properties.ContainsKey(key)) properties[key] = 0;
            }
        }

        public bool TryGetLocalSpawnPose(Transform hubSpawn, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (hubSpawn == null || !TryGetLocalSlot(out int slot)) return false;
            position = hubSpawn.TransformPoint(SlotOffsets[slot]);
            rotation = Quaternion.Euler(0f, hubSpawn.eulerAngles.y, 0f);
            return true;
        }

        public bool TryGetLocalSlot(out int slot)
        {
            slot = -1;
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.LocalPlayer == null)
                return false;

            if (HasLocalSlot && ReadOwner(localSlot) == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                slot = localSlot;
                return true;
            }

            if (pendingSlot >= 0)
            {
                int owner = ReadOwner(pendingSlot);
                if (owner == PhotonNetwork.LocalPlayer.ActorNumber)
                {
                    FinalizeLocalClaim(pendingSlot);
                    slot = localSlot;
                    return true;
                }
                if (owner != 0 || Time.unscaledTime - pendingSince >= PendingClaimTimeoutSeconds)
                    pendingSlot = -1;
                else
                    return false;
            }

            if (Time.unscaledTime < nextClaimAt) return false;
            EnsureRoomSlotsInitialized();
            TryClaimFirstFreeSlot();
            return false;
        }

        private void Update()
        {
            if (PhotonNetwork.InRoom && !HasLocalSlot && Time.unscaledTime >= nextClaimAt)
                TryGetLocalSlot(out _);
        }

        public override void OnJoinedRoom()
        {
            base.OnJoinedRoom();
            ResetLocalClaim();
            EnsureRoomSlotsInitialized();
            nextClaimAt = 0f;
        }

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            base.OnRoomPropertiesUpdate(propertiesThatChanged);
            if (pendingSlot < 0 || !PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
            string key = SlotKey(pendingSlot);
            if (!propertiesThatChanged.ContainsKey(key)) return;
            int owner = ReadOwner(pendingSlot);
            if (owner == PhotonNetwork.LocalPlayer.ActorNumber)
                FinalizeLocalClaim(pendingSlot);
            else if (owner != 0)
                pendingSlot = -1;
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            base.OnPlayerLeftRoom(otherPlayer);
            if (PhotonNetwork.IsMasterClient && otherPlayer != null)
                ReleaseActorClaims(otherPlayer.ActorNumber);
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            base.OnMasterClientSwitched(newMasterClient);
            if (PhotonNetwork.IsMasterClient)
            {
                EnsureRoomSlotsInitialized();
                ReconcileOrphanedClaims();
            }
        }

        public override void OnLeftRoom()
        {
            base.OnLeftRoom();
            ResetLocalClaim();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            base.OnDisconnected(cause);
            ResetLocalClaim();
        }

        private void EnsureRoomSlotsInitialized()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || !PhotonNetwork.IsMasterClient) return;
            var missing = new Hashtable();
            for (int i = 0; i < SlotCount; i++)
            {
                string key = SlotKey(i);
                if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(key)) missing[key] = 0;
            }
            if (missing.Count > 0) PhotonNetwork.CurrentRoom.SetCustomProperties(missing);
        }

        private void TryClaimFirstFreeSlot()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.LocalPlayer == null) return;
            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            for (int i = 0; i < SlotCount; i++)
            {
                string key = SlotKey(i);
                if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(key) || ReadOwner(i) != 0) continue;
                var desired = new Hashtable { [key] = actorNumber };
                var expected = new Hashtable { [key] = 0 };
                if (PhotonNetwork.CurrentRoom.SetCustomProperties(desired, expected))
                {
                    pendingSlot = i;
                    pendingSince = Time.unscaledTime;
                    nextClaimAt = Time.unscaledTime + ClaimRetrySeconds;
                    return;
                }
            }
            if (PhotonNetwork.IsMasterClient) ReconcileOrphanedClaims();
            nextClaimAt = Time.unscaledTime + ClaimRetrySeconds;
        }

        private void FinalizeLocalClaim(int slot)
        {
            localSlot = slot;
            pendingSlot = -1;
            nextClaimAt = float.PositiveInfinity;
            if (PhotonNetwork.LocalPlayer != null)
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [PlayerSlotProperty] = slot });
            Debug.Log($"[HubSpawnSlotAllocator] Actor {PhotonNetwork.LocalPlayer?.ActorNumber} reserved Hub spawn slot {slot}.", this);
        }

        private void ReleaseActorClaims(int actorNumber)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || actorNumber <= 0) return;
            for (int i = 0; i < SlotCount; i++)
            {
                string key = SlotKey(i);
                if (ReadOwner(i) != actorNumber) continue;
                PhotonNetwork.CurrentRoom.SetCustomProperties(
                    new Hashtable { [key] = 0 },
                    new Hashtable { [key] = actorNumber });
            }
        }

        private void ReconcileOrphanedClaims()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || !PhotonNetwork.IsMasterClient) return;
            for (int i = 0; i < SlotCount; i++)
            {
                int owner = ReadOwner(i);
                if (owner <= 0 || PhotonNetwork.CurrentRoom.Players.ContainsKey(owner)) continue;
                string key = SlotKey(i);
                PhotonNetwork.CurrentRoom.SetCustomProperties(
                    new Hashtable { [key] = 0 },
                    new Hashtable { [key] = owner });
            }
        }

        private int ReadOwner(int slot)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || slot < 0 || slot >= SlotCount)
                return -1;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SlotKey(slot), out object raw))
                return -1;
            return raw is int actor ? actor : 0;
        }

        private void ResetLocalClaim()
        {
            localSlot = -1;
            pendingSlot = -1;
            pendingSince = 0f;
            nextClaimAt = 0f;
        }

        private static string SlotKey(int slot) => RoomSlotPrefix + slot;
    }
}
