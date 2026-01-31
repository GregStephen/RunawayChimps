using System;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;

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

        private void Awake()
        {
            if (photonView.IsMine)
            {
                PhotonVRManager.Manager.LocalPlayer = this;
                if (HideLocalPlayer)
                {
                    Head.gameObject.SetActive(false);
                    Body.gameObject.SetActive(false);
                    RightHand.gameObject.SetActive(false);
                    LeftHand.gameObject.SetActive(false);
                    NameText.gameObject.SetActive(false);
                }
            }

            // It will delete automatically when you leave the room
            DontDestroyOnLoad(gameObject);

            _RefreshPlayerValues();
        }

        private void Update()
        {
            if (photonView.IsMine)
            {
                Head.transform.position = PhotonVRManager.Manager.Head.transform.position;
                Head.transform.rotation = PhotonVRManager.Manager.Head.transform.rotation;

                RightHand.transform.position = PhotonVRManager.Manager.RightHand.transform.position;
                RightHand.transform.rotation = PhotonVRManager.Manager.RightHand.transform.rotation;

                LeftHand.transform.position = PhotonVRManager.Manager.LeftHand.transform.position;
                LeftHand.transform.rotation = PhotonVRManager.Manager.LeftHand.transform.rotation;
            }
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
            // Name
            if (NameText != null)
            {
                if (photonView.Owner.CustomProperties != null &&
                    photonView.Owner.CustomProperties.TryGetValue("DisplayName", out object dn) &&
                    dn is string s && !string.IsNullOrEmpty(s))
                {
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
                var c = JsonUtility.FromJson<Color>(colJson);
                foreach (MeshRenderer renderer in ColourObjects)
                    if (renderer != null)
                        renderer.material.color = c;
            }

            // Cosmetics - it's a little ugly to look at
            //Dictionary<string, string> cosmetics = (Dictionary<string, string>)photonView.Owner.CustomProperties["Cosmetics"];
            //foreach (KeyValuePair<string, string> cosmetic in cosmetics)
            //{
            //    Debug.Log(cosmetic.Key);
            //    foreach (CosmeticSlot slot in CosmeticSlots)
            //    {
            //        if (slot.SlotName == cosmetic.Key)
            //        {
            //            foreach (Transform cos in slot.Object)
            //                if (cos.name != cosmetic.Value)
            //                    cos.gameObject.SetActive(false);
            //                else
            //                    cos.gameObject.SetActive(true);
            //        }
            //    }
            //}
            // Cosmetics
            if (photonView.Owner.CustomProperties != null &&
                photonView.Owner.CustomProperties.TryGetValue("Cosmetics", out object cosObj) &&
                cosObj != null)
            {
                // Photon often gives you an ExitGames.Client.Photon.Hashtable here, not Dictionary<string,string>
                if (cosObj is ExitGames.Client.Photon.Hashtable ht)
                {
                    foreach (System.Collections.DictionaryEntry entry in ht)
                    {
                        string key = entry.Key as string;
                        string value = entry.Value as string;
                        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) continue;

                        ApplyCosmetic(key, value);
                    }
                }
                else if (cosObj is Dictionary<string, string> dict)
                {
                    foreach (var kv in dict)
                    {
                        if (string.IsNullOrEmpty(kv.Key) || string.IsNullOrEmpty(kv.Value)) continue;
                        ApplyCosmetic(kv.Key, kv.Value);
                    }
                }
                else
                {
                    Debug.LogWarning($"[PhotonVRPlayer] Cosmetics property has unexpected type: {cosObj.GetType().FullName}");
                }
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