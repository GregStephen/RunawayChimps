// Instrumented API doubles for managed adapter control-flow tests only. These are NOT Unity,
// Photon transport, XR tracking, native physics, acoustic output, or an API compatibility proof.
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class Object { }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.components.OfType<T>().FirstOrDefault();
        public T GetComponentInParent<T>() where T : Component
        {
            for (Transform t = transform; t != null; t = t.parent)
            { T found = t.GetComponent<T>(); if (found != null) return found; }
            return null;
        }
    }
    public class GameObject : Object
    {
        public readonly List<Component> components = new List<Component>();
        public readonly Transform transform;
        public bool activeInHierarchy = true;
        public SceneManagement.Scene scene = SceneManagement.SceneManager.GetActiveScene();
        public GameObject() { transform = new Transform { gameObject = this }; components.Add(transform); }
        public T AddComponent<T>() where T : Component, new()
        { var value = new T { gameObject = this }; components.Add(value); return value; }
    }
    public class MonoBehaviour : Component
    { public bool enabled = true; public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy; }
    public class Transform : Component
    {
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
        public Vector3 position { get => parent == null ? localPosition : parent.TransformPoint(localPosition); set => localPosition = parent == null ? value : parent.InverseTransformPoint(value); }
        public Quaternion rotation { get => parent == null ? localRotation : parent.rotation * localRotation; set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value; }
        public Vector3 lossyScale => parent == null ? localScale : Vector3.Scale(parent.lossyScale, localScale);
        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 TransformPoint(Vector3 value) => position + TransformVector(value);
        public Vector3 TransformVector(Vector3 value) => rotation * Vector3.Scale(value, lossyScale);
        public Vector3 InverseTransformPoint(Vector3 value)
        {
            Vector3 p = Quaternion.Inverse(rotation) * (value - position), s = lossyScale;
            return new Vector3(p.x / s.x, p.y / s.y, p.z / s.z);
        }
        public bool IsChildOf(Transform other) { for (Transform t = this; t != null; t = t.parent) if (t == other) return true; return false; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 zero => new Vector3(0, 0, 0);
        public float magnitude => (float)Math.Sqrt(x*x+y*y+z*z);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x,-a.y,-a.z);
        public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x*f,a.y*f,a.z*f);
        public static Vector3 operator /(Vector3 a, float f) => a*(1/f);
        public static float Dot(Vector3 a, Vector3 b) => a.x*b.x+a.y*b.y+a.z*b.z;
        public static float Distance(Vector3 a, Vector3 b) => (a-b).magnitude;
    }
    public struct Quaternion
    {
        public float x,y,z,w;
        public Quaternion(float x,float y,float z,float w) { this.x=x;this.y=y;this.z=z;this.w=w; }
        public static Quaternion identity => new Quaternion(0,0,0,1);
        private System.Numerics.Quaternion Raw => new System.Numerics.Quaternion(x,y,z,w);
        private static Quaternion From(System.Numerics.Quaternion q) => new Quaternion(q.X,q.Y,q.Z,q.W);
        public static Quaternion Inverse(Quaternion q) => From(System.Numerics.Quaternion.Inverse(q.Raw));
        public static Quaternion operator *(Quaternion a,Quaternion b) => From(a.Raw*b.Raw);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        { var r=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(v.x,v.y,v.z),q.Raw); return new Vector3(r.X,r.Y,r.Z); }
        public static float Angle(Quaternion a,Quaternion b) => (float)(Math.Acos(Math.Min(1,Math.Abs(System.Numerics.Quaternion.Dot(a.Raw,b.Raw))))*360/Math.PI);
    }
    public class Collider : Component { }
    public class SphereCollider : Collider { }
    public class BoxCollider : Collider { public bool isTrigger; public Vector3 size, center; }
    public class AudioClip : Object { public float length = 0.1f; }
    public class AudioSource : MonoBehaviour
    {
        public bool playOnAwake, loop;
        public float spatialBlend = 1, volume, pitch;
        public AudioClip clip;
        public double? scheduled;
        public int playCalls, stops;
        public void Stop() { stops++; scheduled = null; }
        public void PlayScheduled(double at) { playCalls++; scheduled = at; }
    }
    public static class AudioSettings
    {
        public static double dspTime;
        public static event Action<bool> OnAudioConfigurationChanged;
        public static void Change() => OnAudioConfigurationChanged?.Invoke(true);
    }
    public static class Time { public static double unscaledTimeAsDouble; }
    public static class Application { public static bool isPlaying = true; }
    public static class Debug
    {
        public static readonly List<string> Errors = new List<string>();
        public static void LogError(string text, Object context = null) => Errors.Add(text);
        public static void LogWarning(string text, Object context = null) { }
    }
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public static float Clamp(float v,float a,float b) => Math.Max(a,Math.Min(b,v));
        public static float Clamp01(float v) => Clamp(v,0,1);
        public static float Max(float a,float b) => Math.Max(a,b);
        public static float Abs(float v) => Math.Abs(v);
        public static float Sin(float v) => (float)Math.Sin(v);
    }
    public static class SubsystemManager
    {
        public static XR.XRInputSubsystem Input = new XR.XRInputSubsystem();
        public static void GetInstances<T>(List<T> target) where T : class { target.Clear(); if (Input is T value) target.Add(value); }
    }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string name) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a,float b) { } }
    public class DisallowMultipleComponent : Attribute { }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public int id;
        public static bool operator ==(Scene a,Scene b) => a.id == b.id;
        public static bool operator !=(Scene a,Scene b) => a.id != b.id;
        public override bool Equals(object other) => other is Scene s && s.id == id;
        public override int GetHashCode() => id;
    }
    public static class SceneManager { public static Scene Active = new Scene { id = 1 }; public static Scene GetActiveScene() => Active; }
}
namespace UnityEngine.XR
{
    public enum XRNode { LeftHand, RightHand }
    [Flags] public enum InputTrackingState { None=0, Position=1, Rotation=2 }
    public struct InputFeatureUsage<T> { }
    public static class CommonUsages { public static InputFeatureUsage<bool> isTracked; public static InputFeatureUsage<InputTrackingState> trackingState; }
    public struct InputDevice
    {
        public bool isValid, tracked;
        public bool TryGetFeatureValue(InputFeatureUsage<bool> key,out bool value) { value=tracked;return isValid; }
        public bool TryGetFeatureValue(InputFeatureUsage<InputTrackingState> key,out InputTrackingState value) { value=tracked ? InputTrackingState.Position : InputTrackingState.None;return isValid; }
    }
    public static class InputDevices
    {
        public static InputDevice Device = new InputDevice { isValid=true, tracked=true };
        public static InputDevice GetDeviceAtXRNode(XRNode node) => Device;
    }
    public class XRInputSubsystem { public event Action<XRInputSubsystem> trackingOriginUpdated; public void Recenter() => trackingOriginUpdated?.Invoke(this); }
}
namespace ExitGames.Client.Photon
{
    public sealed class Hashtable : Dictionary<string,object> { }
    public class EventData { public byte Code; public int Sender; public object CustomData; }
    public struct SendOptions { public bool Reliability; public static SendOptions SendReliable => new SendOptions { Reliability=true }; }
}
namespace Photon.Realtime
{
    public class Player
    { public int ActorNumber; public bool IsLocal, IsInactive; public ExitGames.Client.Photon.Hashtable CustomProperties = new ExitGames.Client.Photon.Hashtable(); }
    public class Room
    {
        public string Name = "test-room";
        public Dictionary<int,Player> Players = new Dictionary<int,Player>();
        public Player GetPlayer(int number) => Players.TryGetValue(number,out Player value) ? value : null;
    }
    public enum EventCaching { DoNotCache, AddToRoomCache }
    public class RaiseEventOptions { public int[] TargetActors; public EventCaching CachingOption; }
    public enum DisconnectCause { None }
    public interface IOnEventCallback { void OnEvent(ExitGames.Client.Photon.EventData data); }
}
namespace Photon.Pun
{
    using Photon.Realtime;
    using ExitGames.Client.Photon;
    public static class PhotonNetwork
    {
        public static Room CurrentRoom;
        public static bool InRoom => CurrentRoom != null;
        public static Player LocalPlayer;
        public static Player[] PlayerList => CurrentRoom == null ? Array.Empty<Player>() : CurrentRoom.Players.Values.ToArray();
        public static double Time;
        public static bool FailNextSend;
        public static readonly List<(byte code,object[] data,RaiseEventOptions options,SendOptions send)> Sent = new List<(byte,object[],RaiseEventOptions,SendOptions)>();
        public static bool RaiseEvent(byte code,object data,RaiseEventOptions options,SendOptions send)
        { if (FailNextSend) { FailNextSend=false;return false; } Sent.Add((code,(object[])data,options,send));return true; }
    }
    public class MonoBehaviourPunCallbacks : UnityEngine.MonoBehaviour
    {
        public virtual void OnEnable() { }
        public virtual void OnDisable() { }
        public virtual void OnLeftRoom() { }
        public virtual void OnJoinedRoom() { }
        public virtual void OnDisconnected(DisconnectCause cause) { }
        public virtual void OnPlayerLeftRoom(Player player) { }
        public virtual void OnPlayerEnteredRoom(Player player) { }
        public virtual void OnPlayerPropertiesUpdate(Player player, Hashtable changed) { }
        public virtual void OnMasterClientSwitched(Player player) { }
    }
}
public class BlockHandSurfaceAudio : UnityEngine.MonoBehaviour { }
public class LocalRigMarker : UnityEngine.MonoBehaviour { }
public class AppState { public static AppState I; public bool IsReady = true; }
public class LoadingFlow { public static bool IsColdStartupPresentationActive; }
namespace RunawayChimps.Travel
{ public class SectorTravelService { public static SectorTravelService I; public bool IsBusy; public SectorId CurrentSector = SectorId.Hub; } }
namespace GorillaLocomotion
{
    public class Player : UnityEngine.MonoBehaviour
    {
        public static Player Instance;
        public bool disableMovement;
        public UnityEngine.SphereCollider headCollider;
        public UnityEngine.Transform leftHandTransform,rightHandTransform;
        public UnityEngine.Vector3 leftHandOffset,rightHandOffset;
        public float maxArmLength = 1.5f;
    }
}
