using GamePlay.Cheats;
using UnityEngine.UIElements;

namespace GamePlay.Editor
{
    // Tab of the cheats window: current and max turns of the local player.
    internal static class CheatsMovesTab
    {
        public static void Build(VisualElement content)
        {
            content.Add(CheatsUi.SectionLabel("Current Turns"));
            content.Add(CheatsUi.ButtonRow(
                ("Zero", () => GameCheatsAccess.Require().ChangeMoves(-10000), CheatsButtonColor.Red),
                ("-1", () => GameCheatsAccess.Require().ChangeMoves(-1), CheatsButtonColor.Orange),
                ("+1", () => GameCheatsAccess.Require().ChangeMoves(1), CheatsButtonColor.Green),
                ("Full", () => GameCheatsAccess.Require().RestoreMoves(), CheatsButtonColor.Blue)));

            content.Add(CheatsUi.Separator());
            content.Add(CheatsUi.SectionLabel("Max Turns"));
            content.Add(CheatsUi.ButtonRow(
                ("-1", () => GameCheatsAccess.Require().ChangeMaxMoves(-1), CheatsButtonColor.Orange),
                ("+1", () => GameCheatsAccess.Require().ChangeMaxMoves(1), CheatsButtonColor.Green),
                ("= 5", () => GameCheatsAccess.Require().SetMaxMoves(5), CheatsButtonColor.Blue),
                ("= 10", () => GameCheatsAccess.Require().SetMaxMoves(10), CheatsButtonColor.Blue),
                ("= 20", () => GameCheatsAccess.Require().SetMaxMoves(20), CheatsButtonColor.Blue)));
        }
    }
}
