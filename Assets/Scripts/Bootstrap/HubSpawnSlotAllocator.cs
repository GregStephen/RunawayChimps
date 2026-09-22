using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.Travel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunawayChimps.Multiplayer
{
    public sealed class HubSpawnSlotAllocator : MonoBehaviourPunCallbacks
    {
        public const int SlotCount = 10;
        public const string PlayerSlotProperty = "rcHubSpawnSlot";
        private const string RoomSlotPrefix = "rcHubSlot";
        private const string LayoutResourcePath = "HubSpawn/HubSpawnSlots";
        private const float ClaimRetrySeconds = 0.35f;
        private const float PendingClaimTimeoutSeconds = 0.8f;

        private int localSlot = -1;
        private int pendingSlot = -1;
        private float pendingSince;
        private float nextClaimAt;
        private GameObject slotLayoutPrefab;
        private bool layoutErrorLogged;

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
            if (hubSpawn == null || !TryGetLocalSlot(out int slot) || !TryGetAuthoredMarker(slot, out Transform marker))
                return false;

            position = hubSpawn.TransformPoint(marker.localPosition);
            Quaternion authoredRotation = hubSpawn.rotation * marker.localRotation;
            rotation = Quaternion.Euler(0f, authoredRotation.eulerAngles.y, 0f);
            return true;
        }

        public bool TryGetLocalSlot(out int slot)
        {
            slot = -1;
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.LocalPlayer == null)
                return false;

            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            if (HasLocalSlot)
            {
                if (ReadOwner(localSlot) == actorNumber)
                {
                    slot = localSlot;
                    return true;
                }

                // The room changed or ownership was reconciled underneath us. Do not leave a
                // stale +Infinity retry gate behind; recover or claim again on demand.
                localSlot = -1;
                nextClaimAt = 0f;
            }

            if (TryFindOwnedSlot(actorNumber, out int recoveredSlot))
            {
                FinalizeLocalClaim(recoveredSlot);
                slot = localSlot;
                return true;
            }

            if (pendingSlot >= 0)
            {
                int owner = ReadOwner(pendingSlot);
                if (owner == actorNumber)
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

        public override void OnJoinedRoom()
        {
            base.OnJoinedRoom();
            ResetLocalClaim();
            EnsureRoomSlotsInitialized();
            ClearPersistedPlayerSlot();
            nextClaimAt = 0f;
            RefreshHubPlacementForNewRoom();
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

        private bool TryGetAuthoredMarker(int slot, out Transform marker)
        {
            marker = null;
            if (slot < 0 || slot >= SlotCount) return false;
            if (slotLayoutPrefab == null)
                slotLayoutPrefab = Resources.Load<GameObject>(LayoutResourcePath);
            if (slotLayoutPrefab == null || slotLayoutPrefab.transform.childCount != SlotCount)
            {
                LogLayoutError("Hub spawn slot layout must contain exactly 10 authored markers.");
                return false;
            }

            marker = slotLayoutPrefab.transform.GetChild(slot);
            string expectedName = $"HubSpawnSlot_{slot + 1:00}";
            if (marker == null || marker.name != expectedName)
            {
                LogLayoutError($"Hub spawn marker {slot} must be named '{expectedName}'.");
                marker = null;
                return false;
            }
            return true;
        }

        private void LogLayoutError(string message)
        {
            if (layoutErrorLogged) return;
            layoutErrorLogged = true;
            Debug.LogError("[HubSpawnSlotAllocator] " + message, this);
        }

        private void RefreshHubPlacementForNewRoom()
        {
            var snapper = GetComponent<RigSpawnSnapper>();
            if (snapper == null) return;
            Scene hub = SceneManager.GetSceneByName(snapper.HubSceneName);
            if (!hub.isLoaded || (SectorTravelService.I != null && SectorTravelService.I.IsBusy)) return;

            AppState.I?.ResetHubPlacementReady();
            snapper.RetrySnap();
        }

        private void ClearPersistedPlayerSlot()
        {
            if (PhotonNetwork.LocalPlayer == null) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [PlayerSlotProperty] = -1 });
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

        private bool TryFindOwnedSlot(int actorNumber, out int slot)
        {
            slot = -1;
            if (actorNumber <= 0) return false;
            for (int i = 0; i < SlotCount; i++)
            {
                if (ReadOwner(i) != actorNumber) continue;
                slot = i;
                return true;
            }
            return false;
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
