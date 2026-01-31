using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;
using Photon.Realtime;

public enum HandTypeLeft
{
    Left
}

public class XRHandL : MonoBehaviour
{
    public HandTypeLeft handType;
    public float thumbMoveSpeed = 0.1f;

    private Animator animator;
    private InputDevice inputDevice;

    private float pose4Value;
    private float pose5Value;
    private float pose6Value;
    public PhotonView view;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        inputDevice = GetInputDevice();
    }

    // Update is called once per frame
    void Update()
    {
        if (view.IsMine)
        {
            AnimateHand();
        }
    }

    InputDevice GetInputDevice()
    {
        InputDeviceCharacteristics controllerCharacteristic = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller;

        if (handType == HandTypeLeft.Left)
        {
            controllerCharacteristic = controllerCharacteristic | InputDeviceCharacteristics.Left;
        }
        else
        {
            controllerCharacteristic = controllerCharacteristic | InputDeviceCharacteristics.Right;
        }

        List<InputDevice> inputDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(controllerCharacteristic, inputDevices);

        return inputDevices[0];
    }

    void AnimateHand()
    {
        inputDevice.TryGetFeatureValue(CommonUsages.trigger, out pose4Value);
        inputDevice.TryGetFeatureValue(CommonUsages.grip, out pose5Value);

        inputDevice.TryGetFeatureValue(CommonUsages.primaryTouch, out bool primaryTouched);
        inputDevice.TryGetFeatureValue(CommonUsages.secondaryTouch, out bool secondaryTouched);

        if (primaryTouched || secondaryTouched)
        {
            pose6Value += thumbMoveSpeed;
        }
        else
        {
            pose6Value -= thumbMoveSpeed;
        }

        pose6Value = Mathf.Clamp(pose6Value, 0, 1);

        animator.SetFloat("pose4", pose4Value);
        animator.SetFloat("pose5", pose5Value);
        animator.SetFloat("pose6", pose6Value);
    }
}