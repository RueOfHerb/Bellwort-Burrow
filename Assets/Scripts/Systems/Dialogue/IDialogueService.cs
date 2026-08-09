using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Dialogue
{
    public interface IDialogueService
    {
        bool TryStart(DialogueDefinition dialogue, out DialogueLine currentLine);
        bool TryAdvance(DialogueDefinition dialogue, string fromLineId, out DialogueLine nextLine);
    }
}
