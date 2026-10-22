using System;
using GodOfSpeed.Racing;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

public static class SplineRoadVerification
{
    [MenuItem("Tools/God Of Speed/Verify Spline Road")]
    public static void Run()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Road verification");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var source = root.AddComponent<SplineContainer>();
            source.Spline.Add(new BezierKnot(new float3(0, 0, 0)), TangentMode.Linear);
            source.Spline.Add(new BezierKnot(new float3(0, 0, 10)), TangentMode.Linear);
            var roadObject = new GameObject("Road");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(roadObject, scene);
            var road = roadObject.AddComponent<SplineRoad>();
            var settings = new SerializedObject(road);
            settings.FindProperty("spline").objectReferenceValue = source;
            settings.FindProperty("width").floatValue = 4;
            settings.ApplyModifiedPropertiesWithoutUndo();
            road.Rebuild();
            CheckFlat(roadObject, 4);

            // World width and lighting must survive a transformed mesh object.
            roadObject.transform.SetPositionAndRotation(new Vector3(4, 3, -2), Quaternion.Euler(17, 32, 9));
            roadObject.transform.localScale = new Vector3(2, 3, 0.5f);
            road.Rebuild();
            CheckFlat(roadObject, 4);

            source.Spline.Clear();
            source.Spline.Add(new BezierKnot(new float3(0, 0, 0)), TangentMode.AutoSmooth);
            source.Spline.Add(new BezierKnot(new float3(0, 0, 20)), TangentMode.AutoSmooth);
            source.Spline.Add(new BezierKnot(new float3(20, 0, 20)), TangentMode.AutoSmooth);
            source.Spline.Add(new BezierKnot(new float3(20, 0, 0)), TangentMode.AutoSmooth);
            source.Spline.Closed = true;
            road.Rebuild();
            CheckFlat(roadObject, 4);
            var mesh = roadObject.GetComponent<MeshFilter>().sharedMesh;
            Require(Vector3.Distance(mesh.vertices[0], mesh.vertices[mesh.vertexCount - 2]) < 0.0001f, "Closed seam position");
            Require(Vector3.Distance(mesh.normals[0], mesh.normals[mesh.vertexCount - 2]) < 0.0001f, "Closed seam lighting");
            var collider = roadObject.AddComponent<MeshCollider>();
            road.Rebuild();
            Require(collider.sharedMesh == mesh, "Collider updated");
            source.Spline.Clear();
            road.Rebuild();
            Require(mesh.vertexCount == 0 && collider.sharedMesh == null, "Empty spline clears old geometry");
            Debug.Log("SPLINE_ROAD_VERIFICATION_PASS: flat lighting, width, transformed object, curved closed seam, collider, empty spline.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void CheckFlat(GameObject road, float width)
    {
        var mesh = road.GetComponent<MeshFilter>().sharedMesh;
        Require(mesh.vertexCount > 0, "Generated geometry");
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        for (int i = 0; i < vertices.Length; i += 2)
        {
            var left = road.transform.TransformPoint(vertices[i]);
            var right = road.transform.TransformPoint(vertices[i + 1]);
            Require(Mathf.Abs(left.y) < 0.0001f && Mathf.Abs(right.y) < 0.0001f, "Flat surface");
            Require(Mathf.Abs(Vector3.Distance(left, right) - width) < 0.0001f, "World width");
            var normal = road.transform.worldToLocalMatrix.transpose.MultiplyVector(normals[i]).normalized;
            Require(Vector3.Dot(normal, Vector3.up) > 0.9999f, "Upward lighting");
        }
        var indices = mesh.triangles;
        for (int i = 0; i < indices.Length; i += 3)
        {
            var a = road.transform.TransformPoint(vertices[indices[i]]);
            var b = road.transform.TransformPoint(vertices[indices[i + 1]]);
            var c = road.transform.TransformPoint(vertices[indices[i + 2]]);
            Require(Vector3.Cross(b - a, c - a).y > 0, "Upward triangles");
        }
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new Exception("Spline Road verification failed: " + label);
    }
}
