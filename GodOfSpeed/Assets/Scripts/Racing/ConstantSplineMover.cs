using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace GodOfSpeed.Racing
{
    public sealed class ConstantSplineMover : MonoBehaviour
    {
        [SerializeField] private SplineContainer spline;
        [SerializeField, Min(0f)] private float speedKmh = 30f;
        [SerializeField, Range(0f, 1f)] private float startPosition;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool alignToSpline = true;

        private float progress;
        private float splineLength;

        private void Awake()
        {
            progress = startPosition;
            RefreshSplineLength();
            ApplyPosition();
        }

        private void Update()
        {
            if (spline == null)
            {
                return;
            }

            if (splineLength <= 0f)
            {
                RefreshSplineLength();
            }

            if (splineLength <= 0f)
            {
                return;
            }

            float speedMetersPerSecond = speedKmh / 3.6f;
            progress += speedMetersPerSecond * Time.deltaTime / splineLength;
            progress = loop ? Mathf.Repeat(progress, 1f) : Mathf.Clamp01(progress);

            ApplyPosition();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            progress = startPosition;
            RefreshSplineLength();
            ApplyPosition();
        }

        private void RefreshSplineLength()
        {
            splineLength = spline == null ? 0f : spline.CalculateLength();
        }

        private void ApplyPosition()
        {
            if (spline == null)
            {
                return;
            }

            if (!spline.Evaluate(progress, out float3 position, out float3 tangent, out float3 upVector))
            {
                return;
            }

            transform.position = ToVector3(position);

            if (!alignToSpline)
            {
                return;
            }

            Vector3 forward = ToVector3(tangent);
            Vector3 up = ToVector3(upVector);

            if (forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(forward.normalized, up.normalized);
            }
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
