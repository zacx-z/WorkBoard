using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace WorkBoard
{
    public class InspectorElement : VisualElement {
        private readonly Object _target;
        private Editor _editor;
        private MethodInfo _isEnabledMethod;
        private static PropertyInfo _currentViewWidthProperty;

        public InspectorElement(Object target) {
            _target = target;

            Reset();
        }

        private void Reset() {
            this.Clear();
            if (_target == null) return;
            if (_editor != null) {
                Object.DestroyImmediate(_editor);
            }
            _editor = GetOrCreateEditor(_target);
            var child = CreateInspectorElement(_editor);
            this.hierarchy.Add(child);
        }

        private Editor GetOrCreateEditor(Object target) {
            Editor editor = Editor.CreateEditor(target);
            return editor;
        }

        private VisualElement CreateInspectorElement(Editor editor) {
            var element = editor.CreateInspectorGUI();
            if (element != null) {
                return element;
            }

            var inspector = new IMGUIContainer();
            inspector.onGUIHandler = () => {
                ResetGUIState();
                _isEnabledMethod ??= editor.GetType().GetMethod("IsEnabled", BindingFlags.NonPublic | BindingFlags.Instance);
                using (new EditorGUI.DisabledScope(!(bool)_isEnabledMethod.Invoke(editor, new object[]{}))) {
                    try {
                        GUI.changed = false;
                        using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins)) {
                            editor.OnInspectorGUI();
                        }
                    }
                    catch (Exception ex) {
                        Debug.LogException(ex);
                    }
                }
            };
            return inspector;
        }

        private static void ResetGUIState() {
            GUI.backgroundColor = GUI.contentColor = Color.white;
            GUI.color = Color.white;
            GUI.enabled = true;
            GUI.changed = false;
            EditorGUI.indentLevel = 0;
            _currentViewWidthProperty ??= typeof(EditorGUIUtility).GetProperty("currentViewWidth", BindingFlags.Public | BindingFlags.Static);
            _currentViewWidthProperty.SetValue(null, 320);
            EditorGUIUtility.fieldWidth = 150.0f;
            EditorGUIUtility.labelWidth = 240.0f;
            EditorGUIUtility.hierarchyMode = false;
            EditorGUIUtility.wideMode = false;
        }
    }
}
