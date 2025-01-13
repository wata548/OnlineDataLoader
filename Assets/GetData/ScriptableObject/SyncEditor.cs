using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

[CustomEditor(typeof(DataParser))]
public class SyncEditor: Editor {

    public override void OnInspectorGUI() {

        base.OnInspectorGUI();

        GUILayout.Label("It make directory and EnumType (please press first and wait for loading)");
        if (GUILayout.Button("SetUp")) {
            
            DataParser parser = target as DataParser;
            parser.SetUp();
        }
        
        GUILayout.Label("It make data type (please press second and wait for loading)");
        if (GUILayout.Button("GenerateData")) {
            DataParser parser = target as DataParser;

            foreach (var factor in parser.Path) {

                parser.Generate(factor);
            }
        }
        
        if (GUILayout.Button("SyncData")) {
            DataParser parser = target as DataParser;

            foreach (var factor in parser.Path) {
                
                parser.Sync(factor);
            }
        }
    }

}
