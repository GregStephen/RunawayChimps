using System;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.Toys.PrimateCognitive;
using RunawayChimps.Travel;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class Program
{
    private static int assertions, context;
    private static void Check(bool condition, string reason)
    { assertions++; if (!condition) throw new Exception(reason); }
    private static void Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    private sealed class Client
    {
        public readonly int Context = ++context;
        public readonly int Actor;
        public Room Room;
        public readonly CognitiveMachine Machine;
        public readonly GorillaLocomotion.Player Rig;
        public Collider Left, Right;
        public double Now;
        public Client(int actor = 1, bool network = false, int maximum = 2)
        {
            Actor = actor;
            if (network)
            {
                Room = new Room();
                for (int i = 1; i <= 2; i++)
                {
                    var p = new Photon.Realtime.Player { ActorNumber = i, IsLocal = i == actor };
                    p.CustomProperties[SectorPresence.PropertyKey] = (int)SectorId.Hub;
                    Room.Players.Add(i, p);
                }
            }
            Select(0);
            var rig = new GameObject("LocalRig"); rig.AddComponent<LocalRigMarker>();
            Rig = rig.AddComponent<GorillaLocomotion.Player>();
            Rig.headCollider = rig.AddComponent<SphereCollider>();
            Rig.leftHandTransform = Child(rig.transform, "LeftController").transform;
            Rig.rightHandTransform = Child(rig.transform, "RightController").transform;
            Left = Finger(Rig.leftHandTransform); Right = Finger(Rig.rightHandTransform);
            var root = new GameObject("Machine");
            Machine = root.AddComponent<CognitiveMachine>();
            Machine.maximumLength = maximum; Machine.editorControls = true;
            Machine.operatorAnchor = Child(root.transform, "Presence").transform;
            Machine.display = root.AddComponent<TMPro.TMP_Text>();
            Machine.audioSource = root.AddComponent<AudioSource>();
            Machine.clips = Enumerable.Range(0, 6).Select(_ => new AudioClip()).ToArray();
            Machine.pads = new CognitivePad[5];
            for (int i = 0; i < 5; i++)
            {
                var go = Child(root.transform, "Pad" + i); go.AddComponent<BoxCollider>();
                var pad = go.AddComponent<CognitivePad>(); pad.machine = Machine; pad.index = i;
                pad.cap = Child(go.transform, "Cap").transform;
                pad.capRenderer = pad.cap.gameObject.AddComponent<Renderer>();
                Machine.pads[i] = pad; Call(pad, "Awake"); Call(pad, "OnEnable");
            }
            Select(0); Call(Machine, "Awake"); Call(Machine, "OnEnable"); Call(Machine, "Start");
        }
        public void Select(double now)
        {
            Now = now; UnityEngine.Object.Context = Context;
            Time.unscaledTimeAsDouble = PhotonNetwork.Time = now;
            PhotonNetwork.CurrentRoom = Room; PhotonNetwork.LocalPlayer = Room?.GetPlayer(Actor);
            GorillaLocomotion.Player.Instance = Rig;
            SectorTravelService.I = null; LoadingFlow.IsColdStartupPresentationActive = false;
            SceneManager.Active = new Scene(1);
        }
        public void Step(double now)
        {
            Select(now);
            foreach (var pad in Machine.pads) Call(pad, "Update");
            Call(Machine, "Update");
        }
        public void Contact(int index, Collider collider, bool enter)
        { Select(Now); Call(Machine.pads[index], enter ? "OnTriggerEnter" : "OnTriggerExit", collider); }
        public void Touch(int index) { Select(Now); Machine.EditorContact(index, true); }
        public void Release(int index) { Select(Now); Machine.EditorContact(index, false); }
        public void Tap(int index) { Touch(index); Release(index); }
        public void InputPhase()
        {
            for (int i = 0; i < 300 && Machine.State.Phase != CognitivePhase.Input; i++) Step(Now + .1);
            Check(Machine.State.Phase == CognitivePhase.Input, "demonstration enters input");
            Step(Now + .12); // Explicit off-contact debounce, just like a player release.
        }
        public void StartGame()
        { Step(Now); Step(Now + .12); Tap(4); Check(Machine.IsOperator, "operator acquired through pad"); }
    }
    private static GameObject Child(Transform parent, string name)
    { var child = new GameObject(name); child.transform.parent = parent; return child; }
    private static Collider Finger(Transform hand)
    { var child = Child(hand, "Fingertip"); child.tag = "HandTag"; return child.AddComponent<BoxCollider>(); }
    private static void Deliver(PhotonNetwork.Sent packet, Client target, double now)
    { target.Select(now); target.Machine.OnEvent(new EventData { Sender = packet.Sender, Code = packet.Code, CustomData = packet.Data }); }
    private static PhotonNetwork.Sent Take(byte code, int sender)
    {
        var p = PhotonNetwork.Outbox.First(x => x.Code == code && x.Sender == sender);
        PhotonNetwork.Outbox.Remove(p); return p;
    }
    private static void Pump(double now, params Client[] clients)
    {
        int limit = 0;
        while (PhotonNetwork.Outbox.Count != 0)
        {
            if (++limit > 100) throw new Exception("diagnostic delivery loop exceeded bound");
            var packet = PhotonNetwork.Outbox[0]; PhotonNetwork.Outbox.RemoveAt(0);
            foreach (var client in clients)
                if (packet.Targets.Contains(client.Actor)) Deliver(packet, client, now);
        }
    }

    private static void DelayedSynchronization()
    {
        var authority = new Client(1, true); var observer = new Client(2, true);
        authority.Step(0); PhotonNetwork.Outbox.Clear(); observer.Step(0);
        var request = Take(188, 2); Deliver(request, authority, .1);
        var reply = Take(189, 1);
        observer.Step(.51); observer.Step(1.02); observer.Step(1.53);
        Deliver(reply, observer, 1.75);
        Check(observer.Machine.State != null, "delayed initial snapshot survives actual adapter retries");
        observer.Select(1.8); Check(!observer.Machine.IsOperator, "idle observer is not operator");
        PhotonNetwork.Outbox.Clear(); observer.Step(4);
        request = Take(188, 2);
        authority.Select(4); Call(authority.Machine, "OnDisable"); Call(authority.Machine, "OnEnable"); authority.Step(4);
        PhotonNetwork.Outbox.Clear(); Deliver(request, authority, 4.1); reply = Take(189, 1);
        observer.Step(6.1); // A retry after the original two-second interval.
        Deliver(reply, observer, 6.3);
        observer.Step(6.5); observer.Tap(4); Pump(6.5, authority, observer);
        Check(authority.Machine.State.Owner == 2 && observer.Machine.State.Owner == 2,
            "delayed restarted-authority handshake permits a new operator");
    }

    private static void PhysicalAndEditorInputs()
    {
        var c = new Client(); c.StartGame();
        int first = c.Machine.State.Sequence[0];
        c.Contact(first, c.Left, true);
        c.InputPhase();
        c.Contact(first, c.Left, true);
        Check(c.Machine.State.InputIndex == 0, "contact held across demonstration does not submit");
        c.Contact(first, c.Right, true); c.Contact(first, c.Left, false);
        Check(c.Machine.State.InputIndex == 0, "second collider cannot bypass occupied pad");
        c.Contact(first, c.Right, false); c.Step(c.Now + .12); c.Contact(first, c.Right, true);
        Check(c.Machine.State.Phase == CognitivePhase.Success, "either-hand fresh edge completes round");
        c.Contact(first, c.Right, false);
        byte prefix = c.Machine.State.Sequence[0]; c.InputPhase();
        Check(c.Machine.State.Round == 2 && c.Machine.State.Sequence[0] == prefix, "actual adapter preserves and extends prefix");
        foreach (int index in c.Machine.State.Sequence.ToArray())
        { c.Tap(index); c.Step(c.Now + .12); }
        Check(c.Machine.State.Phase == CognitivePhase.Complete, "Editor path reaches configured maximum");
        c.Step(c.Now + .12); c.Tap(4); c.InputPhase();
        c.Tap((c.Machine.State.Sequence[0] + 1) % 4);
        Check(c.Machine.State.Phase == CognitivePhase.Failure, "wrong input fails through adapter");
        Check(c.Machine.audioSource.Played.Last() == c.Machine.clips[5], "failure clip dispatched once");
        c.Step(c.Now + .12); c.Tap(4);
        Check(c.Machine.State.Round == 1 && c.Machine.State.Phase == CognitivePhase.Demonstrating, "immediate physical-control restart");
    }

    private static void ContactIdentityAndCleanup()
    {
        var c = new Client(); c.StartGame(); c.InputPhase();
        int correct = c.Machine.State.Sequence[0]; var pad = c.Machine.pads[correct];
        c.Rig.leftHandTransform.gameObject.tag = "HandTag";
        var broadGrab = c.Rig.leftHandTransform.gameObject.AddComponent<SphereCollider>();
        c.Contact(correct, broadGrab, true);
        var remote = new GameObject("Remote"); remote.tag = "HandTag"; remote.AddComponent<LocalRigMarker>();
        var remoteCollider = remote.AddComponent<BoxCollider>(); c.Contact(correct, remoteCollider, true);
        var forged = Finger(c.Rig.rightHandTransform); forged.gameObject.AddComponent<PhotonView>().IsMine = false;
        c.Contact(correct, forged, true);
        Check(c.Machine.State.InputIndex == 0, "root-grab, remote rig and foreign Photon contacts rejected");
        Call(pad, "OnTriggerStay", c.Left);
        Check(c.Machine.State.InputIndex == 0, "new trigger-stay overlap is not a fresh press");
        c.Left.enabled = false; c.Step(c.Now + .1); c.Step(c.Now + .12);
        c.Contact(correct, c.Right, true);
        Check(c.Machine.State.Phase == CognitivePhase.Success, "disabled collider pruned without needing exit callback");
        c.Contact(correct, c.Right, false);
        c.InputPhase(); correct = c.Machine.State.Sequence[0];
        Call(c.Machine.pads[correct], "OnDisable"); Call(c.Machine.pads[correct], "OnEnable");
        Call(c.Machine.pads[correct], "OnTriggerStay", c.Right); c.Step(c.Now + .3);
        Check(c.Machine.State.InputIndex == 0, "re-enabled overlapping pad still requires exit");
        c.Contact(correct, c.Right, false); c.Step(c.Now + .12); c.Contact(correct, c.Right, true);
        Check(c.Machine.State.InputIndex == 1, "re-enabled pad accepts subsequent fresh edge");
    }

    private static void LifecycleAndPresence()
    {
        var c = new Client(); c.StartGame(); c.Machine.editorControls = false;
        c.Select(c.Now); c.Room = new Room();
        var player = new Photon.Realtime.Player { ActorNumber = 1, IsLocal = true };
        player.CustomProperties[SectorPresence.PropertyKey] = 1; c.Room.Players.Add(1, player);
        c.Step(c.Now + .1); c.Step(c.Now + .12);
        c.Contact(4, c.Right, true); c.Contact(4, c.Right, false);
        Check(c.Machine.State.Owner == 1, "normal rig-distance path acquires operator");
        c.Rig.headCollider.transform.position = new Vector3(20, 0, 0); c.Step(c.Now + .1);
        Check(c.Machine.State.Owner == 0, "walking away releases operator");
        c.Machine.editorControls = true; c.StartGame(); Call(c.Machine, "OnApplicationPause", true);
        Check(c.Machine.State.Owner == 0, "pause releases own operator");
        c.Step(c.Now + .1); c.Tap(4); Check(c.Machine.State == null, "paused client cannot restart");
        Call(c.Machine, "OnApplicationPause", false); c.StartGame();
        c.Room = null; c.Step(c.Now + .1);
        Check(c.Machine.State.Owner == 0, "room identity change discards old session");
        c.StartGame(); c.Select(c.Now); Call(c.Machine, "OnDisable");
        Check(!PhotonNetwork.Callbacks.Contains(c.Machine) && c.Machine.State == null, "disable releases callback and replica state");
        Call(c.Machine, "OnEnable"); c.Step(c.Now + .1);
        Check(c.Machine.State.Owner == 0, "re-enable creates available machine");
    }

    private static void NetworkOwnershipAndRecovery()
    {
        var a = new Client(1, true); var b = new Client(2, true);
        a.Step(0); b.Step(0); Pump(0, a, b);
        a.Step(.2); b.Step(.2); b.Tap(4); Pump(.2, a, b);
        Check(a.Machine.State.Owner == 2 && b.Machine.State.Owner == 2, "network operator assigned consistently");
        var old = b.Machine.State;
        a.Tap(4); Pump(.3, a, b);
        Check(a.Machine.State.Session == old.Session && a.Machine.State.Owner == 2, "spectator cannot restart operator");
        for (double t = .4; t < 2; t += .1) { a.Step(t); b.Step(t); Pump(t, a, b); }
        int index = b.Machine.State.Sequence[0]; a.Tap(index); Pump(2, a, b);
        Check(a.Machine.State.InputIndex == 0, "spectator cannot add sequence input");
        b.Tap(index); Pump(2.1, a, b);
        Check(a.Machine.State.Phase == CognitivePhase.Success && b.Machine.State.Phase == CognitivePhase.Success, "network correct input advances both clients");
        b.Step(2.3); b.Tap(4); var restart = Take(188, 2);
        // Keep the exact authoritative epoch/serial but use the previous session.
        object[] release = (object[])((object[])restart.Data).Clone();
        Deliver(restart, a, 2.3); Pump(2.3, a, b);
        release[3] = 4; release[5] = old.Session; release[7] = (int)release[7] + 100;
        a.Select(2.4); a.Machine.OnEvent(new EventData { Code = 188, Sender = 2, CustomData = release });
        Check(a.Machine.State.Owner == 2 && a.Machine.State.Session == old.Session + 1, "old-session release cannot cancel restarted game");
        // Do not advance B: the actual authority-side heartbeat lease must expire.
        a.Step(7); Pump(7, a, b);
        Check(a.Machine.State.Owner == 0, "abandoned remote operator expires");
        b.Step(7.2); b.Step(7.4); b.Tap(4); Pump(7.4, a, b);
        a.Room.GetPlayer(2).CustomProperties[SectorPresence.PropertyKey] = 2;
        a.Step(7.5); Check(a.Machine.State.Owner == 0, "operator sector change releases immediately");
        a.Room.GetPlayer(2).CustomProperties[SectorPresence.PropertyKey] = 1;
        b.Room.GetPlayer(1).CustomProperties[SectorPresence.PropertyKey] = 0;
        PhotonNetwork.Outbox.Clear(); b.Step(8);
        Check(b.Machine.State.Owner == 0, "authority change establishes available machine");
        var state = b.Machine.State;
        b.Machine.OnEvent(new EventData { Code = 189, Sender = 1,
            CustomData = new object[] { "rc.cognitive.1", "hub-cognitive-01", 1, "old-term", "", old.Pack() } });
        Check(b.Machine.State == state, "departed authority packet rejected");
        b.Step(8.2); b.Tap(4); Check(b.Machine.IsOperator, "new authority can operate after handoff");
    }

    private static void MalformedAndUnrelatedEvents()
    {
        var c = new Client(1, true); c.Step(0); var before = c.Machine.State;
        object[][] packets = { Array.Empty<object>(), new object[] { "another-toy", "hub-cognitive-01", 1 },
            new object[] { "rc.cognitive.1", "other-machine", 1 }, new object[] { "rc.cognitive.1", "hub-cognitive-01", 2 },
            new object[] { "rc.cognitive.1", "hub-cognitive-01", 1 },
            new object[] { "rc.cognitive.1", "hub-cognitive-01", 1, "epoch", "echo", new object[13] } };
        foreach (var data in packets)
            foreach (byte code in new byte[] { 188, 189, 187 })
            { c.Machine.OnEvent(new EventData { Code = code, Sender = 2, CustomData = data }); Check(c.Machine.State == before, "unrelated/malformed packet ignored"); }
        c.Machine.OnEvent(new EventData { Code = 189, Sender = 2, CustomData = null });
        Check(c.Machine.State == before, "null packet payload ignored");
    }
    private static int Main()
    {
        int failures = 0;
        foreach (Action test in new Action[] { DelayedSynchronization, PhysicalAndEditorInputs, ContactIdentityAndCleanup,
                     LifecycleAndPresence, NetworkOwnershipAndRecovery, MalformedAndUnrelatedEvents })
        {
            PhotonNetwork.Outbox.Clear();
            try { test(); Console.WriteLine("PASS: " + test.Method.Name); }
            catch (Exception ex) { failures++; Console.WriteLine("FAIL: " + test.Method.Name + ": " + ex.GetBaseException().Message); }
        }
        Console.WriteLine(assertions + " managed adapter assertions; " + failures + " failing groups.");
        Console.WriteLine("Production machine/pad callbacks with managed doubles. No Unity/PhysX/Photon/headset execution.");
        return failures == 0 ? 0 : 1;
    }
}
