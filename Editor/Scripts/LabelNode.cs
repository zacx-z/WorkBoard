using System;

namespace WorkBoard {
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor;
    using UnityEditor.Experimental.GraphView;

    [BoardElementDataType(typeof(LabelData))]
    public class LabelNode : GraphElement, IBoardElement {
        private readonly Label _label;
        private readonly TextField _textField;
        public BoardNodeData Data => _data;
        private readonly LabelData _data;
        public event Action onWillChange;
        public void SubscribeWillChange(Action listener) {
            onWillChange += listener;
        }

        public LabelNode(LabelData data) {
            _data = data;
            this.styleSheets.Add(EditorGUIUtility.Load("StyleSheets/GraphView/Selectable.uss") as StyleSheet);
            this.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.nelasystem.workboard/Editor/StyleSheets/LabelNode.uss"));

            this.capabilities = Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable | Capabilities.Ascendable | Capabilities.Copiable;
            this.hierarchy.Add(_label = new Label()
            {
                text = "New Label",
                name = "label"
            });
            this.hierarchy.Add(_textField = new TextField()
            {
                name = "label-field"
            });
            _textField.style.display = DisplayStyle.None;
            _textField.RegisterCallback<ChangeEvent<string>>(OnTextChange);
            _textField.Q(TextInputBaseField<string>.textInputUssName).RegisterCallback<BlurEvent>(OnTextBlur);

            _label.RegisterCallback<MouseDownEvent>(OnLabelMouseDown);
            this.hierarchy.Add(new VisualElement()
            {
                name = "selection-border",
                pickingMode = PickingMode.Ignore
            });
            this.AddToClassList("selectable");
            this.AddToClassList("label-node");
        }

        private void OnTextChange(ChangeEvent<string> evt) {
            _label.text = evt.newValue;
        }

        private void OnLabelMouseDown(MouseDownEvent evt) {
            if (evt.button != 0 || evt.clickCount != 2) return;
            _textField.value = _label.text;
            _textField.style.display = DisplayStyle.Flex;
            UpdateTextFieldRect();
            _label.RegisterCallback<GeometryChangedEvent>(OnLabelRelayout);
            _textField.Q(TextInputBaseField<string>.textInputUssName).Focus();
            _textField.SelectAll();
            evt.StopPropagation();
            evt.PreventDefault();
        }

        private void OnTextBlur(BlurEvent evt) {
            _data.label = _textField.value;
            _textField.style.display = DisplayStyle.None;
            _label.UnregisterCallback<GeometryChangedEvent>(OnLabelRelayout);
            onWillChange?.Invoke();
        }

        private void OnLabelRelayout(GeometryChangedEvent e) => this.UpdateTextFieldRect();

        private void UpdateTextFieldRect()
        {
            Rect layout = _label.layout;
            _label.parent.ChangeCoordinatesTo(_label.parent, layout);
            _textField.style.left = layout.xMin - 1f;
            _textField.style.right = layout.yMin + this._label.resolvedStyle.marginTop;
            _textField.style.width = layout.width - this._label.resolvedStyle.marginLeft - this._label.resolvedStyle.marginRight;
            _textField.style.height = layout.height - this._label.resolvedStyle.marginTop - this._label.resolvedStyle.marginBottom;
        }

        public override Rect GetPosition()
        {
          if (this.resolvedStyle.position != Position.Absolute)
            return this.layout;
          double left = this.resolvedStyle.left;
          double top = this.resolvedStyle.top;
          Rect layout = this.layout;
          double width = layout.width;
          layout = this.layout;
          double height = layout.height;
          return new Rect((float) left, (float) top, (float) width, (float) height);
        }

        public override void SetPosition(Rect newPos)
        {
          this.style.position = Position.Absolute;
          this.style.left = newPos.x;
          this.style.top = newPos.y;
        }
    }
}