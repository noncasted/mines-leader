using Global.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Global.Tests
{
    // The saves of the players (ItchSaves, JsonUtility) were written when SettingsSave had public setters: the format is
    // the backing fields of the properties, and the save with intent methods reads and writes the same JSON.
    public class SettingsSaveFormatTests
    {
        // A save of the old SettingsSave ({ get; set; } properties with [field: SerializeField]) as ItchSaves stored it.
        private const string OldSave =
            "{\"<MasterVolume>k__BackingField\":0.25,\"<SoundsVolume>k__BackingField\":0.5," +
            "\"<MusicVolume>k__BackingField\":0.75,\"<ShakeIntensity>k__BackingField\":0.125," +
            "\"<VSync>k__BackingField\":false,\"<WasChanged>k__BackingField\":true}";

        [Test]
        public void OldSave_Loaded_ValuesInPlace()
        {
            var save = JsonUtility.FromJson<SettingsSave>(OldSave);

            Assert.That(save.MasterVolume, Is.EqualTo(0.25f));
            Assert.That(save.SoundsVolume, Is.EqualTo(0.5f));
            Assert.That(save.MusicVolume, Is.EqualTo(0.75f));
            Assert.That(save.ShakeIntensity, Is.EqualTo(0.125f));
            Assert.That(save.VSync, Is.False);
            Assert.That(save.WasChanged, Is.True);
        }

        [Test]
        public void ChangedByIntentMethods_Saved_SameFormatAsTheOldSave()
        {
            var save = new SettingsSave();
            save.SetMasterVolume(0.25f);
            save.SetSoundsVolume(0.5f);
            save.SetMusicVolume(0.75f);
            save.SetShakeIntensity(0.125f);
            save.SetVSync(false);
            save.MarkChanged();

            Assert.That(JsonUtility.ToJson(save), Is.EqualTo(OldSave));
        }

        [Test]
        public void Copy_ValuesWithoutTheChangedMark()
        {
            var save = JsonUtility.FromJson<SettingsSave>(OldSave);

            var copy = save.Copy();

            Assert.That(copy.MasterVolume, Is.EqualTo(0.25f));
            Assert.That(copy.SoundsVolume, Is.EqualTo(0.5f));
            Assert.That(copy.MusicVolume, Is.EqualTo(0.75f));
            Assert.That(copy.ShakeIntensity, Is.EqualTo(0.125f));
            Assert.That(copy.VSync, Is.False);
            Assert.That(copy.WasChanged, Is.False);
        }
    }
}
