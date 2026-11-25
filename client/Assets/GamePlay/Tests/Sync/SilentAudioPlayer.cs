using System;
using Global.Audio;
using Internal;

namespace GamePlay.Tests
{
    // The audio of a test that expects no sound: any call fails. The sound catalogues are not loaded in EditMode, so the
    // records that sound (CellFree, Explosion, a placed flag) are tested on the resolver of a board, not through Handle.
    public class SilentAudioPlayer : IAudioPlayer
    {
        public void PlaySound(Sound sound) => throw new NotSupportedException();
        public void PlaySound(Sound sound, in SoundPlay play) => throw new NotSupportedException();
        public void PlaySound(Sound sound, in SoundPlay play, IReadOnlyLifetime lifetime) => throw new NotSupportedException();
        public void PlayLoopMusic(Sound sound, float fadeIn) => throw new NotSupportedException();
        public void PlayLoopMusic(Sound sound, float fadeIn, IReadOnlyLifetime lifetime) => throw new NotSupportedException();
        public void StopMusic(float fadeOut) => throw new NotSupportedException();
    }
}
