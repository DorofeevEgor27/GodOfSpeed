using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GodOfSpeed.EditorTools
{
    public sealed class LocationTileRandomizerWindow : EditorWindow
    {
        private Material materialA;
        private Material materialB;
        private Material materialC;

        private float heightA = 1f;
        private float heightB = 2f;
        private float heightC = 3f;

        [MenuItem("Tools/God Of Speed/Location Tile Randomizer")]
        private static void Open()
        {
            GetWindow<LocationTileRandomizerWindow>("Tile Randomizer");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Materials", EditorStyles.boldLabel);
            materialA = (Material)EditorGUILayout.ObjectField("Material 1", materialA, typeof(Material), false);
            materialB = (Material)EditorGUILayout.ObjectField("Material 2", materialB, typeof(Material), false);
            materialC = (Material)EditorGUILayout.ObjectField("Material 3", materialC, typeof(Material), false);

            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField("Heights", EditorStyles.boldLabel);
            heightA = EditorGUILayout.FloatField("Height 1", heightA);
            heightB = EditorGUILayout.FloatField("Height 2", heightB);
            heightC = EditorGUILayout.FloatField("Height 3", heightC);

            EditorGUILayout.Space(12f);

            using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
            {
                if (GUILayout.Button("Randomize Selected"))
                {
                    RandomizeSelected();
                }
            }
        }

        private void RandomizeSelected()
        {
            Material[] materials = GetAssignedMaterials();
            float[] heights = { heightA, heightB, heightC };
            GameObject[] selectedObjects = Selection.gameObjects;

            Undo.SetCurrentGroupName("Randomize Location Tiles");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (GameObject selectedObject in selectedObjects)
            {
                ApplyRandomHeight(selectedObject, heights);
                ApplyRandomMaterial(selectedObject, materials);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        private static void ApplyRandomHeight(GameObject target, float[] heights)
        {
            Undo.RecordObject(target.transform, "Randomize Tile Height");

            Vector3 position = target.transform.localPosition;
            position.y = heights[Random.Range(0, heights.Length)];
            target.transform.localPosition = position;

            EditorUtility.SetDirty(target.transform);
        }

        private static void ApplyRandomMaterial(GameObject target, Material[] materials)
        {
            if (materials.Length == 0)
            {
                return;
            }

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            Undo.RecordObject(renderer, "Randomize Tile Material");
            renderer.sharedMaterial = materials[Random.Range(0, materials.Length)];
            EditorUtility.SetDirty(renderer);
        }

        private Material[] GetAssignedMaterials()
        {
            List<Material> materials = new();

            if (materialA != null)
            {
                materials.Add(materialA);
            }

            if (materialB != null)
            {
                materials.Add(materialB);
            }

            if (materialC != null)
            {
                materials.Add(materialC);
            }

            return materials.ToArray();
        }
    }
}
