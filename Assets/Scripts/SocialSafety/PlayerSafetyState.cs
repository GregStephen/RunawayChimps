using System;
using System.Collections.Generic;
using System.Text;

namespace RunawayChimps.SocialSafety
{
    public enum PlayerReportReason { Harassment, HateSpeech, InappropriateName, Cheating, Other }

    // Pure state shared by the Unity service and managed regression harness.
    public sealed class PlayerSafetyState
    {
        private readonly HashSet<int> mutedActors = new HashSet<int>();
        private readonly HashSet<string> sentTargets = new HashSet<string>(StringComparer.Ordinal);
        private bool awaitingLateConfirmation;
        public int RoomGeneration { get; private set; }
        public int RequestGeneration { get; private set; }
        public bool IsSending { get; private set; }
        public double RetryAt { get; private set; }

        public void ChangeRoom()
        {
            RoomGeneration++;
            mutedActors.Clear();
            sentTargets.Clear();
            InvalidateRequest();
        }

        public bool IsMuted(int actor) => mutedActors.Contains(actor);
        public bool SetMuted(int actor, int localActor, bool muted)
        {
            if (actor <= 0 || actor == localActor) return false;
            if (muted) mutedActors.Add(actor);
            else mutedActors.Remove(actor);
            return true;
        }

        public bool WasReported(string id) => id != null && sentTargets.Contains(id);
        public int BeginReport(string id, int room, double now)
        {
            if (room != RoomGeneration || IsSending || now < RetryAt || !ValidAccountId(id) || WasReported(id)) return 0;
            IsSending = true;
            awaitingLateConfirmation = false;
            RetryAt = now + 3;
            return ++RequestGeneration;
        }

        public bool CompleteReport(int request, int room, string id, bool accepted)
        {
            if (request != RequestGeneration || room != RoomGeneration) return false;
            if (!IsSending && !(awaitingLateConfirmation && accepted)) return false;
            IsSending = false;
            awaitingLateConfirmation = false;
            if (accepted) sentTargets.Add(id);
            return true;
        }

        // The UI deadline does not cancel PlayFab's request. A late positive receipt
        // is still useful until a new request or room/account change supersedes it.
        public bool ExpireReport(int request, int room)
        {
            if (!IsSending || request != RequestGeneration || room != RoomGeneration) return false;
            IsSending = false;
            awaitingLateConfirmation = true;
            return true;
        }

        public void InvalidateRequest() { IsSending = false; awaitingLateConfirmation = false; RequestGeneration++; }
        public static bool ShouldMute(bool originalMute, bool sameSector, bool playerMuted)
            => originalMute || !sameSector || playerMuted;

        public static bool ValidAccountId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 32) return false;
            foreach (char c in id)
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        public static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unnamed chimp";
            var output = new StringBuilder(24);
            foreach (char c in value)
            {
                if (char.IsControl(c) || char.IsSurrogate(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) continue;
                // All display labels also disable rich text; strip markup delimiters for readable names.
                if (c == '<' || c == '>') continue;
                output.Append(c);
                if (output.Length == 24) break;
            }
            return output.Length == 0 ? "Unnamed chimp" : output.ToString();
        }

        public static string ReasonLabel(PlayerReportReason reason)
        {
            switch (reason)
            {
                case PlayerReportReason.Harassment: return "Harassment / bullying";
                case PlayerReportReason.HateSpeech: return "Hate speech";
                case PlayerReportReason.InappropriateName: return "Inappropriate name";
                case PlayerReportReason.Cheating: return "Cheating / exploiting";
                default: return "Other misconduct";
            }
        }
    }
}
