using System.Collections.Generic;
using Internal;
using UnityEngine;

namespace Global.Audio
{
    public static class AudioPlayerExtensions
    {
        // Вариации одного звука из каталога: берётся случайная, чтобы повторы не звучали одинаково.
        public static void PlayRandomFromGroup(this IAudioPlayer player, IReadOnlyList<Sound> group)
        {
            if (group.Count == 0)
                return;

            player.PlaySound(group[Random.Range(0, group.Count)]);
        }
    }
}
