using System.Collections.Generic;
using UnityEngine;

namespace Photon.VR.Saving
{
    public class PhotonVRValueSaver : MonoBehaviour
    {
        public static void SaveDictionary(string location, Dictionary<string, string> value)
        {
            value = value ?? new Dictionary<string, string>();
            foreach (var oldKey in PlayerPrefs.GetString(location, "").Split(','))
                if (!string.IsNullOrEmpty(oldKey) && !value.ContainsKey(oldKey))
                    PlayerPrefs.DeleteKey(location + oldKey);
            var keys = new List<string>();
            foreach (var pair in value)
            {
                if (string.IsNullOrEmpty(pair.Key)) continue;
                keys.Add(pair.Key);
                PlayerPrefs.SetString(location + pair.Key, pair.Value ?? "");
            }
            PlayerPrefs.SetString(location, string.Join(",", keys));
            PlayerPrefs.Save();
        }

        public static Dictionary<string, string> GetDictionary(string location)
        {
            var value = new Dictionary<string, string>();
            foreach (var key in PlayerPrefs.GetString(location, "").Split(','))
                if (!string.IsNullOrEmpty(key)) value[key] = PlayerPrefs.GetString(location + key, "");
            return value;
        }
    }
}
