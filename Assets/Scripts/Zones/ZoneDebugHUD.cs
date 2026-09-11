using System.Text;
using UnityEngine;
using Photon.Pun;

namespace RunawayChimps.Zones
{
    /// <summary>
    /// Tiny on-screen debug readout to verify zone replication.
    /// Safe to remove later.
    /// </summary>
    public sealed class ZoneDebugHUD : MonoBehaviour
    {
        [Tooltip("Toggle with F3 in editor/desktop builds.")]
        public bool Visible = true;

        private GUIStyle _style;

        private void EnsureStyle()
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                richText = true
            };
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.F3))
                Visible = !Visible;
#endif
        }

        private void OnGUI()
        {
            if (!Visible) return;
            if (_style == null) EnsureStyle();

            var svc = ZoneStateService.Instance;
            if (svc == null)
            {
                GUI.Label(new Rect(10, 10, 900, 200), "<b>ZoneDebugHUD</b>\nZoneStateService not found.", _style);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("<b>ZoneDebugHUD</b>");
            sb.AppendLine($"Room: {(PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom?.Name : "(not in room)")}");
            sb.AppendLine($"LocalZone: <b>{svc.LocalZone}</b>");
            sb.AppendLine("Players:");

            if (PhotonNetwork.InRoom)
            {
                foreach (var p in PhotonNetwork.PlayerList)
                {
                    var zone = svc.TryGetZone(p.ActorNumber, out var z) ? z : ZoneId.None;
                    sb.AppendLine($"  #{p.ActorNumber}  {(p.IsLocal ? "(local)" : "")}  zone={zone}");
                }
            }

            GUI.Label(new Rect(10, 10, 900, 600), sb.ToString(), _style);
        }
    }
}
