using System;

namespace WorkBoard {
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class PreviewElement : VisualElement {
        private readonly Object _target;
        public readonly Type previewType;
        private ObjectPreview _preview;
        private IMGUIContainer _previewElement;

        public PreviewElement(Object target, Type previewType) {
            _target = target;
            this.previewType = previewType;
            _previewElement = new IMGUIContainer();
            _previewElement.onGUIHandler = DrawPreview;
            this.hierarchy.Add(_previewElement);

            this.RegisterCallback<DetachFromPanelEvent>(this.OnDetachFromPanel);
        }

        private void DrawPreview() {
            GUI.color = Color.white;

            _preview ??= InspectorUtils.GetPreviewForTarget(new[] { _target }, previewType);

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
}