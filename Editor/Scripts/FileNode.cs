namespace WorkBoard {
    using System.IO;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor;

    public class FileNode : BoardNode<FileData> {
        private Object asset => data.asset;

        public FileNode(FileData data) : base (data){
            this.title = asset.name;
            this.titleContainer.Insert(0, new Image()
            {
                image = AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(asset))
            });
            this.expanded = false;
            
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
            }
            evt.menu.AppendSeparator();
            base.BuildContextualMenu(evt);
        }

        private void PingAsset(DropdownMenuAction action) {
            EditorGUIUtility.PingObject(asset);
        }

        private void ExpandFolderContent(DropdownMenuAction obj) {
            var path = AssetDatabase.GetAssetPath(asset);
            Vector2 pos = GetPosition().position + 200 * Vector2.right;
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

        private void CreateChild(string path, Vector2 pos) {
            base.CreateChild(new FileData() { asset = AssetDatabase.LoadAssetAtPath<Object>(path) }, pos);
            Debug.Log(path);
        }
    }
}