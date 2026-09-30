using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.Travel;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace RunawayChimps.FacilityAnnouncements
{
    // One feature-local director per client; no scene PhotonView, master dependency,
    // global event cache, queue, rig, microphone, or general-purpose scheduling service.
    public sealed class FacilityAnnouncementDirector : MonoBehaviour, IOnEventCallback
    {
        public const byte AnnouncementEvent = 197;
        public const string ReadySectorKey = "rc.fa.sector";
        public const string ReadyTicketKey = "rc.fa.ticket";
        public const int MaxSpeakers = 16;
        private static FacilityAnnouncementDirector instance;
        private static readonly List<FacilitySpeaker> Speakers = new List<FacilitySpeaker>(MaxSpeakers);
        private readonly List<FacilityAnnouncementRules.Peer> peers = new List<FacilityAnnouncementRules.Peer>(32);
        private readonly FacilityAnnouncementRules.ReceiveGate[] gates = new FacilityAnnouncementRules.ReceiveGate[4];
        private readonly FacilityAnnouncementRules.Schedule schedule = new FacilityAnnouncementRules.Schedule();
        private readonly string[] lastReceived = new string[4];
        private readonly System.Random random = new System.Random();
        private Room room;
        private int roomActor;
        private SectorId sector;
        private string localTicket;
        private string channel;
        private int sceneHandle;
        private int serial;
        private double nextRefresh;
        private float nextRigLookup;
        private bool paused;
        private bool focused = true;
        private Camera localCamera;
        private FacilityAnnouncementCollection collection;
        private FacilityAnnouncementCaptions captions;
        private FacilityAnnouncementCaptions captionTemplate;
        private Camera captionCamera;

        private enum Stage { None, Waiting, StartCue, Speech, EndCue }
        private Stage stage;
        private FacilitySpeaker output;
        private AudioClip speech;
        private AudioClip startCue;
        private AudioClip endCue;
        private string transcript;
        private double startsAt;
        private float stageStarted;
        private float stageLength;
        private bool observedPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            Speakers.Clear();
        }

        public static void Register(FacilitySpeaker speaker)
        {
            if (!Application.isPlaying || speaker == null || Speakers.Contains(speaker)) return;
            if (Speakers.Count >= MaxSpeakers)
            {
                Debug.LogWarning("Facility announcements support at most 16 registered speakers per client; extra placement ignored.", speaker);
                return;
            }
            Speakers.Add(speaker);
            if (instance == null)
                instance = new GameObject("Facility Announcements (local director)").AddComponent<FacilityAnnouncementDirector>();
            instance.nextRefresh = 0;
        }

        public static void Unregister(FacilitySpeaker speaker)
        {
            Speakers.Remove(speaker);
            if (instance == null) return;
            if (instance.output == speaker) instance.CancelPlayback();
            instance.nextRefresh = 0;
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            for (int i = 0; i < gates.Length; ++i) gates[i] = new FacilityAnnouncementRules.ReceiveGate();
        }
        private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            Deactivate(true);
        }
        private void OnDestroy()
        {
            CancelPlayback();
            if (captions != null) Destroy(captions.gameObject);
            if (instance == this) instance = null;
        }
        private void OnApplicationPause(bool value)
        {
            paused = value;
            if (value) Deactivate(true);
            nextRefresh = 0;
        }
        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value) Deactivate(true);
            nextRefresh = 0;
        }

        private bool LocalReady()
        {
            var travel = SectorTravelService.I;
            return PhotonNetwork.InRoom && PhotonNetwork.IsMessageQueueRunning && !paused && focused &&
                Time.timeScale > 0 && !AudioListener.pause && travel != null && !travel.IsBusy &&
                (int)travel.CurrentSector >= 1 && (int)travel.CurrentSector <= 3 &&
                (AppState.I == null || AppState.I.IsReady);
        }

        private void Update()
        {
            RefreshRoom();
            // Lifecycle cancellation is per-frame, not delayed by the cheap presence poll.
            if (!LocalReady() || (sector != SectorId.None &&
                (SectorTravelService.I.CurrentSector != sector || SceneManager.GetActiveScene().handle != sceneHandle)))
            {
                Deactivate(true);
                return;
            }
            Refresh(false);
            if (sector == SectorId.None) return;
            TickPlayback();
            var gate = gates[(int)sector];
            double now = PhotonNetwork.Time;
            if (gate.Owner != roomActor || gate.OwnerTicket != localTicket || collection == null ||
                !schedule.IsDue(now) || now < gate.BusyUntil || stage != Stage.None) return;
            SendAnnouncement(now);
        }

        private void RefreshRoom()
        {
            Room nextRoom = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom : null;
            int nextActor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
            if (ReferenceEquals(room, nextRoom) && roomActor == nextActor) return;
            CancelPlayback();
            room = nextRoom;
            roomActor = nextActor;
            sector = SectorId.None;
            localTicket = channel = null;
            collection = null;
            serial = 0;
            schedule.Cancel();
            Array.Clear(lastReceived, 0, lastReceived.Length);
            foreach (var gate in gates) gate.Reset(PhotonNetwork.Time);
            // PUN can retain local custom properties across rooms. Never reuse that ticket.
            PublishReady(SectorId.None, string.Empty);
            nextRefresh = 0;
        }

        private void BindLocalCamera()
        {
            if (localCamera != null && localCamera.isActiveAndEnabled) return;
            localCamera = null;
            if (Time.unscaledTime < nextRigLookup) return;
            nextRigLookup = Time.unscaledTime + 1;
            var player = GorillaLocomotion.Player.Instance;
            if (player == null || !player.isActiveAndEnabled || player.GetComponentInParent<LocalRigMarker>() == null) return;
            var view = player.GetComponentInParent<PhotonView>();
            if (view != null && !view.IsMine) return;
            var origin = player.GetComponentInParent<XROrigin>();
            if (origin != null && origin.Camera != null && origin.Camera.isActiveAndEnabled) localCamera = origin.Camera;
        }

        private void Refresh(bool force)
        {
            RefreshRoom();
            if (!LocalReady()) { Deactivate(true); return; }
            double now = PhotonNetwork.Time;
            if (!force && now < nextRefresh) return;
            nextRefresh = now + 0.25;
            BindLocalCamera();
            var wanted = SectorTravelService.I.CurrentSector;
            var activeScene = SceneManager.GetActiveScene();
            FacilitySpeaker chosen = null;
            for (int i = Speakers.Count - 1; i >= 0; --i)
            {
                FacilitySpeaker candidate = Speakers[i];
                if (candidate == null) { Speakers.RemoveAt(i); continue; }
                if (!candidate.Ready || candidate.sector != wanted || candidate.gameObject.scene != activeScene ||
                    !candidate.collection.HasPlayableContent()) continue;
                // One collection per sector is the authoring contract. A mistaken extra
                // collection cannot create a second schedule: choose one deterministically.
                if (chosen == null || string.CompareOrdinal(candidate.collection.collectionId, chosen.collection.collectionId) < 0 ||
                    (candidate.collection.collectionId == chosen.collection.collectionId &&
                     candidate.collection.contentRevision > chosen.collection.contentRevision)) chosen = candidate;
            }
            if (chosen == null || localCamera == null) { Deactivate(true); return; }
            string wantedChannel = chosen.collection.collectionId + "/" + chosen.collection.contentRevision +
                (chosen.collection.Diagnostic ? "/diagnostic" : "/normal");
            if (sector != wanted || channel != wantedChannel || sceneHandle != activeScene.handle || localTicket == null)
            {
                Deactivate(false);
                sector = wanted;
                channel = wantedChannel;
                sceneHandle = activeScene.handle;
                localTicket = Guid.NewGuid().ToString("N");
                serial = 0;
                PublishReady(sector, localTicket);
            }
            collection = chosen.collection;
            peers.Clear();
            Player[] players = PhotonNetwork.PlayerList;
            if (players.Length > FacilityAnnouncementRules.MaxPeers) { Deactivate(true); return; }
            foreach (Player p in players)
            {
                if (p == null || p.IsInactive || !p.CustomProperties.TryGetValue(ReadySectorKey, out object s) ||
                    !(s is int readySector) || !p.CustomProperties.TryGetValue(ReadyTicketKey, out object t) ||
                    !(t is string ticket) || !FacilityAnnouncementRules.ValidTicket(ticket) ||
                    readySector != (int)SectorPresence.Get(p) || (p.IsLocal && ticket != localTicket)) continue;
                peers.Add(new FacilityAnnouncementRules.Peer(p.ActorNumber, readySector, ticket));
            }
            int owner = FacilityAnnouncementRules.Elect(peers, (int)sector);
            string ownerTicket = null;
            foreach (var peer in peers) if (peer.Actor == owner) ownerTicket = peer.Ticket;
            if (gates[(int)sector].SetAuthority(owner, ownerTicket, now))
            {
                CancelPlayback();
                // A handover never resumes or immediately replays the former announcement.
                // The hard maximum protects an old listener even during diagnostic testing.
                schedule.Arm(now, collection.Quiet(random.NextDouble()), FacilityAnnouncementRules.MaxSequenceSeconds + 1);
            }
        }

        private void PublishReady(SectorId value, string ticket)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            {
                [ReadySectorKey] = (int)value,
                [ReadyTicketKey] = ticket ?? string.Empty
            });
        }

        private void Deactivate(bool publish)
        {
            if (publish && localTicket != null) PublishReady(SectorId.None, string.Empty);
            if (sector != SectorId.None) gates[(int)sector].SetAuthority(0, null, PhotonNetwork.Time);
            localTicket = channel = null;
            sector = SectorId.None;
            collection = null;
            schedule.Cancel();
            CancelPlayback();
        }

        private string LastSelectionKey => "rc.fa.last." + (int)sector;
        private string LastSelection()
        {
            if (room != null && room.CustomProperties.TryGetValue(LastSelectionKey, out object value) && value is string text)
            {
                string prefix = collection.collectionId + "/" + collection.contentRevision + "/";
                if (text.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string id = text.Substring(prefix.Length);
                    if (FacilityAnnouncementRules.ValidId(id)) return id;
                }
            }
            return lastReceived[(int)sector];
        }

        private void SendAnnouncement(double now)
        {
            var entry = collection.Choose(LastSelection(), random.NextDouble());
            // Failed/empty selections still wait a full quiet interval; no retry spam.
            schedule.Arm(now, collection.Quiet(random.NextDouble()));
            if (entry == null || serial == int.MaxValue) return;
            var actors = new List<int>(peers.Count);
            var tickets = new List<string>(peers.Count);
            foreach (var peer in peers)
                if (peer.Sector == (int)sector) { actors.Add(peer.Actor); tickets.Add(peer.Ticket); }
            if (actors.Count == 0) return;
            double duration = collection.Duration(entry);
            if (duration > FacilityAnnouncementRules.MaxSequenceSeconds) return;
            // Only selection history is persisted, never audio, a timer or a backlog.
            room.SetCustomProperties(new Hashtable
            {
                [LastSelectionKey] = collection.collectionId + "/" + collection.contentRevision + "/" + entry.id
            });
            PhotonNetwork.RaiseEvent(AnnouncementEvent, new object[]
            {
                FacilityAnnouncementRules.Protocol, (int)sector, collection.collectionId, collection.contentRevision,
                localTicket, ++serial, now, entry.id, duration, actors.ToArray(), tickets.ToArray()
            }, new RaiseEventOptions { TargetActors = actors.ToArray(), CachingOption = EventCaching.DoNotCache }, SendOptions.SendReliable);
            // The sender also waits for the server event; no fast local duplicate path.
            schedule.Arm(now + FacilityAnnouncementRules.StartLeadSeconds + duration,
                collection.Quiet(random.NextDouble()));
        }

        public void OnEvent(EventData message)
        {
            if (message.Code != AnnouncementEvent || !isActiveAndEnabled || !LocalReady()) return;
            Refresh(true);
            if (sector == SectorId.None || collection == null || !(message.CustomData is object[] data) || data.Length != 11 ||
                !(data[0] is string protocol) || protocol != FacilityAnnouncementRules.Protocol ||
                !(data[1] is int eventSector) || eventSector != (int)sector ||
                !(data[2] is string catalog) || catalog != collection.collectionId ||
                !(data[3] is int revision) || revision != collection.contentRevision ||
                !(data[4] is string ticket) || !(data[5] is int incomingSerial) ||
                !(data[6] is double issued) || !(data[7] is string id) || !FacilityAnnouncementRules.ValidId(id) ||
                !(data[8] is double duration) || !(data[9] is int[] actors) || !(data[10] is string[] tickets) ||
                actors.Length == 0 || actors.Length > FacilityAnnouncementRules.MaxPeers || tickets.Length != actors.Length) return;
            int matches = 0;
            for (int i = 0; i < actors.Length; ++i)
                if (actors[i] == roomActor && tickets[i] == localTicket) ++matches;
            // A new arrival/room/scene/pause generation cannot consume an old packet.
            if (matches != 1 || !gates[(int)sector].TryAccept(message.Sender, ticket, incomingSerial,
                issued, duration, PhotonNetwork.Time)) return;
            bool repeated = lastReceived[(int)sector] == id && !collection.allowSingleLineRepeat;
            lastReceived[(int)sector] = id;
            var entry = collection.FindPlayable(id);
            // A missing local clip reserves the slot, but produces neither sound nor captions.
            if (repeated || entry == null || Math.Abs(collection.Duration(entry) - duration) > 0.1) return;
            output = NearestSpeaker();
            if (output == null) return;
            speech = entry.speech;
            transcript = entry.transcript;
            startCue = FacilityAnnouncementCollection.CueLength(collection.startCue) > 0 ? collection.startCue : null;
            endCue = FacilityAnnouncementCollection.CueLength(collection.endCue) > 0 ? collection.endCue : null;
            startsAt = Math.Max(PhotonNetwork.Time, issued + FacilityAnnouncementRules.StartLeadSeconds);
            stage = Stage.Waiting;
        }

        private FacilitySpeaker NearestSpeaker()
        {
            FacilitySpeaker best = null;
            float distance = float.PositiveInfinity;
            foreach (var speaker in Speakers)
            {
                if (speaker == null || !speaker.Ready || speaker.sector != sector ||
                    speaker.gameObject.scene.handle != sceneHandle || speaker.collection != collection) continue;
                float d = (speaker.source.transform.position - localCamera.transform.position).sqrMagnitude;
                if (d < distance) { best = speaker; distance = d; }
            }
            return best;
        }

        private bool PlayStage(AudioClip clip, Stage next, bool cue)
        {
            if (output == null || !output.PlayClip(clip, cue)) { CancelPlayback(); return false; }
            stage = next;
            stageStarted = Time.unscaledTime;
            stageLength = clip.length;
            observedPlaying = false;
            return true;
        }

        private void TickPlayback()
        {
            if (stage == Stage.None) return;
            if (output == null || !output.Ready || output.gameObject.scene.handle != sceneHandle || localCamera == null ||
                !localCamera.isActiveAndEnabled || output.source.mute || output.volume <= 0 || AudioListener.volume <= 0)
            { CancelPlayback(); return; }
            AudioClip expected = stage == Stage.StartCue ? startCue : stage == Stage.EndCue ? endCue : speech;
            if (stage != Stage.Waiting && output.source.clip != expected) { CancelPlayback(); return; }
            if (stage == Stage.Waiting)
            {
                if (PhotonNetwork.Time < startsAt) return;
                if (startCue != null) PlayStage(startCue, Stage.StartCue, true);
                else PlayStage(speech, Stage.Speech, false);
                return;
            }
            if (output.source.isPlaying)
            {
                observedPlaying = true;
                if (stage == Stage.Speech)
                {
                    bool inRange = Vector3.Distance(localCamera.transform.position, output.source.transform.position) < output.source.maxDistance;
                    if (inRange && EnsureCaptions()) captions.Show(transcript);
                    else if (captions != null) captions.Clear();
                }
                if (Time.unscaledTime - stageStarted > stageLength + 0.5f) CancelPlayback();
                return;
            }
            if (captions != null) captions.Clear();
            float elapsed = Time.unscaledTime - stageStarted;
            var completion = FacilityAnnouncementRules.CompletedClip(stage == Stage.Speech, observedPlaying, elapsed, stageLength);
            if (completion == FacilityAnnouncementRules.PlaybackCompletion.Waiting) return;
            if (completion == FacilityAnnouncementRules.PlaybackCompletion.Interrupted) { CancelPlayback(); return; }
            if (stage == Stage.StartCue) PlayStage(speech, Stage.Speech, false);
            else if (stage == Stage.Speech && endCue != null) PlayStage(endCue, Stage.EndCue, true);
            else CancelPlayback();
        }

        private bool EnsureCaptions()
        {
            if (output.captionPrefab == null || localCamera == null) return false;
            if (captions != null && (captionTemplate != output.captionPrefab || captionCamera != localCamera))
            { Destroy(captions.gameObject); captions = null; }
            if (captions == null)
            {
                captionTemplate = output.captionPrefab;
                captionCamera = localCamera;
                captions = Instantiate(captionTemplate, localCamera.transform, false);
                if (!captions.Bind(localCamera)) { Destroy(captions.gameObject); captions = null; }
            }
            return captions != null;
        }

        private void CancelPlayback()
        {
            if (output != null) output.StopPlayback();
            if (captions != null) captions.Clear();
            stage = Stage.None;
            output = null;
            speech = startCue = endCue = null;
            transcript = null;
        }
    }
}
