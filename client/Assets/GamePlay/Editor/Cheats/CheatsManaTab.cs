using GamePlay.Cheats;
using UnityEngine.UIElements;

namespace GamePlay.Editor
{
    // Tab of the cheats window: current and max mana of the local player.
    internal static class CheatsManaTab
    {
        public static void Build(VisualElement content)
        {
            content.Add(CheatsUi.SectionLabel("Current Mana"));
            content.Add(CheatsUi.ButtonRow(
                ("Zero", () => GameCheatsAccess.Require().ChangeMana(-10000), CheatsButtonColor.Red),
                ("-1", () => GameCheatsAccess.Require().ChangeMana(-1), CheatsButtonColor.Orange),
                ("+1", () => GameCheatsAccess.Require().ChangeMana(1), CheatsButtonColor.Green),
                ("Full", () => GameCheatsAccess.Require().RestoreMana(), CheatsButtonColor.Blue)));

            content.Add(CheatsUi.Separator());
            content.Add(CheatsUi.SectionLabel("Max Mana"));
            content.Add(CheatsUi.ButtonRow(
                ("-1", () => GameCheatsAccess.Require().ChangeMaxMana(-1), CheatsButtonColor.Orange),
                ("+1", () => GameCheatsAccess.Require().ChangeMaxMana(1), CheatsButtonColor.Green),
                ("= 5", () => GameCheatsAccess.Require().SetMaxMana(5), CheatsButtonColor.Blue),
                ("= 10", () => GameCheatsAccess.Require().SetMaxMana(10), CheatsButtonColor.Blue),
                ("= 20", () => GameCheatsAccess.Require().SetMaxMana(20), CheatsButtonColor.Blue)));
        }
    }
}
