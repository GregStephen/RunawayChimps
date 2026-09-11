using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TMPro;

namespace Photon.VR.Player
{
    public class PhotonVRPlayerName : MonoBehaviour
    {
        [Tooltip("How high the text should be above the players head")]
        public float Offset = 0.17f;
        public Transform Head;

        private void Update()
        {
            var mgr = PhotonVRManager.Manager;
            if (mgr == null || mgr.Head == null || Head == null)
                return;

            transform.position = Head.position + new Vector3(0, Offset, 0);

            Vector3 direction = PhotonVRManager.Manager.Head.position - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion quaternion = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, quaternion, 10 * Time.deltaTime);
        }
    }

}