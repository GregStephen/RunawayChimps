using System;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.Toys.KnockBack;
using RunawayChimps.Travel;
using UnityEngine;
using UnityEngine.XR;
using NetPlayer = Photon.Realtime.Player;

internal static class Program
{
    private static int assertions, cases;
    private static string filter;
    private static void Require(bool value, string reason)
    { assertions++; if (!value) throw new InvalidOperationException(reason); }
    private static void Near(double a,double b,string reason) => Require(Math.Abs(a-b)<0.00002,reason);
    private static object Call(object target,string method,params object[] args) =>
        target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
    private static void Clock(double value) { PhotonNetwork.Time=value; Time.unscaledTimeAsDouble=value; AudioSettings.dspTime=value+1000; }
    private static void Step(KnockBackVent vent,double end)
    {
        while (PhotonNetwork.Time<end-0.000001)
        { Clock(Math.Min(end,PhotonNetwork.Time+0.04)); Call(vent,"LateUpdate"); }
    }
    private static NetPlayer Member(int actor,bool local,SectorId sector)
    {
        var player=new NetPlayer {ActorNumber=actor,IsLocal=local};
        player.CustomProperties[SectorPresence.PropertyKey]=(int)sector;
        return player;
    }
    private static KnockBackVent BuildVent()
    {
        var root=new GameObject();
        var vent=root.AddComponent<KnockBackVent>();
        root.AddComponent<BlockHandSurfaceAudio>();
        vent.panelCollider=root.AddComponent<BoxCollider>();
        vent.panelCollider.size=new Vector3(0.9f,0.64f,0.1f);
        var visual=new GameObject();visual.transform.parent=root.transform;vent.panelVisual=visual.transform;
        vent.tapClip=new AudioClip {length=0.09f};vent.replyClip=new AudioClip {length=0.24f};vent.bangClip=new AudioClip {length=0.45f};
        vent.tapVoices=new AudioSource[8];vent.replyVoices=new AudioSource[9];
        for(int i=0;i<17;i++)
        {
            var source=new GameObject();source.transform.parent=root.transform;
            source.transform.localPosition=new Vector3(0,0,i<8?0.065f:-0.18f);
            var voice=source.AddComponent<AudioSource>();
            if(i<8) vent.tapVoices[i]=voice;else vent.replyVoices[i-8]=voice;
        }
        vent.rhythm.extraKnockChance=vent.rhythm.heavyBangChance=0;
        return vent;
    }
    private sealed class Fixture : IDisposable
    {
        public readonly KnockBackVent Vent;
        public Fixture(int local=1, VentSettings settings=null)
        {
            AudioSettings.ReadAdvance=0;
            Clock(100);
            PhotonNetwork.Sent.Clear();PhotonNetwork.FailNextSend=false;Debug.Errors.Clear();
            GorillaLocomotion.Player.Instance=null;
            InputDevices.Device=new InputDevice {isValid=true,tracked=true};
            LoadingFlow.IsColdStartupPresentationActive=false;
            AppState.I=null;
            SectorTravelService.I=new SectorTravelService();
            PhotonNetwork.CurrentRoom=new Room();
            for(int i=1;i<=3;i++) PhotonNetwork.CurrentRoom.Players[i]=Member(i,i==local,SectorId.Hub);
            PhotonNetwork.LocalPlayer=PhotonNetwork.CurrentRoom.Players[local];
            Vent=BuildVent();if(settings!=null) Vent.rhythm=settings;Vent.OnEnable();
            Require(Debug.Errors.Count==0,"complete fixture references valid");
        }
        public void Dispose() { Vent.OnDisable();GorillaLocomotion.Player.Instance=null; }
    }
    private static void Receive(KnockBackVent vent,int sender,object[] data) => vent.OnEvent(new EventData {Code=KnockBackVent.EventCode,Sender=sender,CustomData=data});
    private static void Tap(KnockBackVent vent,int sender,double input) => Receive(vent,sender,
        new object[] {KnockBackVent.Protocol,1,vent.interactionId,(byte)0,PhotonNetwork.CurrentRoom.Name,input});
    private static object[] Packet(KnockBackVent vent,byte kind,double cycle,double issued,int owner,params object[] payload) =>
        new object[] {KnockBackVent.Protocol,1,vent.interactionId,kind,PhotonNetwork.CurrentRoom.Name,cycle,issued,owner,payload};
    private static int Scheduled(KnockBackVent vent) => vent.tapVoices.Concat(vent.replyVoices).Count(v=>v.scheduled.HasValue);
    private static void Run(string name,Action action)
    {
        if (filter != null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return;
        action(); cases++; Console.WriteLine("PASS: adapter " + name);
    }

    private static int Main(string[] args)
    {
        filter = args.Length > 0 ? args[0] : null;
        try
        {
            Run("authoritative accepted pattern and one plan",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                Step(v,100.1);Tap(v,2,100.08);
                Step(v,100.34);Tap(v,2,100.3);
                Step(v,100.68);Tap(v,2,100.66);
                Require(v.Tapper==2&&v.State==VentPhase.Recording,"authority locks tapper");
                var taps=PhotonNetwork.Sent.Where(p=>(byte)p.data[3]==1).ToArray();
                Require(taps.Length==3,"broadcast accepted taps exactly once");
                Near((double)((object[])taps[1].data[8])[1]-(double)((object[])taps[0].data[8])[1],0.22,"tap scheduling uses input timestamps");
                Step(v,101.4);
                var replies=PhotonNetwork.Sent.Where(p=>(byte)p.data[3]==2).ToArray();
                Require(replies.Length==1,"one authoritative reply");
                var body=(object[])replies[0].data[8];var offsets=(float[])body[2];
                Near(offsets[1],0.22,"reply interval one");Near(offsets[2],0.58,"reply interval two");
                Require(v.replyVoices.Count(a=>a.scheduled.HasValue)==3,"three scheduled replies");
                Near(v.replyVoices[1].scheduled.Value-v.replyVoices[0].scheduled.Value,0.22,"DSP schedule interval");
                foreach(var packet in PhotonNetwork.Sent)
                { Require(packet.options.CachingOption==EventCaching.DoNotCache&&packet.send.Reliability,"reliable uncached delivery");Require(packet.options.TargetActors.SequenceEqual(new[]{2,3}),"explicit sector audience"); }
                Step(v,(double)body[1]+0.1);
                Require(v.State==VentPhase.Idle&&Scheduled(v)==0,"normal completion frees every source");
            });
            Run("other tapper, malformed, stale and wrong sector requests",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                Step(v,100.1);Tap(v,2,100.1);Tap(v,3,100.1);
                Tap(v,2,100.1);Tap(v,2,double.NaN);Tap(v,2,101.1);Tap(v,2,99);
                PhotonNetwork.CurrentRoom.GetPlayer(3).CustomProperties[SectorPresence.PropertyKey]=2;
                Step(v,100.3);Tap(v,3,100.3);
                Require(PhotonNetwork.Sent.Count==1,"only genuine first accepted request broadcast");
            });
            Run("late sector arrival has no old audience or backlog",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                var late=PhotonNetwork.CurrentRoom.GetPlayer(3);late.CustomProperties[SectorPresence.PropertyKey]=2;
                Step(v,100.1);Tap(v,2,100.1);
                Step(v,100.2);late.CustomProperties[SectorPresence.PropertyKey]=1;
                v.OnPlayerPropertiesUpdate(late,new Hashtable());
                Step(v,100.9);
                Require(PhotonNetwork.Sent.Count==2,"tap and reply");
                Require(PhotonNetwork.Sent.All(p=>p.options.TargetActors.SequenceEqual(new[]{2})),"late arrival never included in existing cycle");
            });
            Run("replica rejects duplicate or non-authority reply",()=>
            {
                using var f=new Fixture(2);var v=f.Vent;
                Step(v,100.1);
                var tap=Packet(v,1,100.1,100.1,3,0,100.3d);
                Receive(v,3,tap);Require(Scheduled(v)==0,"non-authority rejected");
                Receive(v,1,tap);Receive(v,1,tap);
                Require(v.tapVoices[0].playCalls==1,"duplicate accepted tap suppressed");
                Step(v,100.8);
                var reply=Packet(v,2,100.1,100.8,3,101.55d,104.27d,new float[]{0,0.22f},1);
                Receive(v,1,reply);Receive(v,1,reply);
                Require(v.replyVoices.Sum(a=>a.playCalls)==2,"duplicate reply cannot schedule twice");
                Require(v.replyVoices[1].clip==v.bangClip,"authority-selected heavy index shared, not rerolled");
                Require(PhotonNetwork.Sent.Count==0,"replica emits no independent reply");
            });
            Run("room replacement cancels and rejects old room packets",()=>
            {
                using var f=new Fixture(2);var v=f.Vent;
                Step(v,100.1);var old=Packet(v,1,100.1,100.1,1,0,100.3d);Receive(v,1,old);
                var previous=PhotonNetwork.CurrentRoom;
                PhotonNetwork.CurrentRoom=new Room {Name="other-room",Players=previous.Players};
                v.OnJoinedRoom();Require(Scheduled(v)==0&&v.State==VentPhase.Idle,"room cancellation");
                Receive(v,1,old);Require(Scheduled(v)==0,"old room packet rejected");
            });
            Run("controller A-B-A cancels scheduled reply and retires term",()=>
            {
                using var f=new Fixture(2);var v=f.Vent;
                Step(v,100.1);Receive(v,1,Packet(v,1,100.1,100.1,3,0,100.3d));
                Step(v,100.8);var reply=Packet(v,2,100.1,100.8,3,101.55d,104.05d,new float[]{0},-1);Receive(v,1,reply);
                Require(Scheduled(v)>0,"reply staged");
                Step(v,100.9);var a=PhotonNetwork.CurrentRoom.GetPlayer(1);a.CustomProperties[SectorPresence.PropertyKey]=2;
                v.OnPlayerPropertiesUpdate(a,new Hashtable());
                Require(v.Controller==2&&Scheduled(v)==0&&v.State==VentPhase.Idle,"B term cancels");
                Step(v,101);a.CustomProperties[SectorPresence.PropertyKey]=1;v.OnPlayerPropertiesUpdate(a,new Hashtable());
                Require(v.Controller==1,"A returns");Receive(v,1,reply);
                Require(Scheduled(v)==0,"A cannot replay old term");
            });
            Run("tapper departure cancels without authority change",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                Step(v,100.1);Tap(v,2,100.1);
                Step(v,100.2);var owner=PhotonNetwork.CurrentRoom.GetPlayer(2);owner.CustomProperties[SectorPresence.PropertyKey]=2;
                v.OnPlayerPropertiesUpdate(owner,new Hashtable());
                Require(v.Controller==1&&Scheduled(v)==0&&v.State==VentPhase.Idle,"owner left sector");
                Require(PhotonNetwork.Sent.Any(p=>(byte)p.data[3]==3),"cancellation broadcast");
            });
            Run("cancel remains valid deep into a long reply",()=>
            {
                using var f=new Fixture(2);var v=f.Vent;
                Step(v,100.1);Receive(v,1,Packet(v,1,100.1,100.1,1,0,100.3d));
                Step(v,104.9);Receive(v,1,Packet(v,2,100.1,104.9,1,106.9d,114.4d,new float[]{0,1,2,3,4,5},-1));
                Require(v.replyVoices.Count(a=>a.scheduled.HasValue)==6,"long plan scheduled");
                Step(v,107);Receive(v,1,Packet(v,3,100.1,107,1));
                Require(Scheduled(v)==0&&v.State==VentPhase.Idle,"late cancellation honored throughout bounded lifetime");
            });
            Run("local sector travel, pause and disable stop all voices",()=>
            {
                foreach(string interruption in new[]{"travel","pause","disable","audio"})
                {
                    using var f=new Fixture();var v=f.Vent;
                    Step(v,100.1);Tap(v,2,100.1);Step(v,100.9);
                    Require(v.replyVoices.Any(a=>a.scheduled.HasValue),"pending reply");
                    if(interruption=="travel") {SectorTravelService.I.IsBusy=true;Call(v,"LateUpdate");}
                    if(interruption=="pause") Call(v,"OnApplicationPause",true);
                    if(interruption=="disable") v.OnDisable();
                    if(interruption=="audio") AudioSettings.Change();
                    Require(Scheduled(v)==0&&v.State==VentPhase.Idle,"stop on " + interruption);
                }
            });
            Run("duplicate placement identities fail closed",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                Step(v,100.1);Tap(v,2,100.1);
                var duplicate=BuildVent();duplicate.OnEnable();Call(v,"LateUpdate");
                Require(!v.IsAuthority&&!duplicate.IsAuthority&&Scheduled(v)==0,"no duplicate authorities/audio");
                duplicate.OnDisable();Call(v,"LateUpdate");
                Require(v.IsAuthority&&v.State==VentPhase.Idle,"recover cleanly when duplicate removed");
            });
            Run("explicit offline diagnostic and cancellation",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                PhotonNetwork.CurrentRoom=null;v.OnLeftRoom();v.PlayEditorSample();
                Step(v,102);
                Require(v.IsDiagnostic&&v.replyVoices.Sum(a=>a.playCalls)==3,"sample traversed production recorder/playback");
                Require(PhotonNetwork.Sent.Count==0,"offline sample sends no network traffic");
                v.CancelEditorSample();Require(Scheduled(v)==0&&!v.IsDiagnostic,"sample cleanup");
            });
            Run("too-late plan produces no catch-up burst",()=>
            {
                using var f=new Fixture(2);var v=f.Vent;
                Step(v,100.1);Receive(v,1,Packet(v,1,100.1,100.1,1,0,100.3d));
                Step(v,101.2);
                Receive(v,1,Packet(v,2,100.1,100.5,1,100.9d,103.62d,new float[]{0,0.22f},-1));
                Require(v.replyVoices.Sum(a=>a.playCalls)==0,"late plan never bursts missed knocks");
            });
            Run("cooldown packet boundary and failed broadcast",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                Step(v,100.1);Tap(v,2,100.1);Step(v,100.9);
                var reply=PhotonNetwork.Sent.Single(p=>(byte)p.data[3]==2);
                double until=(double)((object[])reply.data[8])[1];
                Step(v,until-0.02);
                // Deliver before the next LateUpdate: request handling must retire at the
                // true deadline, not treat queued cooldown input as a new pattern.
                Clock(until+0.01);Tap(v,2,until-0.01);
                Require(v.State==VentPhase.Idle,"old cooldown input not queued");
                Tap(v,2,until+0.005);
                Require(v.State==VentPhase.Recording,"fresh post-deadline input accepted");
                Step(v,until+0.7);
                Call(v,"AudioConfigurationChanged",true);
                PhotonNetwork.FailNextSend=true;
                Step(v,until+0.74);Tap(v,2,until+0.74);
                Require(v.State==VentPhase.Idle&&Scheduled(v)==0,"send failure cancels local-only response");
            });
            Run("actual hand sampler in translated and rotated frames",()=>
            {
                foreach(var rotation in new[]{Quaternion.identity,new Quaternion(0,0.70710677f,0,0.70710677f)})
                {
                    using var f=new Fixture();var v=f.Vent;
                    v.transform.position=new Vector3(10,2,-7);v.transform.rotation=rotation;
                    var rig=new GameObject();rig.transform.position=v.transform.TransformPoint(new Vector3(0,0,0.6f));rig.transform.rotation=rotation;
                    var player=new GameObject().AddComponent<GorillaLocomotion.Player>();player.transform.parent=rig.transform;
                    var head=new GameObject();head.transform.parent=rig.transform;head.transform.localPosition=new Vector3(0,0.1f,0.1f);player.headCollider=head.AddComponent<SphereCollider>();
                    var hand=new GameObject();hand.transform.parent=rig.transform;
                    player.leftHandFollower=hand.transform;
                    var sampler=new KnockBackVentHandInput();
                    for(int i=0;i<4;i++)
                    {
                        hand.transform.position=v.transform.TransformPoint(new Vector3(0,0,0.3f));
                        Require(!sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,i*0.04,0.3f,7,0.22f,0.07f),"released sample");
                    }
                    hand.transform.position=v.transform.TransformPoint(new Vector3(0,0,0.1f));
                    Require(sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,0.16,0.3f,7,0.22f,0.07f),"front tap independent of world orientation");
                    Require(!sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,0.2,0.3f,7,0.22f,0.07f),"resting hand ignored");
                    InputDevices.Device=new InputDevice {isValid=false};
                    Require(!sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,0.24,0.3f,7,0.22f,0.07f),"tracking lost");
                    InputDevices.Device=new InputDevice {isValid=true,tracked=true};
                    Require(!sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,0.28,0.3f,7,0.22f,0.07f),"tracking recovery cannot create contact");
                    for(int i=0;i<4;i++)
                    {
                        hand.transform.position=v.transform.TransformPoint(new Vector3(0,0,0.4f));
                        sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,0.32+i*0.04,0.3f,7,0.22f,0.07f);
                    }
                    hand.transform.position=v.transform.TransformPoint(new Vector3(0,0,0.1f));
                    Require(!sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,0.48,0.3f,7,0.22f,0.07f),"tracking jump into contact rejected");
                    Require(!sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,0.52,0.3f,7,0.22f,0.07f),"jumped contact cannot rearm in place");
                }
            });
            Run("same-tick cancellation cannot revive retired packets",()=>
            {
                using var f=new Fixture(2);var v=f.Vent;
                Step(v,100.1);
                var old=Packet(v,1,100.1,100.1,1,0,100.3d);
                Receive(v,1,old);
                Require(Scheduled(v)==1,"original tap scheduled");
                Receive(v,1,Packet(v,3,100.1,100.1,1));
                Receive(v,1,old);
                Require(Scheduled(v)==0&&v.State==VentPhase.Idle,"same timestamp must stay retired after Cancel");
                Step(v,100.12);Receive(v,1,Packet(v,1,100.12,100.12,1,0,100.32d));
                Require(Scheduled(v)==1,"fresh later cycle remains usable");
            });
            Run("delayed cancellation preserves a newer live cycle",()=>
            {
                using var f=new Fixture(2);var v=f.Vent;
                Step(v,100.1);Receive(v,1,Packet(v,1,100.1,100.1,1,0,100.3d));
                Step(v,100.9);Receive(v,1,Packet(v,3,100.1,100.7,1));
                Require(Scheduled(v)==0,"delayed cancel stops the old cycle");
                Step(v,100.95);Receive(v,1,Packet(v,1,100.8,100.8,1,0,101d));
                Require(Scheduled(v)==1&&v.State==VentPhase.Recording,
                    "new cycle issued after cancel must survive cancellation transport latency");
            });
            Run("same-tick authority handoff requires fresh input",()=>
            {
                using var f=new Fixture(2);var v=f.Vent;
                Step(v,100.1);
                var old=Packet(v,1,100.1,100.1,3,0,100.3d);Receive(v,1,old);
                var a=PhotonNetwork.CurrentRoom.GetPlayer(1);
                a.CustomProperties[SectorPresence.PropertyKey]=2;v.OnPlayerPropertiesUpdate(a,new Hashtable());
                Tap(v,3,100.1);
                Require(v.State==VentPhase.Idle,"queued input at handoff timestamp cannot start B term");
                a.CustomProperties[SectorPresence.PropertyKey]=1;v.OnPlayerPropertiesUpdate(a,new Hashtable());
                Receive(v,1,old);
                Require(Scheduled(v)==0,"same-tick A-B-A cannot resurrect A's first cycle");
            });
            Run("recording deadline tolerates a bounded render hitch",()=>
            {
                using var f=new Fixture(1,new VentSettings { maxTaps=8,maximumRecording=5,quietInterval=1.2f,extraKnockChance=0,heavyBangChance=0 });
                var v=f.Vent;
                for(int i=0;i<6;i++) { Step(v,100.1+i*0.92);Tap(v,2,PhotonNetwork.Time); }
                Step(v,105.08); // just before the hard recording deadline of 105.1
                Clock(105.68);Call(v,"LateUpdate"); // 600 ms hitch: below the existing 750 ms cancellation threshold
                Require(PhotonNetwork.Sent.Count(p=>(byte)p.data[3]==2)==1,"one bounded reply plan broadcast");
                Require(v.replyVoices.Count(a=>a.scheduled.HasValue)==6,"legal delayed plan must not reject itself");
            });
            Run("receiver watchdog includes permitted processing and transport delay",()=>
            {
                using var f=new Fixture(2);var v=f.Vent;
                Step(v,100.1);Receive(v,1,Packet(v,1,100.1,100.1,3,0,100.3d));
                Step(v,106.18); // receiver must not retire before a 5s recording + 0.75s frame gap + 0.75s delivery
                Receive(v,1,Packet(v,2,100.1,105.68,3,106.43d,113.53d,new float[]{0,0.92f,1.84f,2.76f,3.68f,4.60f},-1));
                Require(v.replyVoices.Count(a=>a.scheduled.HasValue)==6,"valid in-budget plan survives watchdog");
            });
            Run("missing reply watchdog remains bounded",()=>
            {
                using var f=new Fixture(2);var v=f.Vent;
                Step(v,100.1);Receive(v,1,Packet(v,1,100.1,100.1,3,0,100.3d));
                Step(v,107);
                Require(v.State==VentPhase.Idle&&Scheduled(v)==0,"missing reply eventually cancels");
            });
            Run("disconnect before disable never sends into a missing room",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                Step(v,100.1);Tap(v,2,100.1);
                PhotonNetwork.Sent.Clear();PhotonNetwork.CurrentRoom=null;
                v.enabled=false;v.OnDisable();
                Require(PhotonNetwork.Sent.Count==0,"OnDisable must not send old cancellation outside its observed room");
                Require(Scheduled(v)==0,"disconnected cleanup still stops voices");
            });
            Run("room replacement before disable never sends to new session",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                Step(v,100.1);Tap(v,2,100.1);
                var previous=PhotonNetwork.CurrentRoom;
                PhotonNetwork.CurrentRoom=new Room {Name=previous.Name,Players=previous.Players};
                PhotonNetwork.Sent.Clear();v.enabled=false;v.OnDisable();
                Require(PhotonNetwork.Sent.Count==0,"same-name replacement must not receive prior room cancellation");
            });
            Run("disabled adapter ignores already-dispatched callbacks",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                Step(v,100.1);Tap(v,2,100.1);
                v.enabled=false;v.OnDisable();PhotonNetwork.Sent.Clear();
                Clock(100.2);Tap(v,2,100.2);
                Require(!v.IsAuthority&&v.State==VentPhase.Idle&&Scheduled(v)==0&&PhotonNetwork.Sent.Count==0,
                    "late callbacks cannot re-enable a disabled toy");
            });
            Run("tracked controller cannot tap through a blocked virtual hand",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                var rig=new GameObject();var player=rig.AddComponent<GorillaLocomotion.Player>();
                var head=new GameObject();head.transform.parent=rig.transform;head.transform.position=new Vector3(0,0,0.7f);
                player.headCollider=head.AddComponent<SphereCollider>();
                var hand=new GameObject();hand.transform.parent=rig.transform;
                var follower=new GameObject();follower.transform.parent=rig.transform;follower.transform.position=new Vector3(0,0,0.4f);
                player.leftHandFollower=follower.transform;
                var sampler=new KnockBackVentHandInput();
                for(int i=0;i<4;i++)
                {
                    hand.transform.position=new Vector3(0,0,0.3f);
                    sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,i*0.04,0.3f,7,0.22f,0.07f);
                }
                hand.transform.position=new Vector3(0,0,0.1f);
                Require(!sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,0.16,0.3f,7,0.22f,0.07f),
                    "blocked or reach-clamped Gorilla follower never touched the panel");
                follower.transform.position=hand.transform.position;
                Require(!sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,0.2,0.3f,7,0.22f,0.07f),
                    "unblocking a resting controller is not a fresh tap");
                for(int i=0;i<4;i++)
                {
                    hand.transform.position=follower.transform.position=new Vector3(0,0,0.3f);
                    sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,0.24+i*0.04,0.3f,7,0.22f,0.07f);
                }
                hand.transform.position=follower.transform.position=new Vector3(0,0,0.1f);
                Require(sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,XRNode.LeftHand,v.panelCollider,0.4,0.3f,7,0.22f,0.07f),
                    "fresh reachable tap still works after release");
            });
            Run("uniform scale preserves physical release distance for either hand",()=>
            {
                foreach(float scale in new[]{0.5f,1f,2f})
                foreach(XRNode node in new[]{XRNode.LeftHand,XRNode.RightHand})
                {
                    using var f=new Fixture();var v=f.Vent;
                    v.transform.localScale=Vector3.one*scale;
                    var rig=new GameObject();var player=rig.AddComponent<GorillaLocomotion.Player>();
                    var head=new GameObject();head.transform.position=new Vector3(0,0,0.7f);
                    player.headCollider=head.AddComponent<SphereCollider>();
                    var hand=new GameObject();hand.transform.parent=rig.transform;
                    player.leftHandFollower=player.rightHandFollower=hand.transform;
                    var sampler=new KnockBackVentHandInput();
                    // 40 mm beyond the 55 mm contact envelope is a release at every scale.
                    for(int i=0;i<4;i++)
                    {
                        hand.transform.position=new Vector3(0,0,0.05f*scale+0.095f);
                        Require(!sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,node,v.panelCollider,i*0.04,0.3f,7,0.22f,0.07f),"released scaled hand");
                    }
                    hand.transform.position=new Vector3(0,0,0.05f*scale+0.05f);
                    Require(sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,node,v.panelCollider,0.16,0.3f,7,0.22f,0.07f),"tap after physical 35 mm release threshold");
                    // Moving only 25 mm beyond contact must not rearm even on a half-size toy.
                    for(int i=0;i<4;i++)
                    {
                        hand.transform.position=new Vector3(0,0,0.05f*scale+0.08f);
                        sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,node,v.panelCollider,0.2+i*0.04,0.3f,7,0.22f,0.07f);
                    }
                    hand.transform.position=new Vector3(0,0,0.05f*scale+0.05f);
                    Require(!sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,node,v.panelCollider,0.36,0.3f,7,0.22f,0.07f),"partial release never rearms at another root scale");
                }
            });
            Run("disabled panel cancels and requires a new cycle",()=>
            {
                using var f=new Fixture();var v=f.Vent;
                Step(v,100.1);Tap(v,2,100.1);Step(v,100.9);
                Require(Scheduled(v)>0,"reply scheduled before collider disable");
                v.panelCollider.enabled=false;Call(v,"LateUpdate");
                Require(Scheduled(v)==0&&!v.IsAuthority,"disabled physical panel cancels");
                Step(v,101);Tap(v,2,101);
                Require(v.State==VentPhase.Idle,"disabled panel cannot accept requests");
                v.panelCollider.enabled=true;Call(v,"LateUpdate");
                Step(v,101.1);Tap(v,2,101.1);
                Require(v.State==VentPhase.Recording,"fresh enabled panel works again");
            });
            Run("slightly late reply preserves the complete rhythm",()=>
            {
                foreach(double lateness in new[]{-0.005d,0d,0.04d,0.079d})
                {
                    using var f=new Fixture(2);var v=f.Vent;
                    Step(v,100.1);Receive(v,1,Packet(v,1,100.1,100.1,3,0,100.3d));
                    Step(v,101.2+lateness);
                    Receive(v,1,Packet(v,2,100.1,100.8,3,101.2d,104.12d,new float[]{0,0.12f,0.42f},2));
                    Require(v.replyVoices.Take(3).All(a=>a.scheduled.HasValue),"within-tolerance reply is scheduled once");
                    Near(v.replyVoices[1].scheduled.Value-v.replyVoices[0].scheduled.Value,0.12,"late first interval must not compress");
                    Near(v.replyVoices[2].scheduled.Value-v.replyVoices[0].scheduled.Value,0.42,"whole pattern retains relative timing");
                    Require(v.replyVoices[2].clip==v.bangClip,"late scheduling retains authority-selected variation");
                    double first=v.replyVoices[0].scheduled.Value-1000;
                    Clock(first+0.045);Call(v,"LateUpdate");
                    Near(v.panelVisual.localPosition.z,0.0015,"visual follows actual first reply onset, not stale network start");
                }
            });
            Run("one audio clock anchor schedules every reply voice",()=>
            {
                using var f=new Fixture(2);var v=f.Vent;
                Step(v,100.1);Receive(v,1,Packet(v,1,100.1,100.1,3,0,100.3d));
                Step(v,100.9);AudioSettings.ReadAdvance=0.005;
                Receive(v,1,Packet(v,2,100.1,100.8,3,101.55d,104.63d,new float[]{0,0.22f,0.58f},-1));
                Near(v.replyVoices[1].scheduled.Value-v.replyVoices[0].scheduled.Value,0.22,"audio-thread clock tick cannot change first interval");
                Near(v.replyVoices[2].scheduled.Value-v.replyVoices[0].scheduled.Value,0.58,"all voices use one DSP epoch");
            });
            foreach(string interruption in new[]{"pause","audio reset"})
            foreach(bool beforeFirst in new[]{true,false})
                Run("interrupted offline sample stays cancelled: "+interruption+(beforeFirst?" before first tap":" after first tap"),()=>
                {
                    using var f=new Fixture();var v=f.Vent;
                    PhotonNetwork.CurrentRoom=null;v.OnLeftRoom();v.PlayEditorSample();
                    Step(v,beforeFirst?100.1:100.3);
                    int before=v.tapVoices.Sum(a=>a.playCalls);
                    if(interruption=="pause")
                    {
                        Call(v,"OnApplicationPause",true);
                        Clock(PhotonNetwork.Time+0.02);
                        Call(v,"OnApplicationPause",false);
                    }
                    else AudioSettings.Change();
                    Step(v,102);
                    Require(v.tapVoices.Sum(a=>a.playCalls)==before,"cancelled sample must not submit its remaining future taps");
                    Require(v.replyVoices.Sum(a=>a.playCalls)==0&&Scheduled(v)==0&&v.State==VentPhase.Idle,"interrupted diagnostic leaves no tail or partial reply");
                    v.PlayEditorSample();Step(v,104);
                    Require(v.replyVoices.Sum(a=>a.playCalls)==3,"only explicit restart begins a fresh complete diagnostic");
                });
            Run("resting virtual hand must release before controller can rearm",()=>
            {
                foreach(float scale in new[]{0.5f,1f,2f})
                foreach(XRNode node in new[]{XRNode.LeftHand,XRNode.RightHand})
                foreach(float handRadius in new[]{0.05f,0.08f,0.1f})
                {
                    using var f=new Fixture();var v=f.Vent;
                    v.transform.localScale=Vector3.one*scale;
                    var rig=new GameObject();var player=rig.AddComponent<GorillaLocomotion.Player>();
                    player.minimumRaycastDistance=handRadius;
                    var head=new GameObject();head.transform.position=new Vector3(0,0,0.7f);
                    player.headCollider=head.AddComponent<SphereCollider>();
                    var hand=new GameObject();hand.transform.parent=rig.transform;
                    var follower=new GameObject();follower.transform.parent=rig.transform;
                    player.leftHandFollower=player.rightHandFollower=follower.transform;
                    var sampler=new KnockBackVentHandInput();
                    float front=0.05f*scale;
                    for(int i=0;i<4;i++)
                    {
                        hand.transform.position=follower.transform.position=new Vector3(0,0,front+0.2f);
                        sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,node,v.panelCollider,i*0.04,0.3f,7,0.22f,0.07f);
                    }
                    hand.transform.position=new Vector3(0,0,front+0.05f);
                    follower.transform.position=new Vector3(0,0,front+handRadius);
                    Require(sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,node,v.panelCollider,0.16,0.3f,7,0.22f,0.07f),"first physical contact works");
                    for(int i=0;i<4;i++)
                    {
                        hand.transform.position=new Vector3(0,0,front+0.2f); // virtual hand stays on the panel
                        sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,node,v.panelCollider,0.2+i*0.04,0.3f,7,0.22f,0.07f);
                    }
                    hand.transform.position=new Vector3(0,0,front+0.05f);
                    Require(!sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,node,v.panelCollider,0.36,0.3f,7,0.22f,0.07f),"controller-only release cannot duplicate resting physical contact");
                    for(int i=0;i<4;i++)
                    {
                        hand.transform.position=follower.transform.position=new Vector3(0,0,front+0.2f);
                        sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,node,v.panelCollider,0.4+i*0.04,0.3f,7,0.22f,0.07f);
                    }
                    hand.transform.position=new Vector3(0,0,front+0.05f);
                    follower.transform.position=new Vector3(0,0,front+handRadius);
                    Require(sampler.Sample(player,rig.transform,hand.transform,Vector3.zero,node,v.panelCollider,0.56,0.3f,7,0.22f,0.07f),"releasing both poses allows another physical tap");
                }
            });
            if (cases == 0) throw new ArgumentException("No adapter cases matched: " + filter);
            Console.WriteLine($"PASS: {cases} managed adapter cases / {assertions} assertions. Production adapter/core/election linked against instrumented doubles; NOT Unity, native physics, Photon or headset validation.");
            return 0;
        }
        catch(Exception failure) {Console.Error.WriteLine(failure);return 1;}
    }
}
