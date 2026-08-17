using System.Collections.Generic;
using Internal;
using Tools;
using UnityEngine;

namespace Global.Audio
{
    public static class GlobalAudioExtensions
    {
        public static IScopeBuilder AddAudio(this IScopeBuilder builder)
        {
            builder.RegisterComponent(Prefabs.GlobalAudioPlayer.As<AudioPlayer>())
                   .As<IAudioVolume>()
                   .As<IAudioPlayer>()
                   .As<IScopeSetup>();

            builder.RegisterComponent(Prefabs.GlobalAudioListener.As<AudioListener>())
                   .As<IAudioListener>()
                   .As<IScopeBaseSetup>();

            return builder;
        }
    }

    [PrefabDefinition]
    public static class AudioPlayerPrefab
    {
        public static void Define(PrefabBuilder builder)
        {
            var soundSources = new List<AudioSource>();

            for (var i = 0; i < 20; i++)
            {
                var soundSource = builder.WithChild<AudioSource>($"Sound_{i}");
                soundSource.volume = 0.007f;
                soundSource.playOnAwake = false;
                soundSource.loop = false;
                soundSources.Add(soundSource);
            }

            var musicSource = builder.WithChild<AudioSource>("Music");
            musicSource.volume = 0.037f;
            musicSource.playOnAwake = false;
            musicSource.loop = true;

            builder
                .WithName("Global/Global_Audio_Player")
                .WithComponent<AudioPlayer>(player => {
                    player.Configure(musicSource, soundSources.ToArray());
                });
        }
    }

    [PrefabDefinition]
    public static class AudioListenerPrefab
    {
        public static void Define(PrefabBuilder builder)
        {
            builder
                .WithName("Global/Global_Audio_Listener")
                .WithComponent<UnityEngine.AudioListener>()
                .WithComponent<AudioListener>();
        }
    }
}