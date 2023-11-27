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
        private VisualElement _inspectorElement;
        private MethodInfo _isEnabledMethod;
        private static PropertyInfo _currentViewWidthProperty;

        public Editor Editor => _editor;

        public InspectorElement(Object target, Editor editor = null) {
            _target = target;
            _editor = editor;

            this.RegisterCallback<AttachToPanelEvent>(this.OnAttachToPanel);
            this.RegisterCallback<DetachFromPanelEvent>(this.OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt) {
            Init();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt) {
            Cleanup();
        }

        private void Init() {
            if (_target == null) return;
            _editor ??= GetOrCreateEditor(_target);
            _inspectorElement = CreateInspectorElement(Editor);
            this.hierarchy.Add(_inspectorElement);
        }

        private void Cleanup() {
            if (Editor != null) {
                Object.DestroyImmediate(Editor);
                _editor = null;
            }

            if (_inspectorElement != null) {
                this.hierarchy.Remove(_inspectorElement);
                _inspectorElement = null;
            }
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
#if UNITY_2021_3_OR_NEWER
            _currentViewWidthProperty ??= typeof(EditorGUIUtility).GetProperty("currentViewWidth", BindingFlags.Public | BindingFlags.Static);
            _currentViewWidthProperty.SetValue(null, 320);
#endif
            EditorGUIUtility.fieldWidth = 150.0f;
            EditorGUIUtility.labelWidth = 240.0f;
            EditorGUIUtility.hierarchyMode = false;
            EditorGUIUtility.wideMode = false;
        }
    }
}
