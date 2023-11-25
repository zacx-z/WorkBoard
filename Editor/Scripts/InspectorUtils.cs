using System.Linq;

namespace WorkBoard {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    public static class InspectorUtils {
        private static Type _previewableType;
        private static FieldInfo _typeField;
        private static Dictionary<Type, List<Type>> _previewableTypes;

        public static List<Type> GetPreviewableTypesForType(Type objectType) {
            _previewableTypes ??= GetPreviewableTypes();
            if (_previewableTypes.TryGetValue(objectType, out var typeList)) return typeList;
            return null;
        }

        private static Dictionary<Type, List<Type>> GetPreviewableTypes() {
            var previewableTypes = new Dictionary<Type, List<Type>>();
            _previewableType ??= typeof(Editor).Assembly.GetType("UnityEditor.IPreviewable");
            _typeField ??= typeof(CustomPreviewAttribute).GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (Type type in TypeCache.GetTypesDerivedFrom(_previewableType)) {
                if (!type.IsSubclassOf(typeof(Editor))) {
                    if (type.GetConstructor(Type.EmptyTypes) == null) {
                        Debug.LogError(
                            $"{(object)type} does not contain a default constructor, it will not be registered as a " +
                            "preview handler. Use the Initialize function to set up your object instead.");
                    } else {
                        foreach (var customAttribute in type.GetCustomAttributes(typeof(CustomPreviewAttribute), false) as CustomPreviewAttribute[]) {
                            var attrType = (Type)_typeField.GetValue(customAttribute);
                            if (attrType != null) {
                                List<Type> typeList;
                                if (!previewableTypes.TryGetValue(attrType, out typeList)) {
                                    typeList = new List<Type>();
                                    previewableTypes.Add(attrType, typeList);
                                }

                                typeList.Add(type);
                            }
                        }
                    }
                }
            }

            return previewableTypes;
        }

        private static IEnumerable<ObjectPreview> GetPreviewsForType(UnityEngine.Object[] targets) {
            if (targets == null || targets.Length == 0)
                return Enumerable.Empty<ObjectPreview>();
            var target = targets[0];
            Dictionary<Type, List<Type>> previewableTypes = GetPreviewableTypes();
            if (previewableTypes == null || !previewableTypes.TryGetValue(target.GetType(), out var typeList) || typeList == null)
                return Enumerable.Empty<ObjectPreview>();
            var previewsForType = new List<ObjectPreview>();
            foreach (var previewType in typeList) {
                if (typeof(ObjectPreview).IsAssignableFrom(previewType) && Activator.CreateInstance(previewType) is ObjectPreview instance) {
                    instance.Initialize(targets);
                    previewsForType.Add(instance);
                }
            }

            return previewsForType;
        }

        public static ObjectPreview? GetPreviewForTarget(UnityEngine.Object[] targets, Type previewType) {
            if (typeof(ObjectPreview).IsAssignableFrom(previewType) &&
                Activator.CreateInstance(previewType) is ObjectPreview instance) {
                instance.Initialize(targets);
                return instance;
            }

            return null;
        }
    }
}