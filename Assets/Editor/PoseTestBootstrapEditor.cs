using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PoseTestBootstrap))]
public class PoseTestBootstrapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        PoseTestBootstrap bootstrap = (PoseTestBootstrap)target;
        if (GUILayout.Button("Build Demo"))
        {
            bootstrap.BuildDemo();

            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(bootstrap.gameObject);
            }
        }
    }
}
