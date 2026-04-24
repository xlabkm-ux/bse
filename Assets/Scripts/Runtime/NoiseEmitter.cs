using System;
using UnityEngine;

namespace BreachScenarioEngine.Runtime
{
    [DisallowMultipleComponent]
    public sealed class NoiseEmitter : MonoBehaviour
    {
        [SerializeField] private float defaultRadius = 8f;
        [SerializeField] private float defaultIntensity = 1f;

        public static event Action<NoiseEvent> NoiseEmitted;

        public void EmitDefault()
        {
            Emit(defaultRadius, defaultIntensity);
        }

        public void Emit(float radius, float intensity)
        {
            var noise = new NoiseEvent(
                transform.position,
                Mathf.Max(0f, radius),
                Mathf.Clamp01(intensity),
                gameObject,
                Time.time);

            NoiseEmitted?.Invoke(noise);
        }
    }

    public readonly struct NoiseEvent
    {
        public NoiseEvent(Vector3 position, float radius, float intensity, GameObject source, float timestamp)
        {
            Position = position;
            Radius = radius;
            Intensity = intensity;
            Source = source;
            Timestamp = timestamp;
        }

        public Vector3 Position { get; }
        public float Radius { get; }
        public float Intensity { get; }
        public GameObject Source { get; }
        public float Timestamp { get; }
    }
}
