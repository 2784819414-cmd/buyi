namespace NtingCampus.Gameplay.Core
{
    public interface ICampusInteractionActionProvider
    {
        string ProviderId { get; }

        /// <summary>
        /// Return true only when this provider owns the requested action. Keep execution actor-based:
        /// use context.Actor and context.SourceObject instead of reading the player singleton.
        /// </summary>
        bool TryHandle(CampusInteractionActionContext context, out string message);
    }

    public interface ICampusInteractionPromptOverrideProvider
    {
        bool TryResolvePrompt(CampusInteractionActionContext context, out string prompt);
    }
}
