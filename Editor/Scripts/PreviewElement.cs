using System;

namespace WorkBoard {
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class PreviewElement : VisualElement {
        private readonly Object _target;
        public readonly Type previewType;
        private IPreviewProvider _preview;
        private IMGUIContainer _previewElement;

        public PreviewElement(Object target, Type previewType, IPreviewProvider preview = null) {
            _target = target;
            this.previewType = previewType;
            this._preview = preview;
            _previewElement = new IMGUIContainer();
            _previewElement.onGUIHandler = DrawPreview;
            this.hierarchy.Add(_previewElement);

            this.RegisterCallback<DetachFromPanelEvent>(this.OnDetachFromPanel);
        }

        private void DrawPreview() {
            GUI.color = Color.white;

            _preview ??= new ObjectPreviewProvider(InspectorUtils.GetPreviewForTarget(new[] { _target }, previewType));

            var thatControlsPreview = _preview;
            bool flag = thatControlsPreview != null && thatControlsPreview.HasPreviewGUI();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(21f));
            string text = string.Empty;
            if (thatControlsPreview != null) text = thatControlsPreview.GetPreviewTitle().text;
            GUILayout.Label(text, NodeStyles.preToolbarLabel);
            GUILayout.FlexibleSpace();
            if (flag) thatControlsPreview.OnPreviewSettings();
            EditorGUILayout.EndHorizontal();

            var previewArea = GUILayoutUtility.GetRect(0.0f, 1024f, 64f, 1024f);
            if (Event.current.type == EventType.Repaint) NodeStyles.preBackground.Draw(previewArea, false, false, false, false);
            if (thatControlsPreview == null || !thatControlsPreview.HasPreviewGUI()) return;
            thatControlsPreview.DrawPreview(previewArea);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt) {
            if (_preview != null) {
                _preview.Cleanup();
                _preview = null;
            }
        }
    }
    
    public interface IPreviewProvider {
        void Cleanup();
        bool HasPreviewGUI();
        GUIContent GetPreviewTitle();
        void OnPreviewSettings();
        void DrawPreview(Rect previewArea);
    }
    
    public class ObjectPreviewProvider : IPreviewProvider {
        public ObjectPreview preview;

        public ObjectPreviewProvider(ObjectPreview preview) {
            this.preview = preview;
        }

        public void Cleanup() => this.preview.Cleanup();

        public bool HasPreviewGUI() => this.preview.HasPreviewGUI();

        public GUIContent GetPreviewTitle() => this.preview.GetPreviewTitle();

        public void OnPreviewSettings() => this.preview.OnPreviewSettings();

        public void DrawPreview(Rect previewArea) => this.preview.DrawPreview(previewArea);
    }

    public class EditorPreviewProvider : IPreviewProvider {
        private readonly Editor _editor;

        public EditorPreviewProvider(Editor editor) {
            _editor = editor;
        }

        public void Cleanup() {}

        public bool HasPreviewGUI() => this._editor.HasPreviewGUI();

        public GUIContent GetPreviewTitle() => this._editor.GetPreviewTitle();

        public void OnPreviewSettings() => this._editor.OnPreviewSettings();

        public void DrawPreview(Rect previewArea) => this._editor.DrawPreview(previewArea);
    }
}