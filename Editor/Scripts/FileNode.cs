namespace WorkBoard {
    using System.IO;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor;
    using UnityEditor.UIElements;

    public class FileNode : BoardNode<FileData> {
        private Object asset => data.asset;
        private InspectorElement inspectorElement;

        public FileNode(FileData data) : base (data){
            this.title = asset.name;
            this.titleContainer.Insert(0, new Image()
            {
                image = AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(asset))
            });
            this.expanded = false;

            RefreshInspectorElement();

            this.mainContainer.RegisterCallback<MouseDownEvent>(OnClick);
        }

        private void OnClick(MouseDownEvent e) {
            if (e.clickCount == 2) {
                EditorGUIUtility.PingObject(asset);
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
            evt.menu.AppendAction("Ping", PingAsset);
            if (asset is DefaultAsset) {
                var path = AssetDatabase.GetAssetPath(asset);
                if (AssetDatabase.IsValidFolder(path)) {
                    evt.menu.AppendAction("Expand Folder Content", ExpandFolderContent);
                }
            } else {
                evt.menu.AppendAction("Expand Children References", ExpandChildrenReferences);
            }
            evt.menu.AppendAction("Show Inspector", ShowInspector, (data.showInspector ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.None) | DropdownMenuAction.Status.Normal);
            evt.menu.AppendSeparator();
            base.BuildContextualMenu(evt);
        }

        private void PingAsset(DropdownMenuAction action) {
            EditorGUIUtility.PingObject(asset);
        }

        private void ExpandFolderContent(DropdownMenuAction obj) {
            var path = AssetDatabase.GetAssetPath(asset);
            Vector2 pos = GetNewChildPosition();
            foreach (var subFolder in AssetDatabase.GetSubFolders(path)) {
                CreateChild(subFolder, pos);
                pos += 100 * Vector2.up;
            }

            foreach (var p in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly)) {
                if (p.EndsWith(".meta")) continue;
                CreateChild(p, pos);
                pos += 100 * Vector2.up;
            }
        }

        private void ExpandChildrenReferences(DropdownMenuAction action) {
            var so = new SerializedObject(asset);
            Vector2 pos = GetNewChildPosition();
            for (var iter = so.GetIterator(); iter.Next(true);) {
                if ((iter.propertyType == SerializedPropertyType.ObjectReference || iter.propertyType == SerializedPropertyType.ManagedReference) && iter.objectReferenceValue != null) {
                    var path = AssetDatabase.GetAssetPath(iter.objectReferenceValue);
                    if (!string.IsNullOrEmpty(path)) {
                        CreateChild(iter.objectReferenceValue, pos);
                        pos += 100 * Vector2.up;
                    }
                }
            }
            so.Dispose();
        }

        private void ShowInspector(DropdownMenuAction action) {
            data.showInspector = !data.showInspector;
            RefreshInspectorElement();
        }

        private void RefreshInspectorElement() {
            if (data.showInspector) {
                if (inspectorElement == null) {
                    inspectorElement = new InspectorElement(asset)
                    {
                        style = { minWidth = 320 }
                    };
                    extensionContainer.Add(inspectorElement);
                    expanded = true;
                }
            } else {
                if (inspectorElement != null) {
                    extensionContainer.Remove(inspectorElement);
                    inspectorElement = null;
                }
            }
        }

        private void CreateChild(string path, Vector2 pos) {
            CreateChild(AssetDatabase.LoadAssetAtPath<Object>(path), pos);
        }

        private void CreateChild(Object o, Vector2 pos) {
            base.CreateChild(new FileData() { asset = o }, pos);
        }

        private Vector2 GetNewChildPosition() {
            var pos = GetPosition();
            return pos.position + (pos.width + 50) * Vector2.right;
        }
    }
}