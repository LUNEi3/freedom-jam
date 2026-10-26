using System.Collections.Generic;

namespace FreedomJam.Game.Player
{
    public readonly struct LevelUpChoice
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Description;

        public LevelUpChoice(string id, string title, string description)
        {
            Id = id;
            Title = title;
            Description = description;
        }
    }

    /// <summary>Provides upgrade choices for a reusable level-up selection flow.</summary>
    public interface ILevelUpChoiceSource
    {
        void AppendAvailableChoices(List<LevelUpChoice> choices);
        bool TryApplyChoice(string id);
    }
}
