using System;
using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;
using Photon.Realtime;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace RunawayChimps.Zones
{
    public sealed class ZoneStateService : MonoBehaviour
    {
        public static ZoneStateService Instance { get; private set; }

        public const string ZonePropKey = "zone";
        public bool debugLogs = false;

        public event Action<ZoneId> OnLocalZoneChanged;
        public event Action<int, ZoneId> OnRemoteZoneChanged;
        public event Action<int> OnPlayerLeft; // actorNumber

        private readonly Dictionary<int, ZoneId> _zonesByActor = new();

        public ZoneId LocalZone { get; private set; } = ZoneId.None;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // -------- PUBLIC API --------

        public void SetLocalZone(ZoneId zone)
        {
            if (!PhotonNetwork.InRoom)
            {
                if (debugLogs) Debug.Log($"[ZoneStateService] Ignoring zone {zone} outside a room.");
                return;
            }

            if (debugLogs) Debug.Log($"[ZoneStateService] Setting local zone to {zone}");
            ApplyZone(PhotonNetwork.LocalPlayer.ActorNumber, zone, true);

            var props = new PhotonHashtable
            {
                { ZonePropKey, (int)zone }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        public bool TryGetZone(int actorNumber, out ZoneId zone)
            => _zonesByActor.TryGetValue(actorNumber, out zone);

        // -------- HOOKS (CALL THESE FROM YOUR EXISTING PHOTON CALLBACKS) --------

        public void HandleJoinedRoom()
        {
            _zonesByActor.Clear();
            // Default zone if not set
            var current = GetZoneFromProps(PhotonNetwork.LocalPlayer.CustomProperties);
            if (current == ZoneId.None)
            {
                SetLocalZone(ZoneId.Hub);
            }

            RefreshAllPlayers();
        }

        public void HandlePlayerEntered(Player player)
        {
            var zone = GetZoneFromProps(player.CustomProperties);
            if (zone != ZoneId.None)
                ApplyZone(player.ActorNumber, zone, false);
        }

        public void HandlePlayerLeft(Player player)
        {
            _zonesByActor.Remove(player.ActorNumber);
            OnPlayerLeft?.Invoke(player.ActorNumber);
        }

        public void HandlePlayerPropertiesUpdated(Player player, PhotonHashtable changedProps)
        {
            if (changedProps != null && changedProps.ContainsKey(ZonePropKey))
            {
                var zone = GetZoneFromProps(player.CustomProperties);
                ApplyZone(player.ActorNumber, zone, player.IsLocal);
            }
        }

        public void ResetSession()
        {
            _zonesByActor.Clear();
            LocalZone = ZoneId.None;
            OnLocalZoneChanged?.Invoke(ZoneId.None);
        }

        // -------- INTERNAL --------

        private void RefreshAllPlayers()
        {
            foreach (var p in PhotonNetwork.PlayerList)
            {
                var zone = GetZoneFromProps(p.CustomProperties);
                if (zone != ZoneId.None)
                    ApplyZone(p.ActorNumber, zone, p.IsLocal);
            }
        }

        private void ApplyZone(int actorNumber, ZoneId zone, bool isLocal)
        {
            _zonesByActor[actorNumber] = zone;

            if (isLocal)
            {
                if (LocalZone == zone) return;
                LocalZone = zone;
                OnLocalZoneChanged?.Invoke(zone);
            }
            else
            {
                OnRemoteZoneChanged?.Invoke(actorNumber, zone);
            }
        }

        private static ZoneId GetZoneFromProps(PhotonHashtable props)
        {
            if (props == null || !props.ContainsKey(ZonePropKey))
                return ZoneId.None;

            try { return (ZoneId)Convert.ToInt32(props[ZonePropKey]); }
            catch { return ZoneId.None; }
        }
    }
}
