using System;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using RunawayChimps.Travel;

namespace RunawayChimps.SocialSafety
{
    public sealed class PlayerBoard : MonoBehaviour
    {
        [Serializable]
        public sealed class Row
        {
            public GameObject root;
            public TMP_Text nameText;
            public TMP_Text detailText;
            public PlayerBoardButton mute;
            public PlayerBoardButton report;
            [NonSerialized] public Player player;
        }
        public TMP_Text heading;
        public TMP_Text status;
        public TMP_Text pageText;
        public TMP_Text reportHeading;
        public TMP_Text reportHelp;
        public GameObject rosterPanel;
        public GameObject reportPanel;
        public Row[] rows;
        public PlayerBoardButton[] reasons;
        public PlayerBoardButton submit;
        public PlayerBoardButton previous;
        public PlayerBoardButton next;
        public PlayerBoardButton close;
        public bool portable;
        private int page;
        private int generation = -1;
        private float nextRefresh;
        private PlayerSafetyService service;
        private PlayerSafetyService.ReportTarget target;
        private PlayerReportReason? reason;

        private void Awake()
        {
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.richText = false;
        }
        private void OnEnable() { nextRefresh = 0; }
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f;
            Refresh();
        }
        private void Refresh()
        {
            service = PlayerSafetyService.Instance;
            if (service == null) return;
            if (generation != service.State.RoomGeneration)
            {
                generation = service.State.RoomGeneration;
                target = null; reason = null; page = 0;
            }
            rosterPanel.SetActive(target == null);
            reportPanel.SetActive(target != null);
            close.gameObject.SetActive(portable);
            status.text = service.Status;
            heading.text = PhotonNetwork.InRoom ? "PLAYERS  /  " + PhotonNetwork.CurrentRoom.PlayerCount + " IN ROOM" : "PLAYERS  /  OFFLINE";
            if (target != null)
            {
                reportHeading.text = "REPORT " + target.Name + "  #" + target.Actor;
                bool validId = PlayerSafetyState.ValidAccountId(target.AccountId);
                reportHelp.text = validId ? "Choose a reason, then SEND REPORT.\nSends account IDs, reason and session context to game support. No audio." :
                    "This player's reporting identity is unavailable.\nYou can still mute them from the player list.";
                for (int i = 0; i < reasons.Length; i++)
                {
                    reasons[i].label.text = (reason == (PlayerReportReason)i ? "[X] " : "[ ] ") + PlayerSafetyState.ReasonLabel((PlayerReportReason)i);
                    reasons[i].SetAvailable(!service.State.IsSending && validId);
                }
                submit.SetAvailable(reason.HasValue && validId && !service.State.IsSending && !service.State.WasReported(target.AccountId));
                submit.label.text = service.State.IsSending ? "SENDING..." : "SEND REPORT";
                return;
            }
            Player[] players = PhotonNetwork.InRoom ? PhotonNetwork.PlayerList : new Player[0];
            Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
            int pages = Mathf.Max(1, (players.Length + rows.Length - 1) / rows.Length);
            page = Mathf.Clamp(page, 0, pages - 1);
            pageText.text = "PAGE " + (page + 1) + " / " + pages;
            previous.SetAvailable(page > 0);
            next.SetAvailable(page + 1 < pages);
            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                int at = page * rows.Length + i;
                Player player = at < players.Length ? players[at] : null;
                // Keep each button bound to its current Player object, never a nickname.
                row.player = player;
                row.root.SetActive(player != null);
                if (player == null) continue;
                row.nameText.text = "#" + player.ActorNumber + "  " + PlayerSafetyState.SafeName(player.NickName) + (player.IsLocal ? " (YOU)" : "");
                row.detailText.text = player.IsInactive ? "RECONNECTING" : SectorLabel(SectorPresence.Get(player));
                row.mute.label.text = player.IsLocal ? "YOU" : PlayerSafetyService.IsMuted(player) ? "UNMUTE" : "MUTE";
                row.mute.SetAvailable(!player.IsLocal);
                row.report.SetAvailable(!player.IsLocal && !service.State.IsSending);
            }
        }

        public void Press(PlayerBoardAction action, int index)
        {
            if (service == null) return;
            switch (action)
            {
                case PlayerBoardAction.Mute:
                    if (target == null && index >= 0 && index < rows.Length) service.ToggleMute(rows[index].player);
                    break;
                case PlayerBoardAction.Report:
                    if (target == null && index >= 0 && index < rows.Length && !service.State.IsSending)
                    { target = service.CaptureTarget(rows[index].player); reason = null; }
                    break;
                case PlayerBoardAction.Previous: page--; break;
                case PlayerBoardAction.Next: page++; break;
                case PlayerBoardAction.Reason:
                    if (target != null && !service.State.IsSending && index >= 0 && index < reasons.Length) reason = (PlayerReportReason)index;
                    break;
                case PlayerBoardAction.Submit:
                    if (target != null && reason.HasValue) service.Submit(target, reason.Value);
                    break;
                case PlayerBoardAction.Cancel: target = null; reason = null; break;
                case PlayerBoardAction.Close:
                    if (portable) gameObject.SetActive(false);
                    break;
            }
            Refresh();
        }
        private static string SectorLabel(SectorId sector)
        {
            switch (sector)
            {
                case SectorId.Hub: return "SECURITY";
                case SectorId.Containment: return "CONTAINMENT";
                case SectorId.Conditioning: return "CONDITIONING";
                default: return "TRAVELLING";
            }
        }
    }
}
