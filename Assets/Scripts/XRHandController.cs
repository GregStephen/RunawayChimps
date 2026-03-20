using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;

public enum HandType
{
    Left,
    Right
}

public class XRHandController : MonoBehaviour
{
    public HandType handType;
    public float thumbMoveSpeed = 0.1f;
    public PhotonView view;

    private Animator animator;
    private InputDevice inputDevice;

    private float pose1Value;
    private float pose2Value;
    private float pose3Value;

    private float retryTimer;
    private const float RetryInterval = 0.5f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        TryAcquireDevice();
        InputDevices.deviceConnected += OnDeviceChanged;
        InputDevices.deviceDisconnected += OnDeviceChanged;
    }

    private void OnDisable()
    {
        InputDevices.deviceConnected -= OnDeviceChanged;
        InputDevices.deviceDisconnected -= OnDeviceChanged;
    }

    private void Update()
    {
        // Only animate local hand
        if (view != null && !view.IsMine)
            return;

        if (!inputDevice.isValid)
        {
            retryTimer -= Time.deltaTime;
            if (retryTimer <= 0f)
            {
                retryTimer = RetryInterval;
                TryAcquireDevice();
            }
            return;
        }

        AnimateHand();
    }

    private void OnDeviceChanged(InputDevice _)
    {
        // Re-acquire whenever devices change
        TryAcquireDevice();
    }

    private void TryAcquireDevice()
    {
        // Most reliable path in OpenXR
        XRNode node = (handType == HandType.Left) ? XRNode.LeftHand : XRNode.RightHand;
        inputDevice = InputDevices.GetDeviceAtXRNode(node);

        if (inputDevice.isValid)
            return;

        // Fallback search by characteristics
        InputDeviceCharacteristics controllerCharacteristic =
            InputDeviceCharacteristics.HeldInHand |
            InputDeviceCharacteristics.Controller |
            ((handType == HandType.Left) ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);

        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(controllerCharacteristic, devices);

        if (devices.Count > 0)
            inputDevice = devices[0];
    }

    private void AnimateHand()
    {
        inputDevice.TryGetFeatureValue(CommonUsages.trigger, out pose1Value);
        inputDevice.TryGetFeatureValue(CommonUsages.grip, out pose2Value);

        inputDevice.TryGetFeatureValue(CommonUsages.primaryTouch, out bool primaryTouched);
        inputDevice.TryGetFeatureValue(CommonUsages.secondaryTouch, out bool secondaryTouched);

        if (primaryTouched || secondaryTouched) pose3Value += thumbMoveSpeed;
        else pose3Value -= thumbMoveSpeed;

        pose3Value = Mathf.Clamp(pose3Value, 0, 1);

        animator.SetFloat("pose1", pose1Value);
        animator.SetFloat("pose2", pose2Value);
        animator.SetFloat("pose3", pose3Value);
    }
}
