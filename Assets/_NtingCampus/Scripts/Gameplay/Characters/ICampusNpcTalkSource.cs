using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public interface ICampusNpcTalkSource
    {
        bool IsTalkAvailable { get; }
        string ResolveInteractionPrompt(GameObject actor);
        bool TryTalk(GameObject actor, out string spokenLine);
    }
}
