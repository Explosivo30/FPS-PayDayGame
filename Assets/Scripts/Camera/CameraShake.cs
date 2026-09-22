using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [SerializeField, Min(1f)] private float noiseFrequency = 38f;
    [SerializeField, Min(0f)] private float maximumOffset = 0.012f;

    private Vector3 appliedOffset;
    private float remainingTime;
    private float totalDuration;
    private float amplitude;
    private float noiseSeed;

    public void AddImpulse(float duration, float magnitude)
    {
        totalDuration = Mathf.Max(0.01f, duration);
        remainingTime = Mathf.Max(remainingTime, totalDuration);
        amplitude = Mathf.Clamp(Mathf.Max(magnitude, amplitude * 0.65f), 0f, maximumOffset);
        noiseSeed = Random.Range(0f, 1000f);
    }

    // Kept for compatibility with older callers. New weapon code calls AddImpulse directly.
    public IEnumerator Shake(float duration, float magnitude)
    {
        AddImpulse(duration, magnitude);
        yield break;
    }

    private void LateUpdate()
    {
        transform.localPosition -= appliedOffset;
        appliedOffset = Vector3.zero;

        if (remainingTime <= 0f)
        {
            amplitude = 0f;
            return;
        }

        remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
        float envelope = remainingTime / totalDuration;
        envelope *= envelope;
        float time = Time.time * noiseFrequency;
        float x = Mathf.PerlinNoise(noiseSeed, time) * 2f - 1f;
        float y = Mathf.PerlinNoise(noiseSeed + 31.7f, time) * 2f - 1f;
        appliedOffset = new Vector3(x, y, 0f) * amplitude * envelope;
        transform.localPosition += appliedOffset;
    }

    private void OnDisable()
    {
        transform.localPosition -= appliedOffset;
        appliedOffset = Vector3.zero;
        remainingTime = 0f;
        amplitude = 0f;
    }
}
