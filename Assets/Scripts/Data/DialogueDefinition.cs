using System;
using UnityEngine;

namespace BellwortBurrow.Data
{
    [Serializable]
    public struct DialogueLine
    {
        public string id;
        public string speakerId;
        [TextArea] public string text;
        public string nextLineId;
    }

    [CreateAssetMenu(menuName = "Bellwort Burrow/Dialogue/Dialogue Definition", fileName = "NewDialogue")]
    public class DialogueDefinition : ScriptableObject
    {
        [SerializeField] string startLineId;
        [SerializeField] DialogueLine[] lines = Array.Empty<DialogueLine>();

        public string StartLineId => startLineId;
        public DialogueLine[] Lines => lines;

        public bool TryGetLine(string id, out DialogueLine line)
        {
            foreach (var l in lines)
            {
                if (l.id == id)
                {
                    line = l;
                    return true;
                }
            }
            line = default;
            return false;
        }

        internal void ConfigureForTesting(string startLineId, DialogueLine[] lines)
        {
            this.startLineId = startLineId;
            this.lines = lines;
        }
    }
}
