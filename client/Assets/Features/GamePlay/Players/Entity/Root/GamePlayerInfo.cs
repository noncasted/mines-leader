using System;
using Global.GameServices;

namespace GamePlay.Players
{
    public interface IGamePlayerInfo
    {
        Guid Id { get; }
        bool IsLocal { get; }
        CharacterType SelectedCharacter { get; }
    }
    
    public class GamePlayerInfo : IGamePlayerInfo
    {
        public GamePlayerInfo(Guid id, bool isLocal, CharacterType selectedCharacter)
        {
            Id = id;
            IsLocal = isLocal;
            SelectedCharacter = selectedCharacter;
        }

        public Guid Id { get; }
        public bool IsLocal { get; }
        public CharacterType SelectedCharacter { get; }
    }
}