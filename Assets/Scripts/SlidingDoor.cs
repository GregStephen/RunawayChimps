using System;
using System.Collections;
using UnityEngine;

public class SlidingDoor : MonoBehaviour
{
    [Header("Door Panels")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Door Settings")]
    public float slideDistance = 1.0f;
    public float openSpeed = 2.0f;

    private bool isOpen = false;
    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;

    private void Start()
    {
        leftClosedPos = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;
    }

    public void OpenDoor()
    {
        if (!isOpen)
        {
            isOpen = true;
            StartCoroutine(OpenRoutine());
        }
    }

    private IEnumerator OpenRoutine()
    {
        Console.WriteLine("OPENING DOOR");
        Vector3 leftTarget = leftClosedPos + new Vector3(0, 0, slideDistance);
        Vector3 rightTarget = rightClosedPos + new Vector3(0, 0, -slideDistance);

        while (Vector3.Distance(leftDoor.localPosition, leftTarget) > 0.01f)
        {
            leftDoor.localPosition = Vector3.Lerp(leftDoor.localPosition, leftTarget, Time.deltaTime * openSpeed);
            rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, rightTarget, Time.deltaTime * openSpeed);
            yield return null;
        }
    }
}
