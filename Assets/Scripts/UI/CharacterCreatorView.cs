using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Player;
using BellwortBurrow.Systems;

namespace BellwortBurrow.UI
{
    /// <summary>
    /// The character creator (ui-2). Fills every Option Stepper in the creator's UXML from the catalogs, keeps a draft
    /// profile, shows it on the preview critter, and on Begin starts a new game and loads the first scene.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CharacterCreatorView : MonoBehaviour
    {
        [SerializeField] CritterCatalog critterCatalog;
        [SerializeField] CharacterOptions characterOptions;
        [Tooltip("The critter on the left. Its Follow Profile is off, because this view dresses it.")]
        [SerializeField] PlayerAppearance preview;
        [Tooltip("The scene Begin loads. The Forlorn Forest for now, until the story has an opening.")]
        [SerializeField] string firstScene = "ForlornForest";

        readonly List<OptionStepper> rows = new();
        PlayerProfile draft;
        TextField nameField;
        Button beginButton;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            draft ??= AppearanceChoices.Defaults(critterCatalog, characterOptions);

            nameField = root.Q<TextField>("name");
            beginButton = root.Q<Button>("begin");
            root.Query<OptionStepper>().ToList(rows);

            foreach (var row in rows)
            {
                row.Stepped += OnStepped;
                ShowValue(row);
            }

            if (nameField != null)
            {
                nameField.SetValueWithoutNotify(draft.name);
                nameField.RegisterValueChangedCallback(OnNameChanged);
                root.schedule.Execute(() => nameField.Focus());
            }

            if (beginButton != null) beginButton.clicked += Begin;
            RefreshBegin();

            if (preview != null) preview.Apply(draft);
        }

        void OnDisable()
        {
            foreach (var row in rows) row.Stepped -= OnStepped;
            rows.Clear();
            nameField?.UnregisterValueChangedCallback(OnNameChanged);
            if (beginButton != null) beginButton.clicked -= Begin;
        }

        void OnStepped(OptionStepper row, int steps)
        {
            var choices = AppearanceChoices.For(row.Part, critterCatalog, characterOptions);
            draft.Set(row.Part, AppearanceChoices.Step(choices, draft.Get(row.Part), steps));
            ShowValue(row);
            if (preview != null) preview.Apply(draft);
        }

        void ShowValue(OptionStepper row)
        {
            var choices = AppearanceChoices.For(row.Part, critterCatalog, characterOptions);
            row.ValueText = AppearanceChoices.DisplayName(choices, draft.Get(row.Part));
            row.SetSingleChoice(choices.Count <= 1);
        }

        void OnNameChanged(ChangeEvent<string> evt)
        {
            draft.name = evt.newValue;
            RefreshBegin();
        }

        void RefreshBegin() => beginButton?.SetEnabled(!string.IsNullOrWhiteSpace(draft.name));

        void Begin()
        {
            if (string.IsNullOrWhiteSpace(draft.name)) return;
            if (!ServiceLocator.TryGet<IGameSession>(out var session))
            {
                Debug.LogError("[Character Creator] There's no game session, so the game can't start. Is Assets/Resources/GameRoot.prefab there?");
                return;
            }

            var profile = draft.Clone();
            profile.name = profile.name.Trim();
            session.StartNewGame(profile, firstScene);
            SceneManager.LoadScene(firstScene);
        }
    }
}
