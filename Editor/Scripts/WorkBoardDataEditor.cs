namespace WorkBoard {
    using UnityEngine;
    using UnityEditor;

    [CustomEditor(typeof(WorkBoardData))]
    public class WorkBoardDataEditor : Editor {
        public override void OnInspectorGUI() {
            if (GUILayout.Button("Open")) {
                switch (Event.current.button) {
                case 0:
                    WorkBoardWindow.OpenBoard((WorkBoardData)target);
                    break;
                case 1:
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Open In New Window"), false, () => WorkBoardWindow.OpenBoardInNewWindow((WorkBoardData)target));
                    menu.ShowAsContext();
                    break;
                }
            }
        }
    }
}