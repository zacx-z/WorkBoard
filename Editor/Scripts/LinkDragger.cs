using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorkBoard {
    public class LinkDragger : Image {
        private readonly Object _targetAsset;

        public LinkDragger(FileData fileData) {
            _targetAsset = fileData.asset;
            this.image = AssetDatabase.LoadAssetAtPath<Texture>("Packages/com.nelasystem.workboard/Editor/Icons/LinkShare.png");
            this.AddToClassList("link-dragger");
            this.RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        private void OnMouseDown(MouseDownEvent evt) {
            evt.StopPropagation();

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { _targetAsset };
            DragAndDrop.paths = new[] { AssetDatabase.GetAssetPath(_targetAsset) };
            DragAndDrop.StartDrag(_targetAsset.name);
        }
    }
}