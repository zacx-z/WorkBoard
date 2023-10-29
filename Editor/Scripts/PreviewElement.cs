namespace WorkBoard {
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class PreviewElement : VisualElement {
        public static readonly GUIStyle preToolbarLabel = "ToolbarBoldLabel";
        public static GUIStyle preBackground = nameof(preBackground);
        private readonly Object _target;
        private readonly ObjectPreview _preview;
        private IMGUIContainer _previewElement;

        public PreviewElement(Object target, ObjectPreview preview) {
            _target = target;
            _preview = preview;
            _previewElement = new IMGUIContainer();
            _previewElement.onGUIHandler = DrawPreview;
            this.hierarchy.Add(_previewElement);
        }

        private void DrawPreview() {
            GUI.color = Color.white;

            var thatControlsPreview = _preview;
            bool flag = thatControlsPreview != null && thatControlsPreview.HasPreviewGUI();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(21f));
            string text = string.Empty;
            if (thatControlsPreview != null) text = thatControlsPreview.GetPreviewTitle().text;
            GUILayout.Label(text, preToolbarLabel);
            GUILayout.FlexibleSpace();
            if (flag) thatControlsPreview.OnPreviewSettings();
            EditorGUILayout.EndHorizontal();

            var previewArea = GUILayoutUtility.GetRect(0.0f, 1024f, 64f, 1024f);
            if (Event.current.type == EventType.Repaint) preBackground.Draw(previewArea, false, false, false, false);
            if (thatControlsPreview == null || !thatControlsPreview.HasPreviewGUI()) return;
            thatControlsPreview.DrawPreview(previewArea);
        }
    }
}