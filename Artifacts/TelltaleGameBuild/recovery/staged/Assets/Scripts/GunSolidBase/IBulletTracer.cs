using UnityEngine;

public interface IBulletTracer
{
    /// <summary>
    /// Plays the tracer from muzzle to impact (or max distance).
    /// </summary>
    void Play(Vector3 origin, Vector3 destination);
}
