using UnityEngine;

public class DefaultDataTable<T>: ScriptableObject where T : DefaultDataType {

    [SerializeField] protected SerializableDictionary<int, T> m_DataTable = new();
    public SerializableDictionary<int, T> DataTable => m_DataTable;
}