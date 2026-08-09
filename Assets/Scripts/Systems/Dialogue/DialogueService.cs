using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Dialogue
{
    public class DialogueService : IDialogueService
    {
        public bool TryStart(DialogueDefinition dialogue, out DialogueLine currentLine)
        {
            return dialogue.TryGetLine(dialogue.StartLineId, out currentLine);
        }

        public bool TryAdvance(DialogueDefinition dialogue, string fromLineId, out DialogueLine nextLine)
        {
            if (!dialogue.TryGetLine(fromLineId, out var current) || string.IsNullOrEmpty(current.nextLineId))
            {
                nextLine = default;
                return false;
            }

            return dialogue.TryGetLine(current.nextLineId, out nextLine);
        }
    }
}
