using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Linq.Expressions;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine.Serialization;
using Object = System.Object;

[CreateAssetMenu(menuName = "LoadData/DataParser")]
public class DataParser: ScriptableObject {

    [SerializeField] private string m_NameSpace = null;
    [SerializeField] private List<string> m_Path = new();
    [SerializeField] private string m_AddEnums;
    [FormerlySerializedAs("m_DataLoader")] [SerializeField] private RawDataLoader mDataLoaderLoader;
    
    public List<string> Path => m_Path;
    
    private struct TypeAndName {
        
        public string Name;
        public string Type;

        public TypeAndName(string type, string name) {
            Type = type;
            Name = name;
        }
    }
    
    //divide rawData's info to (header and dataPart) 
    private (List<TypeAndName>, List<List<string>>) Divide(List<List<string>> origin) {

        //origin data should over 3. because It must contain dataType row, dataName row, context row 
        if (origin.Count <= 2) {
            
            throw new Exception($"this data is wrong data's size should over 3. but this length is {origin.Count}");
        }

        List<TypeAndName> header = new();
        for (int i = 0; i < origin[0].Count; i++) {
            header.Add(new TypeAndName(origin[1][i], origin[0][i]));
        }

        return (header, origin.Skip(2).ToList());
    }

    private void GenerateDataType( string typeName, List<TypeAndName> header) {

        CodeTypeDeclaration dataType = new(typeName);
        dataType.BaseTypes.Add(typeof(DefaultDataType));
        dataType.IsClass = true;
        dataType.CustomAttributes.Add(
            new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(SerializableAttribute))
            )
        );

        foreach (var field in header) {

            Type fieldType = Type.GetType($"System.{field.Type}") 
                             ?? Type.GetType(field.Type) 
                             ?? Type.GetType($"{field.Type}, Assembly-CSharp")
                             ?? Type.GetType($"{m_NameSpace}.{field.Type}")
                             ?? throw new Exception($"{field.Type} is didn't exist, check again(Name is {field.Name}");
            
            //make field member
            CodeMemberField newField = new();
            newField.Name = $"m_{field.Name}";
            newField.Type = new CodeTypeReference(fieldType);
            newField.Attributes = MemberAttributes.Private;
            newField.CustomAttributes.Add(
                new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(SerializeField))
                )
            );
            dataType.Members.Add(newField);

            //make field's property(only have get method)
            CodeMemberProperty newProperty = new();
            newProperty.Name = field.Name;
            newProperty.Type = new CodeTypeReference(fieldType);
            newProperty.Attributes = MemberAttributes.Public | MemberAttributes.Final;
            newProperty.HasGet = true;
            newProperty.HasSet = false;
            newProperty.GetStatements.Add(
                new CodeMethodReturnStatement(
                    new CodeFieldReferenceExpression (
                        new CodeThisReferenceExpression (), newField.Name
                    )
                )
            );
            dataType.Members.Add(newProperty);
        }

        CodeTypeDeclaration dataTableType = new($"{typeName}Table");
        CodeTypeReference dataTypeReference = new(typeof(DefaultDataTable<>));
        dataTypeReference.TypeArguments.Add(new CodeTypeReference($"{m_NameSpace}.{typeName}"));
        
        dataTableType.IsClass = true;
        dataTableType.BaseTypes.Add(dataTypeReference);
        dataTableType.Attributes = MemberAttributes.Public;
        
        //Group by namespace
        CodeNamespace newNamespace = new(m_NameSpace);
        newNamespace.Types.Add(dataType);
        newNamespace.Types.Add(dataTableType);
        
        //
        CodeCompileUnit compileUnit = new();
        compileUnit.Namespaces.Add(newNamespace);

        //
        CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");
        string path = $@"Assets\{m_NameSpace}\DataTypes\{typeName}.cs";
        StreamWriter writer = new(path);
        provider.GenerateCodeFromCompileUnit(compileUnit, writer, new CodeGeneratorOptions());
        writer.Close();
        
        Debug.Log($"complete make file {typeName}");
    }

    private Object Parse(Type type, string target) {

        if (type.IsEnum) {
            return Enum.Parse(type, target);
        }
        
        if (type == typeof(string)) {

            return target;
        }

        var parse = type.GetMethod("Parse", new Type[] { typeof(string)});
        
        return parse?.Invoke(null, new object[] {target});
    }

    private void SyncData(List<List<string>> data, string dataTypeName) {

        Type dataType = Type.GetType($"{m_NameSpace}.{dataTypeName}");
        Type dataTableType = Type.GetType($"{m_NameSpace}.{dataTypeName}Table");

        string directoryPath = $@"Assets\{m_NameSpace}\Generated\{dataTypeName}Table.asset";
        UnityEngine.Object targetTable = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(directoryPath);

        if (targetTable is null) {
            targetTable = CreateInstance(dataTableType);

            AssetDatabase.CreateAsset(targetTable, directoryPath);
            AssetDatabase.Refresh();
        }

        List<(int, Object)> keyAndValue = new();

        foreach (var factor in data) {

            int code = 0;
            Object row = Activator.CreateInstance(dataType);
            FieldInfo[] fields = dataType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++) {

                Type fieldType = fields[i].FieldType;
                Object value = Parse(fieldType, factor[i]);
                if (i == 0) {
                    code = (int)value;
                }

                fields[i].SetValue(row, value);
            }

            keyAndValue.Add((code, row));
        }

        FieldInfo dataTable = dataTableType.GetField("m_DataTable", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo addFunc = dataTable.FieldType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo clearFunc = dataTable.FieldType.GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance);
        Object target = dataTable.GetValue(targetTable);

        clearFunc.Invoke(target, new object[] {});

        foreach (var context in keyAndValue) {

            addFunc.Invoke(target, new object[] {context.Item1, context.Item2});
        }

        //save on disk
        EditorUtility.SetDirty(targetTable);
        Debug.Log("Sync complete");
    }

    public void AddEnums() {

        string path = $@"Assets\{m_NameSpace}\DataTypes\AddType.cs";
        if (string.IsNullOrEmpty(m_AddEnums)) return;
        if (File.Exists(path)) {
            Debug.Log($"AddDataFile is already exist ({path})");
            return;
        }
        
        List<List<string>> data = mDataLoaderLoader.Load(m_AddEnums);

        CodeCompileUnit compileUnit = new();
        CodeNamespace codeNamespace = new(m_NameSpace);
        compileUnit.Namespaces.Add(codeNamespace);
        
        foreach (var row in data) {

            CodeTypeDeclaration newEnum = new(row[0]);
            newEnum.IsEnum = true;
            newEnum.Attributes = MemberAttributes.Public;
            newEnum.CustomAttributes.Add(
                new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(SerializableAttribute))
                )
            );

            int index = 0;
            foreach (var factor in row.Skip(1)) {

                newEnum.Members.Add(new CodeMemberField(typeof(int), factor)
                    { InitExpression = new CodePrimitiveExpression(index) });

                index++;
            }

            codeNamespace.Types.Add(newEnum);
        }

        CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");
        StreamWriter wirter = new(path);
        provider.GenerateCodeFromCompileUnit(compileUnit, wirter, new CodeGeneratorOptions());
        wirter.Close();

        Debug.Log("make addData type");
    }

    public void SetUp() {
        //check and make namespace directory
        DirectoryInfo @namespace = new(@$"Assets\{m_NameSpace}");
        if (!@namespace.Exists) {
            @namespace.Create();
            Debug.Log($"Create Namespace({m_NameSpace}) Directory");
        }
                                
        DirectoryInfo dataTypes = new DirectoryInfo($@"Assets\{m_NameSpace}\DataTypes");
        if (!dataTypes.Exists) {
            dataTypes.Create();
            Debug.Log($"Create DataTypes({m_NameSpace}\\DataTypes) Directory");
        }
                            
        DirectoryInfo Generated = new DirectoryInfo($@"Assets\{m_NameSpace}\Generated");
        if (!Generated.Exists) {
            Generated.Create();
            Debug.Log($"Create DataTypes({m_NameSpace}\\Generated) Directory");
        }

        AddEnums();
        AssetDatabase.Refresh();
    }

    public void Generate(string path) {
        //check path is correct
        if (new[] {m_NameSpace, path}.Any(string.IsNullOrEmpty)) {
            throw new NullReferenceException("NameSpace and Path must not null or empty");
        }
        
        //split data
        List<List<string>> data = mDataLoaderLoader.Load(path);
        List<TypeAndName> header;
        (header, data) = Divide(data);
                
        //find data type
        string dataTypeName = $"{path.Split('!')[0]}Data";
        Type dataType = Type.GetType($"{m_NameSpace}.{dataTypeName}");
                
        if (dataType is null) {
            //make DataType
            GenerateDataType(dataTypeName, header);
            AssetDatabase.Refresh();
                    
            dataType = Type.GetType($"{m_NameSpace}.{dataTypeName}");
        }
        else {
            Debug.Log($"already exist {dataTypeName} Type");
        }
    }
    
    public void Sync(string path) {

        //check path is correct
        if (new[] {m_NameSpace, path}.Any(string.IsNullOrEmpty)) {
            throw new NullReferenceException("NameSpace and Path must not null or empty");
        }
        
        //split data
        List<List<string>> data = mDataLoaderLoader.Load(path);
        List<TypeAndName> header;
        (header, data) = Divide(data);
        
        //find data type
        string dataTypeName = $"{path.Split('!')[0]}Data";

        SyncData(data, dataTypeName);
        
    }
}