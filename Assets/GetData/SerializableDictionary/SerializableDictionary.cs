using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class DictionaryData<K, V> {
    public K Key;
    public V Value;

    public DictionaryData(K key, V value) {
        Key = key;
        Value = value;
    }

    public DictionaryData(KeyValuePair<K, V> data) {
        Key = data.Key;
        Value = data.Value;
    }
}

[Serializable]
public class SerializableDictionary<K, V>: Dictionary<K, V>, ISerializationCallbackReceiver {

    [SerializeField] private List<DictionaryData<K, V>> dataList = new();

    public SerializableDictionary() { }

    public SerializableDictionary(Dictionary<K, V> dictionary) {
        foreach (var keyValuePair in dictionary) {
            
            dataList.Add(new(keyValuePair));
        }

    }

    public Tuple<Type, Type> GetTypes() {

        return new (typeof(K), typeof(V));
    }

    public void OnBeforeSerialize() {

        dataList.Clear();
        foreach (var data in this) {

            dataList.Add(new(data));
        }
    }

    public void OnAfterDeserialize() {

        this.Clear();
        foreach (var data in dataList) {

            this.Add(data.Key, data.Value);
        }
    }
}