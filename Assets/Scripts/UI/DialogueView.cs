using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Dialogue;

namespace BellwortBurrow.UI
{
    public class DialogueView : MonoBehaviour
    {
        IDialogueService dialogueService;

        void Start()
        {
            dialogueService = ServiceLocator.Get<IDialogueService>();
        }

        public void Show(DialogueDefinition dialogue)
        {
            if (dialogueService.TryStart(dialogue, out var line))
                Display(line);
        }

        void Display(DialogueLine line)
        {
        }
    }
}
