using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

using TMPro;

namespace Photon.VR.Player
{
    // Gorilla locomotion updates its collision-safe hand followers during the normal Update
    // phase. Run the local visual/network pose copy afterwards so the rendered hand position
    // reflects the contact-resolved hand instead of the raw controller pose inside geometry.
    [DefaultExecutionOrder(100)]
    public class PhotonVRPlayer : MonoBehaviourPunCallbacks
    {
        [Header("Objects")]
        public Transform Head;
        public Transform Body;
        public Transform LeftHand;
        public Transform RightHand;
        [Tooltip("The objects that will get the colour of the player applied to them")]
        public List<MeshRenderer> ColourObjects;

        [Space]
        [Tooltip("Feel free to add as many slots as you feel necessary")]
        public List<CosmeticSlot> CosmeticSlots = new List<CosmeticSlot>();

        [Header("Other")]
        public TextMeshPro NameText;
        public bool HideLocalPlayer = true;

        private bool warnedMissingTrackingRig;

        private void Awake()
        {
            if (photonView.IsMine)
            {
                if (PhotonVRManager.Manager != null) PhotonVRManager.Manager.LocalPlayer = this;
                Debug.Log($"[PhotonVRPlayer] Awake (IsMine). Head={(Head!=null?Head.name:"null")}, Body={(Body!=null?Body.name:"null")}, HideLocalPlayer={HideLocalPlayer}");
                // Do NOT notify PhotonVRManager with this prefab's visual head transform —
                // the manager should be bound to the actual tracking rig (via PhotonVRRigBinder).
                if (HideLocalPlayer)
                {
                    if (Head != null) Head.gameObject.SetActive(false);
                    if (Body != null) Body.gameObject.SetActive(false);
                    if (RightHand != null) RightHand.gameObject.SetActive(false);
                    if (LeftHand != null) LeftHand.gameObject.SetActive(false);
                    if (NameText != null) NameText.gameObject.SetActive(false);
                }
                // Debug visual state now and next frame (in case something else toggles it on Start)
            }

            // It will delete automatically when you leave the room
            DontDestroyOnLoad(gameObject);
            if (GetComponent<RunawayChimps.Travel.SectorAvatarVisibility>() == null)
                gameObject.AddComponent<RunawayChimps.Travel.SectorAvatarVisibility>();

            _RefreshPlayerValues();
        }

        private void Update()
        {
            if (!photonView.IsMine) return;

            var manager = PhotonVRManager.Manager;
            if (manager == null)
            {
                WarnMissingTrackingRig("PhotonVRManager");
                return;
            }

            // The raw XR controller transforms can physically be below a floor while Gorilla
            // locomotion correctly clamps its separate hand followers to a collision-safe
            // contact. The local Photon avatar is visible in first person in this project, so
            // copying raw controller *positions* makes its fingertips visibly pass through the
            // floor even though locomotion itself is no longer buried. Use the safe follower
            // for position and preserve the real tracked controller rotation for wrist/hand aim.
            GorillaLocomotion.Player locomotion = GorillaLocomotion.Player.Instance;

            bool copiedAny = false;
            copiedAny |= CopyTrackedPose(Head, manager.Head);
            copiedAny |= CopyTrackedHandPose(
                RightHand,
                manager.RightHand,
                locomotion != null ? locomotion.rightHandFollower : null);
            copiedAny |= CopyTrackedHandPose(
                LeftHand,
                manager.LeftHand,
                locomotion != null ? locomotion.leftHandFollower : null);

            if (copiedAny)
            {
                warnedMissingTrackingRig = false;
            }
            else
            {
                WarnMissingTrackingRig("Head/LeftHand/RightHand tracking transforms");
            }
        }

        private static bool CopyTrackedPose(Transform target, Transform source)
        {
            if (target == null || source == null) return false;
            target.SetPositionAndRotation(source.position, source.rotation);
            return true;
        }

        private static bool CopyTrackedHandPose(
            Transform target,
            Transform trackedRotationSource,
            Transform collisionSafePositionSource)
        {
            if (target == null || trackedRotationSource == null)
                return false;

            Vector3 position = collisionSafePositionSource != null
                ? collisionSafePositionSource.position
                : trackedRotationSource.position;

            target.SetPositionAndRotation(position, trackedRotationSource.rotation);
            return true;
        }

        private void WarnMissingTrackingRig(string missing)
        {
            if (warnedMissingTrackingRig) return;
            warnedMissingTrackingRig = true;
            Debug.LogWarning($"[PhotonVRPlayer] Local avatar is waiting for {missing}. PhotonVRRigBinder should restore the tracking references.", this);
        }

        public void RefreshPlayerValues() => photonView.RPC("RPCRefreshPlayerValues", RpcTarget.All);

        [PunRPC]
        private void RPCRefreshPlayerValues()
        {
            _RefreshPlayerValues();
        }
        public override void OnPlayerPropertiesUpdate(Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            // Only refresh when THIS avatar's owner updated something we care about
            if (targetPlayer != photonView.Owner) return;

            if (changedProps.ContainsKey("DisplayName") ||
                changedProps.ContainsKey("Colour") ||
                changedProps.ContainsKey("Cosmetics"))
            {
                _RefreshPlayerValues();
            }
        }

        private void ApplyCosmetic(string slotName, string cosmeticId)
        {
            foreach (CosmeticSlot slot in CosmeticSlots)
            {
                if (slot == null || slot.Object == null) continue;
                if (slot.SlotName != slotName) continue;

                foreach (Transform cos in slot.Object)
                {
                    if (cos == null) continue;
                    cos.gameObject.SetActive(cos.name == cosmeticId);
                }
            }
        }


        private void _RefreshPlayerValues()
        {
            if (photonView.Owner == null) return;
            // Name
            if (NameText != null)
            {
                if (photonView.Owner.CustomProperties != null &&
                    photonView.Owner.CustomProperties.TryGetValue("DisplayName", out object dn) &&
                    dn is string s && !string.IsNullOrEmpty(s))
                {
                    NameText.richText = false;
                    NameText.text = s;
                }
                else
                {
                    NameText.text = photonView.Owner.NickName;
                }
            }

            // Colour
            //foreach (MeshRenderer renderer in ColourObjects)
            //{
            //    if(renderer != null)
            //        renderer.material.color = JsonUtility.FromJson<Color>((string)photonView.Owner.CustomProperties["Colour"]);
            //}
            if (photonView.Owner.CustomProperties != null &&
                photonView.Owner.CustomProperties.TryGetValue("Colour", out object colObj) &&
                colObj is string colJson)
            {
                Color c;
                try { c = JsonUtility.FromJson<Color>(colJson); }
                catch (ArgumentException) { c = Color.white; }
                if (ColourObjects != null) foreach (MeshRenderer renderer in ColourObjects)
                    if (renderer != null)
                        renderer.material.color = c;
            }

            object cosmetics = null;
            photonView.Owner.CustomProperties?.TryGetValue("Cosmetics", out cosmetics);
            foreach (var slot in CosmeticSlots)
            {
                if (slot == null || string.IsNullOrEmpty(slot.SlotName)) continue;
                string cosmeticId = null;
                if (cosmetics is ExitGames.Client.Photon.Hashtable table &&
                    table.TryGetValue(slot.SlotName, out var value)) cosmeticId = value as string;
                else if (cosmetics is Dictionary<string, string> dictionary)
                    dictionary.TryGetValue(slot.SlotName, out cosmeticId);
                ApplyCosmetic(slot.SlotName, cosmeticId);
            }



        }

        [Serializable]
        public class CosmeticSlot
        {
            public string SlotName;
            public Transform Object;
        }
    }
}
