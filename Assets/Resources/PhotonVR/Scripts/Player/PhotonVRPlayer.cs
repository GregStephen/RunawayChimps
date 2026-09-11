using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

using TMPro;

namespace Photon.VR.Player
{
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

            // Keep each tracked point independent. A temporarily missing controller must not
            // freeze the head and the other hand at their prefab/T-pose locations.
            bool copiedAny = false;
            copiedAny |= CopyTrackedPose(Head, manager.Head);
            copiedAny |= CopyTrackedPose(RightHand, manager.RightHand);
            copiedAny |= CopyTrackedPose(LeftHand, manager.LeftHand);

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
