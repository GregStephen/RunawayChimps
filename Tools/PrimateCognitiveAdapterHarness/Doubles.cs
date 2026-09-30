// Managed diagnostic doubles only. These do not implement Unity physics, rendering,
// scheduling, serialization or Photon delivery. Tests invoke production callbacks.
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class Object
    {
        private static int serial;
        private readonly int id = ++serial;
        public static readonly List<Object> All = new List<Object>();
        public static int Context;
        public readonly int CreatedIn = Context;
        public string name = "";
        public Object() { All.Add(this); }
        public int GetInstanceID() => id;
        public static T[] FindObjectsOfType<T>(bool includeInactive = false) where T : Object =>
            All.OfType<T>().Where(x => x.CreatedIn == Context).ToArray();
    }
    public class GameObject : Object
    {
        public Transform transform;
        public bool activeInHierarchy = true;
        public string tag = "Untagged";
        public SceneManagement.Scene scene = new SceneManagement.Scene(1);
        private readonly List<Component> components = new List<Component>();
        public GameObject(string value = "") { name = value; transform = AddComponent<Transform>(); }
        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T { gameObject = this }; components.Add(c); return c;
        }
        public T GetComponent<T>() where T : class => components.OfType<T>().FirstOrDefault();
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>() where T : class
        {
            for (Transform t = transform; t != null; t = t.parent)
            { var c = t.gameObject.GetComponent<T>(); if (c != null) return c; }
            return null;
        }
        public bool CompareTag(string tag) => gameObject.tag == tag;
    }
    public class Behaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    }
    public class MonoBehaviour : Behaviour { }
    public class Transform : Component
    {
        public Transform parent;
        public Vector3 localPosition;
        // Test transforms are translation-only/unit-scale. Not a native transform proof.
        public Vector3 position { get => localPosition + (parent == null ? Vector3.zero : parent.position); set => localPosition = value; }
        public bool IsChildOf(Transform other)
        { for (Transform t = this; t != null; t = t.parent) if (t == other) return true; return false; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3();
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.x * b, a.y * b, a.z * b);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a * (1 - t) + b * t;
        public static float Distance(Vector3 a, Vector3 b) => (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));
    }
    public struct Color
    {
        public static Color gray => new Color();
        public static Color white => new Color();
        public static Color operator *(Color a, float b) => a;
    }
    public class MaterialPropertyBlock { public void SetColor(string key, Color value) { } }
    public class Renderer : Component
    { public void GetPropertyBlock(MaterialPropertyBlock b) { } public void SetPropertyBlock(MaterialPropertyBlock b) { } }
    public class Collider : Component { public bool enabled = true; public bool isTrigger; }
    public class BoxCollider : Collider { }
    public class SphereCollider : Collider { }
    public class AudioClip : Object { }
    public class AudioSource : Behaviour
    {
        public bool playOnAwake, loop, isPlaying;
        public float spatialBlend;
        public readonly List<AudioClip> Played = new List<AudioClip>();
        public void Stop() { isPlaying = false; }
        public void PlayOneShot(AudioClip clip) { Played.Add(clip); isPlaying = true; }
    }
    public static class Time { public static double unscaledTimeAsDouble; public static float unscaledDeltaTime = .1f; }
    public static class Mathf
    { public static float Max(float a, float b) => Math.Max(a, b); public static float Exp(float x) => (float)Math.Exp(x); }
    public static class Debug { public static void LogError(string message, Object context = null) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class RequireComponent : Attribute { public RequireComponent(Type type) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string text) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float lo, float hi) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class MinAttribute : Attribute { public MinAttribute(float lo) { } }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene : IEquatable<Scene>
    {
        public int handle;
        public Scene(int id) { handle = id; }
        public bool isLoaded => handle != 0;
        public static bool operator ==(Scene a, Scene b) => a.handle == b.handle;
        public static bool operator !=(Scene a, Scene b) => !(a == b);
        public bool Equals(Scene other) => this == other;
        public override bool Equals(object other) => other is Scene scene && Equals(scene);
        public override int GetHashCode() => handle;
    }
    public static class SceneManager { public static Scene Active = new Scene(1); public static Scene GetActiveScene() => Active; }
}
namespace TMPro { public class TMP_Text : UnityEngine.Behaviour { public bool richText; public string text = ""; } }
namespace ExitGames.Client.Photon
{
    public class EventData { public byte Code; public int Sender; public object CustomData; }
    public struct SendOptions { public static SendOptions SendReliable => new SendOptions(); }
}
namespace Photon.Realtime
{
    public interface IOnEventCallback { void OnEvent(ExitGames.Client.Photon.EventData data); }
    public class Player
    {
        public int ActorNumber;
        public bool IsLocal, IsInactive;
        public readonly Dictionary<object, object> CustomProperties = new Dictionary<object, object>();
    }
    public class Room
    {
        public readonly Dictionary<int, Player> Players = new Dictionary<int, Player>();
        public Player GetPlayer(int actor) => Players.TryGetValue(actor, out var p) ? p : null;
    }
    public class RaiseEventOptions { public int[] TargetActors; }
}
namespace Photon.Pun
{
    public class PhotonView : UnityEngine.Component { public bool IsMine; }
    public static class PhotonNetwork
    {
        public sealed class Sent { public int Sender; public byte Code; public object Data; public int[] Targets; }
        public static readonly List<Sent> Outbox = new List<Sent>();
        public static readonly HashSet<object> Callbacks = new HashSet<object>();
        public static Photon.Realtime.Room CurrentRoom;
        public static Photon.Realtime.Player LocalPlayer;
        public static Photon.Realtime.Player[] PlayerList => CurrentRoom == null ? Array.Empty<Photon.Realtime.Player>() : CurrentRoom.Players.Values.ToArray();
        public static bool InRoom => CurrentRoom != null;
        public static double Time;
        public static void AddCallbackTarget(object target) => Callbacks.Add(target);
        public static void RemoveCallbackTarget(object target) => Callbacks.Remove(target);
        public static bool RaiseEvent(byte code, object data, Photon.Realtime.RaiseEventOptions options, ExitGames.Client.Photon.SendOptions send)
        { Outbox.Add(new Sent { Sender = LocalPlayer.ActorNumber, Code = code, Data = data, Targets = options.TargetActors }); return true; }
    }
}
public class LocalRigMarker : UnityEngine.MonoBehaviour { }
public static class LoadingFlow { public static bool IsColdStartupPresentationActive; }
namespace GorillaLocomotion
{
    public class Player : UnityEngine.MonoBehaviour
    {
        public static Player Instance;
        public UnityEngine.SphereCollider headCollider;
        public UnityEngine.Transform leftHandTransform, rightHandTransform;
    }
}
namespace RunawayChimps.Travel
{
    public class SectorTravelService
    { public static SectorTravelService I; public bool IsBusy; public SectorId CurrentSector; }
}
