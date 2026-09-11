using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhotonVRPlayerBody : MonoBehaviour
{
    public Transform Head;
    public float Offset;

    private void Update()
    {
        if (Head == null) return;
        transform.rotation = Quaternion.Euler(0, Head.eulerAngles.y, 0);
        transform.position = new Vector3(Head.position.x, Head.position.y + Offset, Head.position.z);
    }
}
