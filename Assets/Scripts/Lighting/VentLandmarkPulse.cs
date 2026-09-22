using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class VentLandmarkPulse : MonoBehaviour
{
    [Range(0f, 0.5f)]
    [SerializeField] private float amplitude = 0.10f;

    [Min(0.05f)]
    [SerializeField] private float frequency = 1.35f;

    [SerializeField] private float phase = 0.61f;

    private Light targetLight;
    private float baseIntensity;

    public void Configure(float newAmplitude, float newFrequency, float newPhase)
    {
        amplitude = Mathf.Clamp(newAmplitude, 0f, 0.5f);
        frequency = Mathf.Max(0.05f, newFrequency);
        phase = newPhase;
    }

    private void Awake()
    {
        CacheBaseIntensity();
    }

    private void OnEnable()
    {
        CacheBaseIntensity();
    }

    private void Update()
    {
        if (targetLight == null)
            return;

        float wave = 1f + amplitude * Mathf.Sin(Time.time * frequency + phase);
        targetLight.intensity = baseIntensity * wave;
    }

    private void OnDisable()
    {
        if (targetLight != null)
            targetLight.intensity = baseIntensity;
    }

    private void CacheBaseIntensity()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        if (targetLight != null)
            baseIntensity = Mathf.Max(0f, targetLight.intensity);
    }
}
