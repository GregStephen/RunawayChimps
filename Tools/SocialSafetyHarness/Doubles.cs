// Diagnostic doubles only: no claim about Unity physics/rendering or live service delivery.
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
    public class Object
    {
        public static int ParentInstantiations;
        public static GameObject LastInstance;
        public static void Destroy(Object o) { }
        public static void DontDestroyOnLoad(Object o) { }
        public static T Instantiate<T>(T t) where T:Object => t;
        public static GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            if (worldPositionStays) throw new Exception("Hub must use authored local placement");
            ParentInstantiations++;
            LastInstance = new GameObject(prefab.name);
            LastInstance.transform.parent = parent;
            parent.children.Add(LastInstance.transform);
            LastInstance.AddComponent<RunawayChimps.SocialSafety.PlayerBoard>();
            return LastInstance;
        }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T:class => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>() where T:class => GetComponent<T>();
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class => gameObject.GetComponentsInChildren<T>(inactive);
    }
    public class Behaviour : Component { public bool enabled = true; public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy; }
    public class MonoBehaviour : Behaviour { }
    public class GameObject : Object
    {
        public readonly Dictionary<Type,object> components = new Dictionary<Type,object>();
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf;
        public int layer;
        public string name;
        public string tag;
        public Transform transform = new Transform();
        public GameObject(string name = "") { this.name=name; transform.gameObject = this; }
        public T AddComponent<T>() where T:Component,new() { var t=new T {gameObject=this}; components[typeof(T)]=t; return t; }
        public T GetComponent<T>() where T:class => components.Values.OfType<T>().FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool inactive) where T:class => components.Values.OfType<T>().Concat(transform.children.SelectMany(c=>c.gameObject.GetComponentsInChildren<T>(inactive))).ToArray();
        public void SetActive(bool active) { activeSelf=active; }
    }
    public class Transform : Component { public Transform parent; public List<Transform> children=new List<Transform>(); public Vector3 position; public Vector3 forward=Vector3.forward; public Vector3 localScale; public void SetPositionAndRotation(Vector3 p, Quaternion q) { position=p; } }
    public class Collider : Behaviour { public bool CompareTag(string tag) => gameObject.tag == tag; }
    public class BoxCollider : Collider { public bool isTrigger; }
    public class Renderer : Behaviour { }
    public class AudioSource : Behaviour { public bool mute; }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 up => new Vector3(0,1,0);
        public static Vector3 one => new Vector3(1,1,1);
        public static Vector3 forward => new Vector3(0,0,1);
        public float sqrMagnitude => x*x+y*y+z*z;
        public Vector3 normalized => this;
        public static Vector3 ProjectOnPlane(Vector3 a,Vector3 b) => a;
        public static Vector3 operator +(Vector3 a,Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b) => a+-b;
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x,-a.y,-a.z);
        public static Vector3 operator *(Vector3 a,float b) => new Vector3(a.x*b,a.y*b,a.z*b);
    }
    public struct Quaternion { public static Quaternion LookRotation(Vector3 a,Vector3 b) => new Quaternion(); }
    public struct Color { public Color(float r,float g,float b) { } }
    public static class Mathf { public static int Max(int a,int b)=>Math.Max(a,b); public static int Clamp(int n,int a,int b)=>Math.Max(a,Math.Min(n,b)); }
    public static class Time { public static float realtimeSinceStartup; public static float unscaledTime; }
    public static class Application { public static string version="test-build"; public static bool isPlaying=true; }
    public static class JsonUtility { public static string ToJson(object o)=>System.Text.Json.JsonSerializer.Serialize(o,new System.Text.Json.JsonSerializerOptions {IncludeFields=true}); }
    public static class Resources { public static Dictionary<string,object> Assets=new Dictionary<string,object>(); public static T Load<T>(string path) where T:class => Assets.TryGetValue(path,out var value)?value as T:null; }
    public static class Debug { public static void LogError(string text) { } }
    public static class LayerMask { public static int NameToLayer(string name) => name=="FingerTip"?28:name=="Left Hand"?24:name=="Right Hand"?25:-1; }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public sealed class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int t) { } }
    public sealed class ContextMenu : Attribute { public ContextMenu(string t) { } }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name;
        public bool isLoaded;
        public UnityEngine.GameObject[] roots;
        public bool IsValid()=>name!=null;
        public UnityEngine.GameObject[] GetRootGameObjects()=>roots;
    }
    public enum LoadSceneMode { Single, Additive }
    public static class SceneManager
    {
        public static List<Scene> Scenes=new List<Scene>();
        public static int sceneCount=>Scenes.Count;
        public static Scene GetSceneAt(int index)=>Scenes[index];
        public static event Action<Scene,LoadSceneMode> sceneLoaded;
        public static void Loaded(Scene scene)=>sceneLoaded?.Invoke(scene,LoadSceneMode.Additive);
    }
}
namespace UnityEngine.XR
{
    public enum XRNode { LeftHand }
    public static class CommonUsages { public static bool secondaryButton; }
    public struct InputDevice { public bool TryGetFeatureValue(bool feature,out bool result) { result=false;return false; } }
    public static class InputDevices { public static InputDevice GetDeviceAtXRNode(XRNode n)=>new InputDevice(); }
}
namespace TMPro { public class TMP_Text : UnityEngine.Component { public string text; public bool richText; public UnityEngine.Color color; } }
namespace ExitGames.Client.Photon { public class Hashtable : Dictionary<object,object> { } }
namespace Photon.Realtime
{
    public enum DisconnectCause { None }
    public class Player { public int ActorNumber; public bool IsLocal; public bool IsInactive; public string NickName; public ExitGames.Client.Photon.Hashtable CustomProperties=new ExitGames.Client.Photon.Hashtable(); public void SetCustomProperties(ExitGames.Client.Photon.Hashtable p) { foreach(var v in p) CustomProperties[v.Key]=v.Value; } }
    public class Room { public Dictionary<int,Player> Players=new Dictionary<int,Player>(); public int PlayerCount=>Players.Count; public Player GetPlayer(int actor)=>Players.TryGetValue(actor,out var p)?p:null; }
}
namespace Photon.Pun
{
    public class MonoBehaviourPunCallbacks : UnityEngine.MonoBehaviour
    {
        public virtual void OnJoinedRoom() { } public virtual void OnLeftRoom() { } public virtual void OnDisconnected(Photon.Realtime.DisconnectCause c) { }
        public virtual void OnPlayerEnteredRoom(Photon.Realtime.Player p) { } public virtual void OnPlayerLeftRoom(Photon.Realtime.Player p) { }
        public virtual void OnPlayerPropertiesUpdate(Photon.Realtime.Player p,ExitGames.Client.Photon.Hashtable h) { }
    }
    public static class PhotonNetwork
    {
        public static Photon.Realtime.Room CurrentRoom;
        public static bool InRoom=>CurrentRoom!=null;
        public static Photon.Realtime.Player LocalPlayer;
        public static Photon.Realtime.Player[] PlayerList=>CurrentRoom.Players.Values.ToArray();
    }
    public class PhotonView : UnityEngine.Component { public bool IsMine; public Photon.Realtime.Player Owner; }
}
namespace Photon.VR { public class PhotonVRManager { public static PhotonVRManager Manager; public UnityEngine.Transform Head; } }
namespace Photon.Voice.PUN { public class PhotonVoiceView : UnityEngine.Component { public UnityEngine.Component SpeakerInUse; } }
namespace PlayFab.ClientModels
{
    public class ReportPlayerClientRequest { public string ReporteeId; public string Comment; }
    public class ReportPlayerClientResult { public int SubmissionsRemaining; }
}
namespace PlayFab
{
    public class PlayFabError { }
    public static class PlayFabClientAPI
    {
        public static bool LoggedIn=true;
        public static bool Throws;
        public static int Calls;
        public static ClientModels.ReportPlayerClientRequest LastRequest;
        public static Action<ClientModels.ReportPlayerClientResult> Success;
        public static Action<PlayFabError> Failure;
        public static bool IsClientLoggedIn()=>LoggedIn;
        public static void ReportPlayer(ClientModels.ReportPlayerClientRequest request,Action<ClientModels.ReportPlayerClientResult> success,Action<PlayFabError> failure)
        { if(Throws) throw new Exception(); Calls++; LastRequest=request;Success=success;Failure=failure; }
    }
}
public class LocalRigMarker : UnityEngine.Component { }
namespace RunawayChimps.Travel { public class SectorTravelService { public static SectorTravelService I; public SectorId CurrentSector; public bool IsBusy; } }
