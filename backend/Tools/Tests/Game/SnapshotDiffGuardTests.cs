using Common;
using FluentAssertions;
using Game.GamePlay;
using Game.GamePlay.Snapshots;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class SnapshotApplierTests
{
    private static GameStateSnapshot CreateBaseState(Guid playerId)
    {
        return new GameStateSnapshot
        {
            Players = new Dictionary<Guid, PlayerStateSnapshot>
            {
                {
                    playerId, new PlayerStateSnapshot
                    {
                        ManaCurrent = 3,
                        ManaMax = 5,
                        HealthCurrent = 10,
                        HealthMax = 10,
                        MovesLeft = 2,
                        MovesMax = 2,
                        MovesIsAvailable = true,
                        Modifiers = new Dictionary<PlayerModifier, float>
                        {
                            { PlayerModifier.AdditionalMana, 0f },
                            { PlayerModifier.AdditionalMoves, 0f }
                        },
                        Hand = new Dictionary<Guid, CardType>()
                    }
                }
            },
            Boards = new Dictionary<Guid, BoardStateSnapshot>
            {
                {
                    playerId, new BoardStateSnapshot
                    {
                        Cells = new Dictionary<Position, CellStateSnapshot>
                        {
                            { new Position(0, 0), new CellStateSnapshot { Status = CellStatus.Taken } },
                            { new Position(1, 0), new CellStateSnapshot { Status = CellStatus.Taken } }
                        }
                    }
                }
            }
        };
    }

    private static SharedMoveSnapshot ToRecords(params IMoveSnapshotRecord[] records)
    {
        return new SharedMoveSnapshot { Records = records };
    }

    [Fact]
    public void Apply_ManaUpdate_SetsCurrentAndMax()
    {
        var playerId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        var records = ToRecords(new PlayerSnapshotRecord.ManaUpdate
        {
            PlayerId = playerId,
            Current = 7,
            Max = 9
        });

        var post = SnapshotApplier.Apply(pre, records);

        post.Players[playerId].ManaCurrent.Should().Be(7);
        post.Players[playerId].ManaMax.Should().Be(9);
    }

    [Fact]
    public void Apply_HealthUpdate_SetsCurrentAndMax()
    {
        var playerId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        var records = ToRecords(new PlayerSnapshotRecord.HealthUpdate
        {
            PlayerId = playerId,
            Current = 5,
            Max = 15
        });

        var post = SnapshotApplier.Apply(pre, records);

        post.Players[playerId].HealthCurrent.Should().Be(5);
        post.Players[playerId].HealthMax.Should().Be(15);
    }

    [Fact]
    public void Apply_MovesUpdate_SetsAllFields()
    {
        var playerId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        var records = ToRecords(new PlayerSnapshotRecord.MovesUpdate
        {
            PlayerId = playerId,
            Left = 0,
            Max = 3,
            IsAvailable = false
        });

        var post = SnapshotApplier.Apply(pre, records);

        post.Players[playerId].MovesLeft.Should().Be(0);
        post.Players[playerId].MovesMax.Should().Be(3);
        post.Players[playerId].MovesIsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Apply_ModifierUpdate_OverwritesValue()
    {
        var playerId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        var records = ToRecords(new PlayerSnapshotRecord.ModifierUpdate
        {
            PlayerId = playerId,
            Modifier = PlayerModifier.AdditionalMana,
            Value = 3f
        });

        var post = SnapshotApplier.Apply(pre, records);

        post.Players[playerId].Modifiers[PlayerModifier.AdditionalMana].Should().Be(3f);
    }

    [Fact]
    public void Apply_CardAdd_InsertsIntoHand()
    {
        var playerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        var records = ToRecords(new PlayerSnapshotRecord.CardAdd
        {
            PlayerId = playerId,
            CardId = cardId,
            Type = CardType.Bloodhound
        });

        var post = SnapshotApplier.Apply(pre, records);

        post.Players[playerId].Hand.Should().ContainKey(cardId);
        post.Players[playerId].Hand[cardId].Should().Be(CardType.Bloodhound);
    }

    [Fact]
    public void Apply_CardRemove_EvictsFromHand()
    {
        var playerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);
        pre.Players[playerId].Hand[cardId] = CardType.Medic;

        var records = ToRecords(new PlayerSnapshotRecord.CardRemove
        {
            PlayerId = playerId,
            CardId = cardId
        });

        var post = SnapshotApplier.Apply(pre, records);

        post.Players[playerId].Hand.Should().NotContainKey(cardId);
    }

    [Fact]
    public void Apply_CardUse_DoesNotMutateState()
    {
        var playerId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        var records = ToRecords(new PlayerSnapshotRecord.CardUse
        {
            PlayerId = playerId,
            CardId = Guid.NewGuid(),
            Data = new CardActionSnapshot.Medic { TargetPlayer = playerId }
        });

        var post = SnapshotApplier.Apply(pre, records);

        post.Players[playerId].ManaCurrent.Should().Be(pre.Players[playerId].ManaCurrent);
        post.Players[playerId].HealthCurrent.Should().Be(pre.Players[playerId].HealthCurrent);
    }

    [Fact]
    public void Apply_CellTaken_ResetsCellState()
    {
        var playerId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        pre.Boards[playerId].Cells[new Position(0, 0)] = new CellStateSnapshot
        {
            Status = CellStatus.Free,
            MinesAround = 4
        };

        var boardSnapshot = new SharedBoardSnapshot
        {
            BoardOwnerId = playerId,
            Records = new List<IBoardSnapshotRecord>
            {
                new BoardSnapshotRecord.CellTaken { Position = new Position(0, 0) }
            }
        };
        var records = new SharedMoveSnapshot { Records = new IMoveSnapshotRecord[] { boardSnapshot } };

        var post = SnapshotApplier.Apply(pre, records);

        post.Boards[playerId].Cells[new Position(0, 0)].Status.Should().Be(CellStatus.Taken);
        post.Boards[playerId].Cells[new Position(0, 0)].MinesAround.Should().Be(0);
    }

    [Fact]
    public void Apply_CellFree_ResetsFlaggedAndEffects()
    {
        var playerId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        pre.Boards[playerId].Cells[new Position(0, 0)] = new CellStateSnapshot
        {
            Status = CellStatus.Taken,
            IsFlagged = true,
            Effects = new Dictionary<Guid, CellEffectType>
            {
                { Guid.NewGuid(), CellEffectType.Frost }
            }
        };

        var boardSnapshot = new SharedBoardSnapshot
        {
            BoardOwnerId = playerId,
            Records = new List<IBoardSnapshotRecord>
            {
                new BoardSnapshotRecord.CellFree { Position = new Position(0, 0) }
            }
        };
        var records = new SharedMoveSnapshot { Records = new IMoveSnapshotRecord[] { boardSnapshot } };

        var post = SnapshotApplier.Apply(pre, records);

        post.Boards[playerId].Cells[new Position(0, 0)].Status.Should().Be(CellStatus.Free);
        post.Boards[playerId].Cells[new Position(0, 0)].IsFlagged.Should().BeFalse();
        post.Boards[playerId].Cells[new Position(0, 0)].Effects.Should().BeEmpty();
    }

    [Fact]
    public void Apply_Flag_TogglesIsFlagged()
    {
        var playerId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        var boardSnapshot = new SharedBoardSnapshot
        {
            BoardOwnerId = playerId,
            Records = new List<IBoardSnapshotRecord>
            {
                new BoardSnapshotRecord.Flag { Position = new Position(0, 0), IsFlagged = true }
            }
        };
        var records = new SharedMoveSnapshot { Records = new IMoveSnapshotRecord[] { boardSnapshot } };

        var post = SnapshotApplier.Apply(pre, records);

        post.Boards[playerId].Cells[new Position(0, 0)].IsFlagged.Should().BeTrue();
    }

    [Fact]
    public void Apply_MinesAround_UpdatesValue()
    {
        var playerId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);
        pre.Boards[playerId].Cells[new Position(0, 0)] = new CellStateSnapshot { Status = CellStatus.Free };

        var boardSnapshot = new SharedBoardSnapshot
        {
            BoardOwnerId = playerId,
            Records = new List<IBoardSnapshotRecord>
            {
                new BoardSnapshotRecord.MinesAround { Position = new Position(0, 0), Count = 3 }
            }
        };
        var records = new SharedMoveSnapshot { Records = new IMoveSnapshotRecord[] { boardSnapshot } };

        var post = SnapshotApplier.Apply(pre, records);

        post.Boards[playerId].Cells[new Position(0, 0)].MinesAround.Should().Be(3);
    }

    [Fact]
    public void Apply_EffectAddedAndRemoved_UpdatesEffectMap()
    {
        var playerId = Guid.NewGuid();
        var effectId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        var addSnapshot = new SharedBoardSnapshot
        {
            BoardOwnerId = playerId,
            Records = new List<IBoardSnapshotRecord>
            {
                new BoardSnapshotRecord.EffectAdded
                {
                    Position = new Position(0, 0),
                    Type = CellEffectType.Frost,
                    EffectId = effectId
                }
            }
        };

        var postAdd = SnapshotApplier.Apply(pre, new SharedMoveSnapshot
        {
            Records = new IMoveSnapshotRecord[] { addSnapshot }
        });

        postAdd.Boards[playerId].Cells[new Position(0, 0)].Effects.Should().ContainKey(effectId);

        var removeSnapshot = new SharedBoardSnapshot
        {
            BoardOwnerId = playerId,
            Records = new List<IBoardSnapshotRecord>
            {
                new BoardSnapshotRecord.EffectRemoved
                {
                    Position = new Position(0, 0),
                    EffectId = effectId
                }
            }
        };

        var postRemove = SnapshotApplier.Apply(postAdd, new SharedMoveSnapshot
        {
            Records = new IMoveSnapshotRecord[] { removeSnapshot }
        });

        postRemove.Boards[playerId].Cells[new Position(0, 0)].Effects.Should().NotContainKey(effectId);
    }

    [Fact]
    public void Apply_Explosion_DoesNotMutateState()
    {
        var playerId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        var boardSnapshot = new SharedBoardSnapshot
        {
            BoardOwnerId = playerId,
            Records = new List<IBoardSnapshotRecord>
            {
                new BoardSnapshotRecord.Explosion { Position = new Position(0, 0) }
            }
        };
        var records = new SharedMoveSnapshot { Records = new IMoveSnapshotRecord[] { boardSnapshot } };

        var post = SnapshotApplier.Apply(pre, records);

        post.Boards[playerId].Cells[new Position(0, 0)].Status.Should()
            .Be(pre.Boards[playerId].Cells[new Position(0, 0)].Status);
    }

    [Fact]
    public void Apply_DoesNotMutatePre()
    {
        var playerId = Guid.NewGuid();
        var pre = CreateBaseState(playerId);

        var records = ToRecords(new PlayerSnapshotRecord.ManaUpdate
        {
            PlayerId = playerId,
            Current = 99,
            Max = 99
        });

        SnapshotApplier.Apply(pre, records);

        pre.Players[playerId].ManaCurrent.Should().Be(3);
        pre.Players[playerId].ManaMax.Should().Be(5);
    }
}

public class SnapshotDiffCalculatorTests
{
    private static GameStateSnapshot StateWith(Guid playerId, Action<PlayerStateSnapshot> configure)
    {
        var state = new PlayerStateSnapshot
        {
            Modifiers = new Dictionary<PlayerModifier, float>
            {
                { PlayerModifier.AdditionalMana, 0f }
            }
        };
        configure(state);

        return new GameStateSnapshot
        {
            Players = new Dictionary<Guid, PlayerStateSnapshot> { { playerId, state } },
            Boards = new Dictionary<Guid, BoardStateSnapshot>()
        };
    }

    [Fact]
    public void Compute_IdenticalStates_ReturnsEmpty()
    {
        var id = Guid.NewGuid();

        var a = StateWith(id, p => {
            p.ManaCurrent = 3;
            p.ManaMax = 5;
        });

        var b = StateWith(id, p => {
            p.ManaCurrent = 3;
            p.ManaMax = 5;
        });

        var diff = SnapshotDiffCalculator.Compute(a, b);

        diff.Should().BeEmpty();
    }

    [Fact]
    public void Compute_ManaDiffers_ReportsManaDiff()
    {
        var id = Guid.NewGuid();

        var a = StateWith(id, p => {
            p.ManaCurrent = 3;
            p.ManaMax = 5;
        });

        var b = StateWith(id, p => {
            p.ManaCurrent = 7;
            p.ManaMax = 5;
        });

        var diff = SnapshotDiffCalculator.Compute(a, b);

        diff.Should().ContainSingle();
        diff[0].Should().Contain("Mana").And.Contain("3/5").And.Contain("7/5");
    }

    [Fact]
    public void Compute_MissingPlayer_Reports()
    {
        var id = Guid.NewGuid();

        var a = StateWith(id, _ => {
        });
        var b = new GameStateSnapshot();

        var diff = SnapshotDiffCalculator.Compute(a, b);

        diff.Should().ContainSingle(line => line.Contains("missing"));
    }

    [Fact]
    public void Compute_UnexpectedExtraPlayer_Reports()
    {
        var id = Guid.NewGuid();
        var a = new GameStateSnapshot();

        var b = StateWith(id, _ => {
        });

        var diff = SnapshotDiffCalculator.Compute(a, b);

        diff.Should().ContainSingle(line => line.Contains("unexpected"));
    }

    [Fact]
    public void Compute_BoardCellStatusDiffers_Reports()
    {
        var ownerId = Guid.NewGuid();
        var pos = new Position(2, 2);

        var a = new GameStateSnapshot
        {
            Boards = new Dictionary<Guid, BoardStateSnapshot>
            {
                {
                    ownerId, new BoardStateSnapshot
                    {
                        Cells = new Dictionary<Position, CellStateSnapshot>
                        {
                            { pos, new CellStateSnapshot { Status = CellStatus.Free } }
                        }
                    }
                }
            }
        };

        var b = new GameStateSnapshot
        {
            Boards = new Dictionary<Guid, BoardStateSnapshot>
            {
                {
                    ownerId, new BoardStateSnapshot
                    {
                        Cells = new Dictionary<Position, CellStateSnapshot>
                        {
                            { pos, new CellStateSnapshot { Status = CellStatus.Taken } }
                        }
                    }
                }
            }
        };

        var diff = SnapshotDiffCalculator.Compute(a, b);

        diff.Should().ContainSingle(line => line.Contains("Status"));
    }
}

public class SnapshotDiffGuardTests
{
    private static ISnapshotDiffGuard CreateGuard(bool enabled)
    {
        var flags = Substitute.For<IClusterFlags>();
        flags.SnapshotDiffGuardEnabled.Returns(enabled);
        return new SnapshotDiffGuard(flags, NullLogger<SnapshotDiffGuard>.Instance);
    }

    [Fact]
    public void IsEnabled_ReflectsFlag()
    {
        CreateGuard(true).IsEnabled.Should().BeTrue();
        CreateGuard(false).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenDisabled_DoesNotThrowOnDrift()
    {
        var guard = CreateGuard(false);
        var id = Guid.NewGuid();

        var pre = new GameStateSnapshot
        {
            Players = new Dictionary<Guid, PlayerStateSnapshot>
            {
                { id, new PlayerStateSnapshot { ManaCurrent = 1 } }
            }
        };

        var post = new GameStateSnapshot
        {
            Players = new Dictionary<Guid, PlayerStateSnapshot>
            {
                { id, new PlayerStateSnapshot { ManaCurrent = 5 } }
            }
        };
        var records = new SharedMoveSnapshot { Records = Array.Empty<IMoveSnapshotRecord>() };

        var act = () => guard.Validate(pre, records, post, "test");
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenEnabled_NoDrift_DoesNotThrow()
    {
        var guard = CreateGuard(true);
        var id = Guid.NewGuid();

        var pre = new GameStateSnapshot
        {
            Players = new Dictionary<Guid, PlayerStateSnapshot>
            {
                { id, new PlayerStateSnapshot { ManaCurrent = 1, ManaMax = 5 } }
            }
        };

        var records = new SharedMoveSnapshot
        {
            Records = new IMoveSnapshotRecord[]
            {
                new PlayerSnapshotRecord.ManaUpdate { PlayerId = id, Current = 3, Max = 5 }
            }
        };

        var post = new GameStateSnapshot
        {
            Players = new Dictionary<Guid, PlayerStateSnapshot>
            {
                { id, new PlayerStateSnapshot { ManaCurrent = 3, ManaMax = 5 } }
            }
        };

        var act = () => guard.Validate(pre, records, post, "test");
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenEnabled_SilentMutation_ThrowsSnapshotDiffException()
    {
        var guard = CreateGuard(true);
        var id = Guid.NewGuid();

        var pre = new GameStateSnapshot
        {
            Players = new Dictionary<Guid, PlayerStateSnapshot>
            {
                { id, new PlayerStateSnapshot { ManaCurrent = 1, ManaMax = 5 } }
            }
        };
        // No records, but post mana changed — this is the bug diff-guard must catch.
        var records = new SharedMoveSnapshot { Records = Array.Empty<IMoveSnapshotRecord>() };

        var post = new GameStateSnapshot
        {
            Players = new Dictionary<Guid, PlayerStateSnapshot>
            {
                { id, new PlayerStateSnapshot { ManaCurrent = 5, ManaMax = 5 } }
            }
        };

        var act = () => guard.Validate(pre, records, post, "bug");

        act.Should().Throw<SnapshotDiffException>()
           .WithMessage("*Mana*");
    }
}

public class BloodPactSnapshotSequenceTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_InvokesResourceMethodsInChoreographyOrder()
    {
        var owner = MockPlayer();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.AdditionalMana, 0f },
            { PlayerModifier.AdditionalMoves, 0f }
        });
        owner.Mana.Current.Returns(0);

        var calls = new List<string>();

        owner.Health.When(h => h.TakeDamage(Arg.Any<MoveSnapshot>(), Arg.Any<int>()))
             .Do(_ => calls.Add("Health.TakeDamage"));

        owner.Modifiers.When(m => m.Set(Arg.Any<MoveSnapshot>(), PlayerModifier.AdditionalMana, Arg.Any<float>()))
             .Do(_ => calls.Add("Modifiers.Set(AdditionalMana)"));

        owner.Mana.When(m => m.SetCurrent(Arg.Any<MoveSnapshot>(), Arg.Any<int>()))
             .Do(_ => calls.Add("Mana.SetCurrent"));

        owner.Modifiers.When(m => m.Set(Arg.Any<MoveSnapshot>(), PlayerModifier.AdditionalMoves, Arg.Any<float>()))
             .Do(_ => calls.Add("Modifiers.Set(AdditionalMoves)"));

        var snapshot = new MoveSnapshot();
        var card = new BloodPact(MockConfigs(), Substitute.For<IRoundActionService>());

        card.Use(owner, new CardUsePayload.BloodPact { Type = CardType.BloodPact }, snapshot);

        calls.Should().Equal("Health.TakeDamage",
            "Modifiers.Set(AdditionalMana)",
            "Mana.SetCurrent",
            "Modifiers.Set(AdditionalMoves)");
    }
}

public class BloodhoundSnapshotSequenceTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_WritesBoardRecordsGroupedUnderInvokerBoard()
    {
        var ownerId = Guid.NewGuid();

        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t
                                                t t t t t t t
                                                t t t t t t t
                                                t t t x t t t
                                                t t t t t t t
                                                t t t t t t t
                                                t t t t t t t
                                                """);
        var invoker = MockPlayer(ownerId);
        invoker.Board.Returns(board);

        var snapshot = new MoveSnapshot();
        var card = new Bloodhound(MockConfigs());

        card.Use(invoker, new CardUsePayload.Bloodhound { Position = target }, snapshot);

        var records = snapshot.Collect().Records;

        var cardUseRecord = records.OfType<PlayerSnapshotRecord.CardUse>().Should().ContainSingle().Subject;
        var data = cardUseRecord.Data.Should().BeOfType<CardActionSnapshot.Bloodhound>().Subject;
        data.OpenedCells.Should().NotBeEmpty();
    }
}

public class CarpetBombSnapshotSequenceTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_PopulatesTakenCellsAndUpdatedFreeNeighbors()
    {
        var ownerId = Guid.NewGuid();

        // Opponent's board — all free, the bomb plants mines along a line.
        var (board, target) = BoardParser.Parse("""
                                                _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _
                                                _ _ _ x _ _ _ _
                                                _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _
                                                """);

        var invoker = MockPlayer();
        var opponent = MockPlayer(ownerId);
        opponent.Board.Returns(board);
        var ctx = MockGameContext(invoker, opponent);

        var snapshot = new MoveSnapshot();
        var card = new CarpetBomb(MockConfigs(), ctx);

        card.Use(invoker, new CardUsePayload.CarpetBomb { Position = target }, snapshot);

        var records = snapshot.Collect().Records;

        var cardUseRecord = records.OfType<PlayerSnapshotRecord.CardUse>().Should().ContainSingle().Subject;
        var data = cardUseRecord.Data.Should().BeOfType<CardActionSnapshot.CarpetBomb>().Subject;

        data.TakenCells.Should().NotBeNullOrEmpty();
        data.UpdatedFreeCells.Should().NotBeNullOrEmpty("neighbors of the newly-placed mines must be re-recorded");
    }
}