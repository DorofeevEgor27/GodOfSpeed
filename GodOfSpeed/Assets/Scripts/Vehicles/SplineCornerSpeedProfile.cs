using System;
using System.Collections.Generic;
using UnityEngine;

namespace GodOfSpeed.Racing
{
    // A corner is a continuous change of heading in one direction, bounded by
    // a straight or an inflection. It is independent of car speed and spline knots.
    public sealed class SplineCornerSpeedProfile
    {
        public readonly struct Corner
        {
            public readonly float StartDistance;
            public readonly float EndDistance;
            public readonly float AngleDegrees;
            public readonly int Direction;

            public Corner(float start, float end, float angle, int direction)
            {
                StartDistance = start;
                EndDistance = end;
                AngleDegrees = Mathf.Clamp(angle, 0f, 180f);
                Direction = direction;
            }
        }

        private readonly Corner[] corners;
        public IReadOnlyList<Corner> Corners => corners;

        public SplineCornerSpeedProfile(float[] distances, Vector3[] tangents, bool closed)
        {
            if (distances == null || tangents == null || distances.Length != tangents.Length || distances.Length < 2)
                throw new ArgumentException("Matching distance and tangent samples are required.");
            var result = new List<Corner>();
            int direction = 0;
            float start = 0f, end = 0f, angle = 0f;
            for (int i = 1; i < distances.Length; i++)
            {
                Vector3 previous = Vector3.ProjectOnPlane(tangents[i - 1], Vector3.up).normalized;
                Vector3 current = Vector3.ProjectOnPlane(tangents[i], Vector3.up).normalized;
                float change = previous.sqrMagnitude > 0f && current.sqrMagnitude > 0f
                    ? Mathf.Atan2(Vector3.Cross(previous, current).y, Vector3.Dot(previous, current)) * Mathf.Rad2Deg : 0f;
                // Numerical tolerance, not a gameplay angle threshold.
                int nextDirection = Mathf.Abs(change) < 0.001f ? 0 : (change > 0f ? 1 : -1);
                if (nextDirection != direction)
                {
                    if (direction != 0 && angle > 0.1f)
                        result.Add(new Corner(start, end, angle, direction));
                    direction = nextDirection;
                    start = distances[i - 1];
                    angle = 0f;
                }
                if (direction != 0) angle += Mathf.Abs(change);
                end = distances[i];
            }
            if (direction != 0 && angle > 0.1f)
                result.Add(new Corner(start, end, angle, direction));

            float length = distances[distances.Length - 1];
            if (closed && result.Count > 1)
            {
                Corner first = result[0], last = result[result.Count - 1];
                if (first.StartDistance <= 0.001f && last.EndDistance >= length - 0.001f && first.Direction == last.Direction)
                {
                    result[result.Count - 1] = new Corner(last.StartDistance, length + first.EndDistance,
                        last.AngleDegrees + first.AngleDegrees, last.Direction);
                    result.RemoveAt(0);
                }
            }
            corners = result.ToArray();
        }
    }
}
