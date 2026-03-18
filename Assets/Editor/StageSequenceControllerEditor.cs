using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageSequenceController))]
public class StageSequenceControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        StageSequenceController controller = (StageSequenceController)target;
        if (GUILayout.Button("Sync Stage Setup"))
        {
            controller.SyncStageSetup();
            EditorUtility.SetDirty(controller.gameObject);
        }

        if (GUILayout.Button("Create Missing Stage Setup"))
        {
            controller.CreateMissingStageSetup();
            EditorUtility.SetDirty(controller.gameObject);
        }
    }
}