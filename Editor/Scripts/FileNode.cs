namespace WorkBoard {
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
                Debug.Log(asset.name);
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
            evt.menu.AppendAction("Ping", PingAsset);
            evt.menu.AppendAction("Select", SelectAsset);
            evt.menu.AppendSeparator();
            base.BuildContextualMenu(evt);
        }

        private void PingAsset(DropdownMenuAction action) {
            // TODO support multiple ping
            EditorGUIUtility.PingObject(asset);
        }

        private void SelectAsset(DropdownMenuAction action) {
            // TODO support multiple selection
        }
    }
}