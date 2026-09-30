// Instrumented managed collaborators ONLY. They do not implement Unity native audio,
// rendering, object lifetime or Photon transport. Production adapters are linked unchanged.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public static void Destroy(Object value)
        {
            if (value is GameObject go) go.SetActive(false);
            else if (value is Component c) c.gameObject.SetActive(false);
        }
        public static T Instantiate<T>(T template, Transform parent, bool world) where T : Object
        {
            if (template is RunawayChimps.FacilityAnnouncements.FacilityAnnouncementCaptions)
            {
                var copy = HarnessObjects.Captions();
                copy.transform.SetParent(parent, world);
                return (T)(Object)copy;
            }
            throw new NotSupportedException("Unmodelled Instantiate");
        }
        public static void DontDestroyOnLoad(Object _) { }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponentInParent<T>() where T : Component
        {
            for (Transform p = transform; p != null; p = p.parent)
            { T value = p.gameObject.GetComponent<T>(); if (value != null) return value; }
            return null;
        }
    }
    public class Behaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled && gameObject != null && gameObject.activeInHierarchy;
    }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { }
    public class GameObject : Object
    {
        private readonly List<Component> components = new List<Component>();
        public string name;
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public int layer;
        public SceneManagement.Scene scene = SceneManagement.SceneManager.GetActiveScene();
        public Transform transform;
        public GameObject(string name = "test")
        { this.name = name; transform = new Transform { gameObject = this }; components.Add(transform); }
        public T Attach<T>(T value) where T : Component { value.gameObject = this; components.Add(value); return value; }
        public T AddComponent<T>() where T : Component, new()
        {
            T value = Attach(new T());
            HarnessObjects.CallOptional(value, "Awake");
            HarnessObjects.CallOptional(value, "OnEnable");
            return value;
        }
        public T GetComponent<T>() where T : Component
        { foreach (Component c in components) if (c is T value) return value; return null; }
        public void SetActive(bool active) => activeSelf = active;
    }
    public class Transform : Component
    {
        public Transform parent;
        public Vector3 position;
        public void SetParent(Transform value, bool _) => parent = value;
    }
    public class RectTransform : Transform
    { public Vector2 anchorMin, anchorMax, offsetMin, offsetMax; }
    public class Camera : Behaviour { public float nearClipPlane = 0.1f; }
    public class Canvas : Behaviour
    { public Camera worldCamera; public RenderMode renderMode; public float planeDistance; public bool overrideSorting; public int sortingOrder; }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public struct Vector2
    { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero => default; }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public float sqrMagnitude => x*x+y*y+z*z;
        public static float Distance(Vector3 a, Vector3 b) => (float)Math.Sqrt((a-b).sqrMagnitude);
    }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a,b);
        public static float Min(float a, float b) => Math.Min(a,b);
        public static float Clamp(float v, float a, float b) => v<a?a:v>b?b:v;
    }
    public enum AudioDataLoadState { Unloaded, Loading, Loaded, Failed }
    public enum AudioClipLoadType { DecompressOnLoad, CompressedInMemory, Streaming }
    public class AudioClip : Object
    {
        public float length = 5;
        public AudioDataLoadState loadState = AudioDataLoadState.Loaded;
        public AudioClipLoadType loadType = AudioClipLoadType.DecompressOnLoad;
        public bool preloadAudioData = true;
    }
    public enum AudioRolloffMode { Logarithmic, Linear, Custom }
    public class AudioSource : Behaviour
    {
        public bool playOnAwake, loop, mute, ignoreListenerPause, ignoreListenerVolume;
        public bool isPlaying;
        public float pitch, spatialBlend, dopplerLevel, minDistance, maxDistance, volume;
        public int priority;
        public AudioRolloffMode rolloffMode;
        public Audio.AudioMixerGroup outputAudioMixerGroup;
        public AudioClip clip;
        public int Plays, Stops;
        public void Play() { isPlaying = true; ++Plays; }
        public void Stop() { isPlaying = false; ++Stops; }
    }
    public static class AudioListener { public static bool pause; public static float volume = 1; }
    public static class Application { public static bool isPlaying = true; }
    public static class Time { public static float timeScale = 1, unscaledTime; }
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, object> values = new Dictionary<string, object>();
        public static int GetInt(string k, int d) => values.TryGetValue(k,out var v)?(int)v:d;
        public static float GetFloat(string k, float d) => values.TryGetValue(k,out var v)?(float)v:d;
        public static void SetInt(string k,int v) => values[k]=v;
        public static void SetFloat(string k,float v) => values[k]=v;
        public static void Clear() => values.Clear();
    }
    public static class Debug { public static void LogWarning(string _, Object context) { } }
    [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class CreateAssetMenuAttribute : Attribute { public string menuName; }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string _) { } }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string _) { } }
    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float a,float b) { } }
    [AttributeUsage(AttributeTargets.Field)] public class MinAttribute : Attribute { public MinAttribute(float _) { } }
    [AttributeUsage(AttributeTargets.Field)] public class TextAreaAttribute : Attribute { public TextAreaAttribute(int a,int b) { } }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType _) { } }
}
namespace UnityEngine.Audio { public class AudioMixerGroup : UnityEngine.Object { } }
namespace UnityEngine.UI { public class Image : UnityEngine.Behaviour { public bool raycastTarget; } }
namespace TMPro { public class TMP_Text : UnityEngine.Behaviour { public string text; public float fontSize; public bool richText, raycastTarget; } }
namespace UnityEngine.SceneManagement
{
    public struct Scene : IEquatable<Scene>
    {
        public int handle;
        public bool isLoaded;
        public static bool operator ==(Scene a,Scene b)=>a.handle==b.handle;
        public static bool operator !=(Scene a,Scene b)=>!(a==b);
        public bool Equals(Scene other)=>this==other;
        public override bool Equals(object other)=>other is Scene s && this==s;
        public override int GetHashCode()=>handle;
    }
    public static class SceneManager { public static Scene Active = new Scene {handle=1,isLoaded=true}; public static Scene GetActiveScene()=>Active; }
}
namespace ExitGames.Client.Photon
{
    public class Hashtable : Dictionary<object,object> { }
    public class EventData { public byte Code; public int Sender; public object CustomData; }
    public struct SendOptions { public static SendOptions SendReliable => default; }
}
namespace Photon.Realtime
{
    using ExitGames.Client.Photon;
    public interface IOnEventCallback { void OnEvent(EventData message); }
    public enum EventCaching { DoNotCache }
    public class RaiseEventOptions { public int[] TargetActors; public EventCaching CachingOption; }
    public class Player
    {
        public int ActorNumber;
        public bool IsLocal, IsInactive;
        public Hashtable CustomProperties = new Hashtable();
        public int PropertyWrites;
        public bool SetCustomProperties(Hashtable values)
        { ++PropertyWrites; foreach(var p in values) CustomProperties[p.Key]=p.Value; return true; }
    }
    public class Room
    {
        public Hashtable CustomProperties = new Hashtable();
        public int PropertyWrites;
        public bool SetCustomProperties(Hashtable values)
        { ++PropertyWrites; foreach(var p in values) CustomProperties[p.Key]=p.Value; return true; }
    }
}
namespace Photon.Pun
{
    using Photon.Realtime;
    using ExitGames.Client.Photon;
    public class PhotonView : UnityEngine.Component { public bool IsMine = true; }
    public static class PhotonNetwork
    {
        public static bool InRoom, IsMessageQueueRunning = true;
        public static Room CurrentRoom;
        public static Player LocalPlayer;
        public static Player[] PlayerList = Array.Empty<Player>();
        public static double Time;
        public static bool SendSucceeds = true;
        public static readonly List<EventData> Sent = new List<EventData>();
        public static void AddCallbackTarget(object _) { }
        public static void RemoveCallbackTarget(object _) { }
        public static bool RaiseEvent(byte code,object data,RaiseEventOptions options,SendOptions send)
        {
            if (!SendSucceeds) return false;
            Sent.Add(new EventData {Code=code, CustomData=data,Sender=LocalPlayer.ActorNumber});
            return true;
        }
    }
}
namespace Unity.XR.CoreUtils { public class XROrigin : UnityEngine.Component { public UnityEngine.Camera Camera; } }
namespace GorillaLocomotion { public class Player : UnityEngine.Behaviour { public static Player Instance; } }
public class LocalRigMarker : UnityEngine.Component { }
public class AppState { public static AppState I; public bool IsReady = true; }
namespace RunawayChimps.Travel
{ public class SectorTravelService { public static SectorTravelService I; public bool IsBusy; public SectorId CurrentSector = SectorId.Hub; } }
internal static class HarnessObjects
{
    internal static void CallOptional(object target,string method,params object[] args)
    { target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(target,args); }
    internal static RunawayChimps.FacilityAnnouncements.FacilityAnnouncementCaptions Captions()
    {
        var root=new UnityEngine.GameObject("captions");
        var panel=new UnityEngine.GameObject("panel"); panel.transform.SetParent(root.transform,false);
        var text=new UnityEngine.GameObject("label"); text.transform.SetParent(panel.transform,false);
        return root.Attach(new RunawayChimps.FacilityAnnouncements.FacilityAnnouncementCaptions {
            canvas=root.Attach(new UnityEngine.Canvas()),panel=panel.Attach(new UnityEngine.RectTransform()),
            label=text.Attach(new TMPro.TMP_Text()),backdrop=panel.Attach(new UnityEngine.UI.Image()) });
    }
}
