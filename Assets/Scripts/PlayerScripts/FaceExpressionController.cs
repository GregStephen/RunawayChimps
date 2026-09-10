using UnityEngine;
using Photon.Voice.Unity;
using Photon.Pun; // Needed for ownership check

public class FaceExpressionController : MonoBehaviourPun
{
    [Header("Face Material")]
    public Material faceClosed;
    public Material[] talkingFaces;
    public Material faceScream;

    [Header("References")]
    public Recorder recorder;
    public Renderer faceRenderer; // The mesh that displays the face texture

    [Header("Thresholds")]
    [Range(0f, 1f)] public float talkThreshold = 0.02f;
    [Range(0f, 1f)] public float screamThreshold = 0.08f;

    private float changeTimer = 0f;
    private int currentTalkingIndex = 0;

    void Start()
    {
        if (faceRenderer != null)
        {
            Debug.Log($"{gameObject.name} has {faceRenderer.sharedMaterials.Length} materials");
            for (int i = 0; i < faceRenderer.sharedMaterials.Length; i++)
            {
                Debug.Log($"Material {i}: {faceRenderer.sharedMaterials[i].name}");
            }
        }
    }
    void Update()
    {
        // Only control your own face
        if (!photonView.IsMine)
            return;

        if (recorder == null || recorder.LevelMeter == null)
            return;

        float amplitude = recorder.LevelMeter.CurrentAvgAmp;

        if (amplitude > screamThreshold)
        {
            SetFace(faceScream);
        }
        else if (amplitude > talkThreshold && talkingFaces != null && talkingFaces.Length > 0)
        {
            // Cycle between 3 talking faces for a more natural look
            changeTimer += Time.deltaTime;
            if (changeTimer > 0.1f) // change every 0.1 seconds
            {
                currentTalkingIndex = (currentTalkingIndex + 1) % talkingFaces.Length;
                SetFace(talkingFaces[currentTalkingIndex]);
                changeTimer = 0f;
            }
        }
        else
        {
            SetFace(faceClosed);
        }
    }

    void SetFace(Material material)
    {
        if (faceRenderer == null || material == null)
            return;

        var mats = faceRenderer.sharedMaterials; // Copy of all materials
        if (mats.Length > 2 && mats[2] != material)
        {
            mats[2] = material;
            faceRenderer.sharedMaterials = mats; // Reassign the array back
        }
    }
}