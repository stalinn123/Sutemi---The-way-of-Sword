using System;
using UnityEditor;
using UnityEngine;

namespace Kendo.EditorTools.Setup
{
    /// <summary>Small helper for writing private [SerializeField]s of data assets from setup code.</summary>
    internal sealed class SerializedEdit : IDisposable
    {
        private readonly SerializedObject so;

        public SerializedEdit(UnityEngine.Object target) => so = new SerializedObject(target);

        public SerializedEdit Set(string field, string value) { Prop(field).stringValue = value; return this; }
        public SerializedEdit Set(string field, int value) { Prop(field).intValue = value; return this; }
        public SerializedEdit Set(string field, float value) { Prop(field).floatValue = value; return this; }
        public SerializedEdit Set(string field, bool value) { Prop(field).boolValue = value; return this; }
        public SerializedEdit Set(string field, UnityEngine.Object value) { Prop(field).objectReferenceValue = value; return this; }

        /// <summary>Works for plain and [Flags] enums (writes the underlying int).</summary>
        public SerializedEdit SetEnum(string field, Enum value) { Prop(field).intValue = Convert.ToInt32(value); return this; }

        public SerializedProperty Prop(string field) =>
            so.FindProperty(field) ?? throw new ArgumentException($"Field '{field}' not found on {so.targetObject.GetType().Name}");

        public void Dispose()
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so.targetObject);
            so.Dispose();
        }
    }

    internal static class AssetFolders
    {
        public const string Project = "Assets/_Project";
        public const string Data = Project + "/Data";
        public const string Prefabs = Project + "/Prefabs";
        public const string Settings = Project + "/Settings";

        public static void Ensure(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) Ensure(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }

        /// <summary>Loads an asset of type T at path, creating it if missing. Existing assets are updated in place.</summary>
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            Ensure(System.IO.Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
