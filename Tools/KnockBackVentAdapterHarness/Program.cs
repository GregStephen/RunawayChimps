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
        public Fixture(int local=1)
        {
            Clock(100);
            PhotonNetwork.Sent.Clear();PhotonNetwork.FailNextSend=false;Debug.Errors.Clear();
            GorillaLocomotion.Player.Instance=null;
            LoadingFlow.IsColdStartupPresentationActive=false;
            AppState.I=null;
            SectorTravelService.I=new SectorTravelService();
            PhotonNetwork.CurrentRoom=new Room();
            for(int i=1;i<=3;i++) PhotonNetwork.CurrentRoom.Players[i]=Member(i,i==local,SectorId.Hub);
            PhotonNetwork.LocalPlayer=PhotonNetwork.CurrentRoom.Players[local];
            Vent=BuildVent();Vent.OnEnable();
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
    private static void Run(string name,Action action) { action();cases++;Console.WriteLine("PASS: adapter " + name); }

    private static int Main()
    {
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
            Console.WriteLine($"PASS: {cases} managed adapter cases / {assertions} assertions. Production adapter/core/election linked against instrumented doubles; NOT Unity, native physics, Photon or headset validation.");
            return 0;
        }
        catch(Exception failure) {Console.Error.WriteLine(failure);return 1;}
    }
}
