using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>Presentation contract for temporary character color flashes.</summary>
    public interface ICharacterFlashFeedback
    {
        void PlayFlash(Color color, float duration);
    }
}
