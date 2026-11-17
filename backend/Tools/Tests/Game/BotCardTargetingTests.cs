using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Прицеливание карт бота считает только видимое: закрытые клетки, флаги, цифры.
/// Геометрия совпадает с самой картой, поэтому центр проверяется через те же PatternShapes.
/// </summary>
public class BotCardTargetingTests
{
    [Fact]
    public void Bloodhound_CoversTheLargestUnflaggedCluster()
    {
        // Слева два закрытых столбца, справа один: лучший ромб накрывает левый кластер целиком.
        var (board, _) = BoardParser.Parse("""
                                           t t _ _ _ _ t
                                           t t _ _ _ _ t
                                           t t _ _ _ _ t
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           """);

        var centre = BotCardTargeting.BestBloodhoundCentre(board, 3);

        var covered = PatternShapes.Rhombus(3).SelectTaken(board, centre);
        covered.Should().HaveCountGreaterThan(3);
        covered.Should().OnlyContain(c => c.Position.x <= 1);
    }

    [Fact]
    public void Bloodhound_IgnoresFlaggedCells()
    {
        // Левый кластер весь под флагами, правый чистый: ромб идёт направо.
        var (board, _) = BoardParser.Parse("""
                                           f f _ _ _ t t
                                           f f _ _ _ t t
                                           f f _ _ _ t t
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           """);

        var centre = BotCardTargeting.BestBloodhoundCentre(board, 3);

        var covered = PatternShapes.Rhombus(3).SelectTaken(board, centre);
        covered.Should().NotBeEmpty();
        covered.Should().OnlyContain(c => c.Position.x >= 5);
    }

    [Fact]
    public void Bloodhound_NothingToCover_ReturnsNone()
    {
        var (board, _) = BoardParser.Parse("""
                                           _ _ _
                                           _ f _
                                           _ _ _
                                           """);

        BotCardTargeting.BestBloodhoundCentre(board, 3).Should().Be(BotCardTargeting.None);
    }

    [Fact]
    public void MinefieldScout_PicksTheLineWithMostClosedCells()
    {
        // Единственная закрытая линия: горизонталь в третьем ряду.
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ t t t t t _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           """);

        var centre = BotCardTargeting.BestMinefieldScoutCentre(board, 4);

        centre.y.Should().Be(2);
        PatternShapes.Line(4, horizontal: true).SelectTaken(board, centre).Should().HaveCount(4);
    }

    [Fact]
    public void Trebuchet_CoversTheLargestOpenArea()
    {
        // Открыт только левый верхний квадрат: ромб должен лечь на него, а не на закрытое поле.
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ t t t t
                                           _ _ _ t t t t
                                           _ _ _ t t t t
                                           t t t t t t t
                                           t t t t t t t
                                           t t t t t t t
                                           t t t t t t t
                                           """);

        var centre = BotCardTargeting.BestTrebuchetCentre(board, 4);

        var covered = PatternShapes.Rhombus(4).SelectFree(board, centre);
        covered.Should().HaveCountGreaterThan(4);
        covered.Should().OnlyContain(c => c.Position.x <= 2 && c.Position.y <= 2);
    }

    [Fact]
    public void Trebuchet_NoOpenCells_ReturnsNone()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);

        BotCardTargeting.BestTrebuchetCentre(board, 4).Should().Be(BotCardTargeting.None);
    }

    [Fact]
    public void Sonar_AimsWhereNumbersPromiseMines()
    {
        // Слева цифра 2 с двумя закрытыми соседями (обе мины по ограничению),
        // справа закрытые клетки без единой цифры рядом. Ромб должен лечь налево.
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ _ _ _ t
                                           m _ _ _ _ _ t
                                           m _ _ _ _ _ t
                                           _ _ _ _ _ _ t
                                           _ _ _ _ _ _ t
                                           _ _ _ _ _ _ t
                                           _ _ _ _ _ _ t
                                           """);

        var centre = BotCardTargeting.BestSonarCentre(board, 4);

        var covered = PatternShapes.Rhombus(4).SelectTaken(board, centre);
        covered.Should().Contain(c => c.Position == new Position(0, 1));
        covered.Should().Contain(c => c.Position == new Position(0, 2));
        covered.Should().NotContain(c => c.Position.x == 6);
    }

    [Fact]
    public void Sonar_NoNumbersWithUnresolvedMines_ReturnsNone()
    {
        // Единственная мина уже под флагом: цифры закрыты, Sonar бить некуда.
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ t
                                           _ f _ t
                                           _ _ _ t
                                           _ _ _ t
                                           """);

        BotCardTargeting.BestSonarCentre(board, 4).Should().Be(BotCardTargeting.None);
        BotCardTargeting.UnresolvedMinesByNumbers(board).Should().Be(0);
    }
}
