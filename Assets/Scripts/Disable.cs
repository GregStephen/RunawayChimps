using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Disable : MonoBehaviour
{
    public GameObject objecttodisable;
    public string HandTag = "HandTag";

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == HandTag)
        {
            objecttodisable.SetActive(false);
        }
    }
}
