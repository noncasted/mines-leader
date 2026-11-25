using GamePlay.Cheats;
using UnityEngine.UIElements;

namespace GamePlay.Editor
{
    // Tab of the cheats window: current and max health of the local player, the end of the match.
    internal static class CheatsHealthTab
    {
        public static void Build(VisualElement content)
        {
            content.Add(CheatsUi.SectionLabel("Current Health"));
            content.Add(CheatsUi.ButtonRow(
                ("Zero", () => GameCheatsAccess.Require().ChangeHealth(-10000), CheatsButtonColor.Red),
                ("-1", () => GameCheatsAccess.Require().ChangeHealth(-1), CheatsButtonColor.Orange),
                ("+1", () => GameCheatsAccess.Require().ChangeHealth(1), CheatsButtonColor.Green),
                ("Full", () => GameCheatsAccess.Require().RestoreHealth(), CheatsButtonColor.Blue)));

            content.Add(CheatsUi.Separator());
            content.Add(CheatsUi.SectionLabel("Max Health"));
            content.Add(CheatsUi.ButtonRow(
                ("-1", () => GameCheatsAccess.Require().ChangeMaxHealth(-1), CheatsButtonColor.Orange),
                ("+1", () => GameCheatsAccess.Require().ChangeMaxHealth(1), CheatsButtonColor.Green),
                ("= 5", () => GameCheatsAccess.Require().SetMaxHealth(5), CheatsButtonColor.Blue),
                ("= 10", () => GameCheatsAccess.Require().SetMaxHealth(10), CheatsButtonColor.Blue),
                ("= 20", () => GameCheatsAccess.Require().SetMaxHealth(20), CheatsButtonColor.Blue)));

            content.Add(CheatsUi.Separator());
            content.Add(CheatsUi.SectionLabel("Match"));
            content.Add(CheatsUi.ButtonRow(
                ("Win", () => GameCheatsAccess.Require().EndMatch(true), CheatsButtonColor.Green),
                ("Lose", () => GameCheatsAccess.Require().EndMatch(false), CheatsButtonColor.Red)));
        }
    }
}
