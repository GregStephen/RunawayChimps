using System;
using UnityEngine;

public class KeyBox : MonoBehaviour
{
    [Header("Settings")]
    public int keysNeeded = 2;

    [Header("Assigned in Inspector")]
    public SlidingDoor door;

    private int currentKeys = 0;

    public void AddKey()
    {
        currentKeys++;

        Debug.Log($"Key inserted! ({currentKeys}/{keysNeeded})");

        if (currentKeys >= keysNeeded)
        {
            Console.WriteLine("SHOULD OPEN DOOR");
            door.OpenDoor();
        }
    }
}
