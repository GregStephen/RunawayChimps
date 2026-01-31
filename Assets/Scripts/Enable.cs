using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enable : MonoBehaviour
{
    public GameObject objecttoenable;
    public string HandTag = "HandTag";

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == HandTag)
        {
            objecttoenable.SetActive(true);
        }
    }
}
