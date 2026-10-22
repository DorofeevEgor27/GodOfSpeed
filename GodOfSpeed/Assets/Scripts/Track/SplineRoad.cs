using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace GodOfSpeed.Racing
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("God Of Speed/Spline Road")]
    public sealed class SplineRoad : MonoBehaviour
    {
        [SerializeField] private SplineContainer spline;
        [SerializeField, Min(0)] private int splineIndex;
        [SerializeField, Min(0.01f), Tooltip("Full road width in world metres.")] private float width = 1f;
        [SerializeField, Tooltip("World-space vertical offset from the spline.")] private float heightOffset;
        [SerializeField, Range(0.1f, 10f)] private float segmentsPerMetre = 2f;
        [SerializeField, Min(0.01f)] private float metresPerTextureRepeat = 1f;

        private Mesh generatedMesh;
        private bool dirty = true;
        private Matrix4x4 lastRoadMatrix;
        private Matrix4x4 lastSplineMatrix;
        private SplineContainer lastSource;
        private Spline lastPath;

        private void OnEnable()
        {
            Spline.Changed += OnSplineChanged;
            dirty = true;
        }

        private void OnDisable() => Spline.Changed -= OnSplineChanged;

        private void OnValidate() => dirty = true;

        private void OnSplineChanged(Spline changed, int knot, SplineModification modification)
        {
            if (changed == CurrentPath) dirty = true;
        }

        private Spline CurrentPath => spline != null && splineIndex >= 0 && splineIndex < spline.Splines.Count
            ? spline.Splines[splineIndex] : null;

        private void Update()
        {
            var sourceMatrix = spline != null ? spline.transform.localToWorldMatrix : Matrix4x4.identity;
            if (dirty || generatedMesh == null || lastSource != spline || lastPath != CurrentPath
                || lastRoadMatrix != transform.localToWorldMatrix || lastSplineMatrix != sourceMatrix)
                Rebuild();
        }

        [ContextMenu("Rebuild Road")]
        public void Rebuild()
        {
            dirty = false;
            lastSource = spline;
            lastPath = CurrentPath;
            lastRoadMatrix = transform.localToWorldMatrix;
            lastSplineMatrix = spline != null ? spline.transform.localToWorldMatrix : Matrix4x4.identity;

            // Own a transient mesh: never modify a mesh asset assigned by the owner.
            if (generatedMesh == null)
            {
                generatedMesh = new Mesh { name = "Spline Road (generated)", hideFlags = HideFlags.DontSave };
            }
            generatedMesh.Clear();
            GetComponent<MeshFilter>().sharedMesh = generatedMesh;
            var collider = GetComponent<MeshCollider>();
            if (collider != null) collider.sharedMesh = null;
            if (lastPath == null || lastPath.Count < 2 || Mathf.Abs(lastRoadMatrix.determinant) < 0.000001f) return;

            float length = spline.CalculateLength(splineIndex);
            if (!float.IsFinite(length) || length < 0.0001f) return;
            int segments = Mathf.Clamp(Mathf.CeilToInt(length * Mathf.Clamp(segmentsPerMetre, 0.1f, 10f)),
                lastPath.Closed ? 3 : 1, 32000);
            var centres = new Vector3[segments + 1];
            var vertices = new Vector3[centres.Length * 2];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
                centres[i] = (Vector3)spline.EvaluatePosition(splineIndex, (float)i / segments) + Vector3.up * heightOffset;
            if (lastPath.Closed) centres[segments] = centres[0];

            float distance = 0f;
            for (int i = 0; i <= segments; i++)
            {
                int sample = lastPath.Closed && i == segments ? 0 : i;
                Vector3 tangent = (Vector3)spline.EvaluateTangent(splineIndex, (float)sample / segments);
                Vector3 right = Vector3.Cross(Vector3.up, tangent);
                if (right.sqrMagnitude < 0.000001f)
                {
                    int before = sample == 0 ? (lastPath.Closed ? segments - 1 : 0) : sample - 1;
                    int after = sample == segments ? segments : sample + 1;
                    tangent = centres[after] - centres[before];
                    right = Vector3.Cross(Vector3.up, tangent);
                }
                if (right.sqrMagnitude < 0.000001f)
                {
                    generatedMesh.Clear();
                    Debug.LogWarning("Spline Road needs a path without vertical or zero-length sections.", this);
                    return;
                }
                right.Normalize();
                Vector3 normal = Vector3.Cross(tangent.normalized, right).normalized;
                // Normals use the transpose when converting world space to mesh space.
                Vector3 localNormal = lastRoadMatrix.transpose.MultiplyVector(normal).normalized;
                Vector3 edge = right * (Mathf.Max(0.01f, width) * 0.5f);
                vertices[i * 2] = transform.InverseTransformPoint(centres[i] - edge);
                vertices[i * 2 + 1] = transform.InverseTransformPoint(centres[i] + edge);
                normals[i * 2] = normals[i * 2 + 1] = localNormal;
                if (i > 0) distance += Vector3.Distance(centres[i - 1], centres[i]);
                float v = distance / Mathf.Max(0.01f, metresPerTextureRepeat);
                uv[i * 2] = new Vector2(0, v);
                uv[i * 2 + 1] = new Vector2(1, v);
                if (i == segments) continue;
                int a = i * 2;
                int t = i * 6;
                triangles[t] = a;
                triangles[t + 1] = a + 2;
                triangles[t + 2] = a + 1;
                triangles[t + 3] = a + 1;
                triangles[t + 4] = a + 2;
                triangles[t + 5] = a + 3;
            }
            if (lastRoadMatrix.determinant < 0f)
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int swap = triangles[t + 1];
                    triangles[t + 1] = triangles[t + 2];
                    triangles[t + 2] = swap;
                }
            generatedMesh.indexFormat = IndexFormat.UInt16;
            generatedMesh.vertices = vertices;
            generatedMesh.normals = normals;
            generatedMesh.uv = uv;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();
            generatedMesh.RecalculateTangents();
            if (collider != null) collider.sharedMesh = generatedMesh;
        }

        private void OnDestroy()
        {
            if (generatedMesh == null) return;
            if (Application.isPlaying) Destroy(generatedMesh);
            else DestroyImmediate(generatedMesh);
        }
    }
}
