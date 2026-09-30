using System;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.FacilityAnnouncements;
using RunawayChimps.Travel;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using Rules = RunawayChimps.FacilityAnnouncements.FacilityAnnouncementRules;

internal static class Program
{
    private static int assertions, cases, failures;
    private const string RemoteTicket = "11111111111111111111111111111111";
    private static void Check(bool condition, string message)
    { ++assertions; if (!condition) throw new InvalidOperationException(message); }
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Call(object target, string method, params object[] args) => HarnessObjects.CallOptional(target, method, args);
    private static void Run(string name, Action test)
    {
        ++cases;
        try { test(); Console.WriteLine("PASS: " + name); }
        catch (Exception e) { ++failures; Console.WriteLine("FAIL: " + name + ": " + e.GetBaseException().Message); }
    }
    private static int Main()
    {
        Run("ordinary recorded playback and caption cleanup", NormalPlayback);
        Run("stalled cue cannot start expired speech", ExpiredCue);
        Run("expired output is stopped before replacement", ExpiredOutput);
        Run("occupied slot rejects new packets", OccupiedSlot);
        Run("delayed speech must still fit its reservation", DelayedSpeech);
        Run("disabled captions cannot be resurrected by playback", DisabledCaptions);
        Run("actual director handover cancels and guards A-B-A", DirectorHandover);
        Run("failed send does not consume single-line history", FailedSend);
        Run("send rechecks authority at deadline", StaleSender);
        Run("Photon clock rollover rearms rather than freezing", ClockRollover);
        Run("disabled locomotion withdraws cached-camera readiness", DisabledRig);
        Run("replacement local rig rebinds the camera", ReplacedRig);
        Run("streaming and zero-weight speech are not playable", InvalidContent);
        Run("nonfinite local presentation controls are bounded", InvalidControls);
        Run("room, sector, scene, pause and focus cancel captions", LifecycleCancellation);
        Run("malformed, duplicate and previous-visit packets are silent", InvalidPackets);
        Run("external clip replacement cancels subtitles", ReplacedClip);
        Run("overlapping speakers choose exactly one output", MultipleSpeakers);
        Run("empty and pending collections stay silent", MissingContent);
        Console.WriteLine($"RESULT: {cases} cases, {assertions} assertions, {failures} failures. Production adapters with instrumented managed doubles; NOT Unity/native audio/Photon transport/XR validation.");
        return failures == 0 ? 0 : 1;
    }
    private sealed class Fixture
    {
        public FacilityAnnouncementDirector Director;
        public FacilitySpeaker Speaker;
        public Camera Camera;
        public GorillaLocomotion.Player Rig;
        public Rules.Schedule Schedule => Get<Rules.Schedule>(Director,"schedule");
        public string Ticket => Get<string>(Director,"localTicket");
        public string Stage => Get<object>(Director,"stage").ToString();
        public FacilityAnnouncementCaptions Captions => Get<FacilityAnnouncementCaptions>(Director,"captions");
        public Fixture(int actor = 1, double time = 100)
        {
            typeof(FacilityAnnouncementDirector).GetMethod("ResetStatics",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            PhotonNetwork.Sent.Clear(); PhotonNetwork.SendSucceeds=true;
            PhotonNetwork.InRoom = PhotonNetwork.IsMessageQueueRunning = true;
            PhotonNetwork.CurrentRoom = new Room();
            PhotonNetwork.LocalPlayer = new Player { ActorNumber=actor, IsLocal=true };
            PhotonNetwork.PlayerList = new[] { PhotonNetwork.LocalPlayer };
            SectorTravelService.I = new SectorTravelService(); AppState.I = new AppState();
            Time.timeScale=1; AudioListener.pause=false; AudioListener.volume=1;
            PlayerPrefs.Clear(); SceneManager.Active=new Scene {handle=1,isLoaded=true};
            SetTime(time);
            BindRig();
            Speaker=CreateSpeaker(null, new Vector3(0,0,0));
            Call(Speaker,"OnEnable");
            Director=(FacilityAnnouncementDirector)typeof(FacilityAnnouncementDirector).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Tick();
        }
        public void BindRig()
        {
            var root=new GameObject("existing local rig");
            root.Attach(new LocalRigMarker());
            Rig=root.Attach(new GorillaLocomotion.Player()); GorillaLocomotion.Player.Instance=Rig;
            var head=new GameObject("existing camera"); head.transform.SetParent(root.transform,false);
            Camera=head.Attach(new Camera());
            root.Attach(new XROrigin {Camera=Camera});
        }
        public FacilitySpeaker CreateSpeaker(FacilityAnnouncementCollection content, Vector3 position)
        {
            var root=new GameObject("speaker"); root.transform.position=position;
            if(content==null) content=new FacilityAnnouncementCollection {
                entries=new[] {new FacilityAnnouncementCollection.Entry {id="one",speechAvailable=true,speech=new AudioClip(),transcript="One."}},
                startCue=new AudioClip {length=0.22f}, endCue=new AudioClip {length=0.22f}, diagnosticScheduling=true };
            var speaker=root.Attach(new FacilitySpeaker {source=root.Attach(new AudioSource()),collection=content,captionPrefab=HarnessObjects.Captions()});
            speaker.ConfigureSource();
            return speaker;
        }
        public void Tick(double? now=null) { if(now.HasValue) SetTime(now.Value); Call(Director,"Update"); }
        public EventData Message(int serial=1, double? issued=null, string ticket=null)
        {
            return new EventData {Code=FacilityAnnouncementDirector.AnnouncementEvent,Sender=PhotonNetwork.LocalPlayer.ActorNumber,
                CustomData=new object[] {Rules.Protocol,1,Speaker.collection.collectionId,Speaker.collection.contentRevision, ticket??Ticket,
                    serial,issued??PhotonNetwork.Time,"one",Speaker.collection.Duration(Speaker.collection.entries[0]),
                    new[] {PhotonNetwork.LocalPlayer.ActorNumber}, new[] {ticket??Ticket} } };
        }
        public void Receive(int serial=1) => Director.OnEvent(Message(serial));
        public void StartSpeech()
        { Receive(); Tick(100.5); Speaker.source.isPlaying=false; Tick(100.8); Tick(100.9); }
    }
    private static void SetTime(double time) { PhotonNetwork.Time=time; Time.unscaledTime=(float)time; }
    private static void NormalPlayback()
    {
        var f=new Fixture(); f.StartSpeech();
        Check(f.Stage=="Speech" && f.Speaker.source.isPlaying,"speech starts after cue");
        Check(f.Captions!=null && f.Captions.label.text=="One." && f.Captions.panel.gameObject.activeSelf,"matching live caption");
        f.Speaker.source.isPlaying=false; f.Tick(105.8);
        Check(f.Stage=="EndCue" && f.Captions.label.text==string.Empty,"caption clears before end cue");
        f.Speaker.source.isPlaying=false; f.Tick(106.2);
        Check(f.Stage=="None" && f.Speaker.source.clip==null,"completion clears source");
    }
    private static void ExpiredCue()
    {
        var f=new Fixture(); f.Receive(); f.Tick(100.5); f.Speaker.source.isPlaying=false; f.Tick(112);
        Check(f.Stage=="None" && !f.Speaker.source.isPlaying,"no delayed speech beyond reserved slot");
    }
    private static void ExpiredOutput()
    {
        var f=new Fixture(); f.StartSpeech();
        var other=f.CreateSpeaker(f.Speaker.collection,new Vector3(10,0,0)); FacilityAnnouncementDirector.Register(other);
        f.Camera.transform.position=new Vector3(10,0,0); f.Speaker.collection.allowSingleLineRepeat=true;
        SetTime(108); f.Receive(2);
        Check(!f.Speaker.source.isPlaying && f.Speaker.source.clip==null,"old source cannot be orphaned by replacement packet");
        Check(f.Captions==null || f.Captions.label.text==string.Empty,"old caption removed before next cue");
    }
    private static void OccupiedSlot()
    {
        var f=new Fixture(); f.StartSpeech(); f.Receive(2);
        Check(f.Stage=="Speech" && f.Speaker.source.Plays==2,"in-flight packet cannot replace active output");
    }
    private static void DelayedSpeech()
    {
        var f=new Fixture(); f.Receive(); f.Tick(100.5); f.Speaker.source.isPlaying=false; f.Tick(103);
        Check(f.Stage=="None" && !f.Speaker.source.isPlaying,"do not start a sentence that cannot finish inside its slot");
    }
    private static void DisabledCaptions()
    {
        var f=new Fixture(); f.StartSpeech(); var captions=f.Captions;
        captions.enabled=false; Call(captions,"OnDisable"); f.Tick(101);
        Check(captions.label.text==string.Empty && !captions.panel.gameObject.activeSelf,"disabled view must not accept direct Show calls");
        f.Speaker.source.Stop(); f.Tick(101.1); captions.enabled=true; Call(captions,"LateUpdate");
        Check(captions.label.text==string.Empty && !captions.panel.gameObject.activeSelf,"no stale caption on reenable");
    }
    private static void DirectorHandover()
    {
        var f=new Fixture(2); f.StartSpeech();
        var remote=new Player {ActorNumber=1};
        remote.CustomProperties[SectorPresence.PropertyKey]=1;
        remote.CustomProperties[FacilityAnnouncementDirector.ReadySectorKey]=1;
        remote.CustomProperties[FacilityAnnouncementDirector.ReadyTicketKey]=RemoteTicket;
        PhotonNetwork.PlayerList=new[] {PhotonNetwork.LocalPlayer,remote}; f.Tick(101.2);
        Check(f.Stage=="None" && f.Captions.label.text==string.Empty,"handover clears active speech and caption");
        Check(f.Schedule.Due>=137.19,"new owner gets full handover guard");
        var stale=f.Message(99,101.5); remote.IsInactive=true; f.Tick(102);
        Check(f.Schedule.Due>=138,"A-B-A return gets a new guarded term");
        f.Director.OnEvent(stale); Check(f.Stage=="None","delayed prior A term is rejected");
        f.Speaker.collection.allowSingleLineRepeat=true; SetTime(140); f.Receive(2);
        Check(f.Stage=="Waiting","fresh A term is eligible after guard");
    }
    private static void FailedSend()
    {
        var f=new Fixture(); PhotonNetwork.SendSucceeds=false; f.Schedule.Arm(100,0); f.Tick();
        Check(PhotonNetwork.CurrentRoom.PropertyWrites==0,"rejected RaiseEvent must not consume repeat history");
        Check(PhotonNetwork.Sent.Count==0 && f.Schedule.Due>100,"failed send rearms quietly");
        PhotonNetwork.SendSucceeds=true; f.Schedule.Arm(101,0); f.Tick(101);
        Check(PhotonNetwork.Sent.Count==1 && PhotonNetwork.CurrentRoom.PropertyWrites==1,"single line remains available when sending recovers");
        f.Director.OnEvent(PhotonNetwork.Sent[0]);
        Check(f.Stage=="Waiting","successful send follows the same receiver path as peers");
    }
    private static void StaleSender()
    {
        var f=new Fixture(2); var remote=new Player {ActorNumber=1};
        remote.CustomProperties[SectorPresence.PropertyKey]=1;
        remote.CustomProperties[FacilityAnnouncementDirector.ReadySectorKey]=1;
        remote.CustomProperties[FacilityAnnouncementDirector.ReadyTicketKey]=RemoteTicket;
        PhotonNetwork.PlayerList=new[] {PhotonNetwork.LocalPlayer,remote};
        f.Schedule.Arm(100,0); f.Tick(100.1);
        Check(PhotonNetwork.Sent.Count==0 && PhotonNetwork.CurrentRoom.PropertyWrites==0,"new lower sector authority wins before a due send");
    }
    private static void ClockRollover()
    {
        var f=new Fixture(time:4294967.0); string previous=f.Ticket;
        f.Tick(0.1); f.Tick(0.4);
        Check(f.Ticket!=null && f.Ticket!=previous,"rollover retires previous readiness generation");
        Check(f.Schedule.Due<500 && f.Schedule.Due>35,"fresh bounded quiet guard rather than a 49-day wait");
        Check(PhotonNetwork.Sent.Count==0,"no rollover catchup announcement");
        f=new Fixture(time:4294966); f.Receive(); f.Tick(4294966.5);
        Check(f.Speaker.source.isPlaying,"cue was active before rollover");
        f.Tick(0.1);
        Check(f.Stage=="None" && !f.Speaker.source.isPlaying,"rollover also cancels in-flight audio");
    }
    private static void DisabledRig()
    {
        var f=new Fixture(); f.StartSpeech(); f.Rig.enabled=false; f.Tick(101);
        Check(f.Stage=="None" && f.Ticket==null,"disabled locomotion invalidates cached active camera");
        Check((int)PhotonNetwork.LocalPlayer.CustomProperties[FacilityAnnouncementDirector.ReadySectorKey]==0,"withdraw eligibility");
        Check(f.Captions.label.text==string.Empty,"disabled rig clears captions");
    }
    private static void ReplacedRig()
    {
        var f=new Fixture(); var old=f.Camera; f.BindRig(); f.Tick(101.1);
        Check(Get<Camera>(f.Director,"localCamera")==f.Camera && f.Camera!=old,"cached camera must follow current local rig instance");
    }
    private static void InvalidContent()
    {
        var f=new Fixture(); var entry=f.Speaker.collection.entries[0];
        entry.speech.loadType=AudioClipLoadType.Streaming;
        Check(!FacilityAnnouncementCollection.HasSpeech(entry),"streaming speech cannot satisfy the preload contract");
        entry.speech.loadType=AudioClipLoadType.DecompressOnLoad; entry.weight=0;
        Check(!f.Speaker.collection.HasPlayableContent() && f.Speaker.collection.FindPlayable("one")==null,"zero weight is not an eligible schedule source");
    }
    private static void InvalidControls()
    {
        var f=new Fixture(); f.Speaker.minimumDistance=float.NaN; f.Speaker.maximumDistance=float.PositiveInfinity; f.Speaker.volume=float.NaN;
        f.Speaker.ConfigureSource();
        Check(Rules.Finite(f.Speaker.source.minDistance) && Rules.Finite(f.Speaker.source.maxDistance) && Rules.Finite(f.Speaker.source.volume),"source settings must stay finite");
        FacilityAnnouncementCaptions.TextScale=float.NaN; FacilityAnnouncementCaptions.VerticalPosition=float.NaN;
        Check(Rules.Finite(FacilityAnnouncementCaptions.TextScale) && Rules.Finite(FacilityAnnouncementCaptions.VerticalPosition),"preference API must not poison caption geometry");
    }
    private static void LifecycleCancellation()
    {
        foreach(string reason in new[] {"room","sector","scene","pause","focus","timescale","listener","disable","disconnect"})
        {
            var f=new Fixture(); f.StartSpeech();
            switch(reason)
            {
                case "room": PhotonNetwork.CurrentRoom=new Room(); break;
                case "sector": SectorTravelService.I.CurrentSector=SectorId.Containment; break;
                case "scene": SceneManager.Active=new Scene {handle=2,isLoaded=true}; break;
                case "pause": Call(f.Director,"OnApplicationPause",true); break;
                case "focus": Call(f.Director,"OnApplicationFocus",false); break;
                case "timescale": Time.timeScale=0; break;
                case "listener": AudioListener.pause=true; break;
                case "disable": f.Speaker.enabled=false; Call(f.Speaker,"OnDisable"); break;
                case "disconnect": PhotonNetwork.InRoom=false; break;
            }
            f.Tick(101);
            Check(f.Stage=="None" && !f.Speaker.source.isPlaying,reason+" cancels playback");
            Check(f.Captions==null || f.Captions.label.text==string.Empty,reason+" clears captions");
        }
    }
    private static void InvalidPackets()
    {
        var f=new Fixture(); var message=f.Message(); ((object[])message.CustomData)[6]=double.NaN; f.Director.OnEvent(message);
        Check(f.Stage=="None","nonfinite issued timestamp rejected");
        f.Receive(); var old=f.Message(); f.Receive(); Check(f.Stage=="Waiting","duplicate cannot double-start");
        Call(f.Director,"OnApplicationPause",true); Call(f.Director,"OnApplicationPause",false); f.Tick(101);
        f.Director.OnEvent(old); Check(f.Stage=="None","old visit rejected after resume");
    }
    private static void ReplacedClip()
    {
        var f=new Fixture(); f.StartSpeech(); f.Speaker.source.clip=new AudioClip(); f.Tick(101);
        Check(f.Stage=="None" && f.Captions.label.text==string.Empty,"replaced clip clears selected transcript");
    }
    private static void MultipleSpeakers()
    {
        var f=new Fixture(); var other=f.CreateSpeaker(f.Speaker.collection,new Vector3(1,0,0)); FacilityAnnouncementDirector.Register(other);
        f.StartSpeech(); Check(f.Speaker.source.Plays==2 && other.source.Plays==0,"only nearest source plays");
        Check(f.Captions!=null,"one director caption view");
    }
    private static void MissingContent()
    {
        var f=new Fixture(); f.Speaker.collection.entries=Array.Empty<FacilityAnnouncementCollection.Entry>(); f.Tick(101);
        Check(f.Ticket==null && PhotonNetwork.Sent.Count==0,"empty collection has no authority or events");
        f=new Fixture(); f.Speaker.collection.entries[0].speechAvailable=false; f.Tick(101);
        Check(f.Ticket==null && !f.Speaker.source.isPlaying,"pending speech is silent");
    }
}
