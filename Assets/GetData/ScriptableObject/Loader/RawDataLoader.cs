using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
public abstract class RawDataLoader: ScriptableObject {
    
    public abstract List<List<string>> Load(string path);
}
