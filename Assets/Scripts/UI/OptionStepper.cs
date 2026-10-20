using System;
using UnityEngine.UIElements;
using BellwortBurrow.Data;

namespace BellwortBurrow.UI
{
    /// <summary>
    /// One character creator row (ui-2): a label, then ‹ value ›. Left and right (the arrow keys, a stick or the arrow buttons)
    /// step through the row's choices; up and down move to the next row. In UI Builder it's under Library > Project >
    /// Custom Controls: drop one in and set its Part and Label.
    /// </summary>
    [UxmlElement]
    public partial class OptionStepper : VisualElement
    {
        public const string UssClassName = "option-stepper";
        public const string SingleChoiceUssClassName = UssClassName + "--single";

        readonly Label label;
        readonly Label valueLabel;

        /// <summary>Raised with -1 or +1 when the player steps this row.</summary>
        public event Action<OptionStepper, int> Stepped;

        /// <summary>Which part of the player this row changes.</summary>
        [UxmlAttribute]
        public AppearancePart Part { get; set; }

        [UxmlAttribute("label")]
        public string LabelText
        {
            get => label.text;
            set => label.text = value;
        }

        public string ValueText
        {
            get => valueLabel.text;
            set => valueLabel.text = value;
        }

        public OptionStepper()
        {
            AddToClassList(UssClassName);
            focusable = true;

            label = new Label { name = "label" };
            label.AddToClassList(UssClassName + "__label");

            var previous = new Button(() => Step(-1)) { name = "previous", text = "‹", focusable = false };
            previous.AddToClassList(UssClassName + "__arrow");

            valueLabel = new Label { name = "value" };
            valueLabel.AddToClassList(UssClassName + "__value");

            var next = new Button(() => Step(1)) { name = "next", text = "›", focusable = false };
            next.AddToClassList(UssClassName + "__arrow");

            Add(label);
            Add(previous);
            Add(valueLabel);
            Add(next);

            RegisterCallback<NavigationMoveEvent>(OnNavigationMove);
        }

        /// <summary>Marks a row that has only one choice so far, so its arrows show as resting.</summary>
        public void SetSingleChoice(bool single) => EnableInClassList(SingleChoiceUssClassName, single);

        public void Step(int steps) => Stepped?.Invoke(this, steps);

        void OnNavigationMove(NavigationMoveEvent evt)
        {
            int steps = evt.direction switch
            {
                NavigationMoveEvent.Direction.Left => -1,
                NavigationMoveEvent.Direction.Right => 1,
                _ => 0
            };
            if (steps == 0) return;

            Step(steps);
            evt.StopPropagation();
            focusController?.IgnoreEvent(evt);
        }
    }
}
