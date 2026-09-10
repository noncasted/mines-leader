namespace Internal.Tests
{
    // 12 marker interfaces, same consume path as EventLoop.ResolveList<T>
    // (ContainerLocal<IReadOnlyList<T>> on VContainer, ResolveAll<T> on own).
    // First 10 map to EventLoop phases; last two map to ISceneService / IEntityComponent.

    internal interface IBenchBaseSetup
    {
    }

    internal interface IBenchBaseSetupAsync
    {
    }

    internal interface IBenchSetup
    {
    }

    internal interface IBenchSetupAsync
    {
    }

    internal interface IBenchSetupCompletion
    {
    }

    internal interface IBenchSetupCompletionAsync
    {
    }

    internal interface IBenchLoaded
    {
    }

    internal interface IBenchLoadedAsync
    {
    }

    internal interface IBenchDispose
    {
    }

    internal interface IBenchDisposeAsync
    {
    }

    internal interface IBenchSceneService
    {
    }

    internal interface IBenchEntityComponent
    {
    }

    internal sealed class BenchRootTime
    {
        public readonly int Stamp;

        public BenchRootTime()
        {
            Stamp = 1;
        }
    }

    internal sealed class BenchRootLog : IBenchBaseSetup
    {
        public readonly BenchRootTime Time;

        public BenchRootLog(BenchRootTime time)
        {
            Time = time;
        }
    }

    internal sealed class BenchRootConfig
    {
        public readonly BenchRootLog Log;

        public BenchRootConfig(BenchRootLog log)
        {
            Log = log;
        }
    }

    internal sealed class BenchRootAssets : IBenchSceneService
    {
        public readonly BenchRootConfig Config;

        public BenchRootAssets(BenchRootConfig config)
        {
            Config = config;
        }
    }

    internal sealed class BenchRootPrefabs
    {
        public readonly BenchRootAssets Assets;

        public BenchRootPrefabs(BenchRootAssets assets)
        {
            Assets = assets;
        }
    }

    internal sealed class BenchRootAudio : IBenchLoaded
    {
        public readonly BenchRootPrefabs Prefabs;
        public readonly BenchRootLog Log;

        public BenchRootAudio(BenchRootPrefabs prefabs, BenchRootLog log)
        {
            Prefabs = prefabs;
            Log = log;
        }
    }

    internal sealed class BenchRootInput
    {
        public readonly BenchRootTime Time;

        public BenchRootInput(BenchRootTime time)
        {
            Time = time;
        }
    }

    internal sealed class BenchRootSaves : IBenchDispose
    {
        public readonly BenchRootConfig Config;

        public BenchRootSaves(BenchRootConfig config)
        {
            Config = config;
        }
    }

    internal sealed class BenchRootPublisher : IBenchSetup
    {
        public readonly BenchRootLog Log;

        public BenchRootPublisher(BenchRootLog log)
        {
            Log = log;
        }
    }

    internal sealed class BenchRootUpdater : IBenchSetup, IBenchDispose
    {
        public readonly BenchRootTime Time;
        public readonly BenchRootLog Log;

        public BenchRootUpdater(BenchRootTime time, BenchRootLog log)
        {
            Time = time;
            Log = log;
        }
    }

    internal sealed class BenchRootNetwork
    {
        public readonly BenchRootConfig Config;
        public readonly BenchRootLog Log;

        public BenchRootNetwork(BenchRootConfig config, BenchRootLog log)
        {
            Config = config;
            Log = log;
        }
    }

    internal sealed class BenchRootProfiler : IBenchBaseSetup
    {
        public readonly BenchRootTime Time;

        public BenchRootProfiler(BenchRootTime time)
        {
            Time = time;
        }
    }

    internal sealed class BenchMatchRandom
    {
        public readonly BenchRootConfig Config;

        public BenchMatchRandom(BenchRootConfig config)
        {
            Config = config;
        }
    }

    internal sealed class BenchMatchEvents : IBenchSetup
    {
        public readonly BenchRootPublisher Publisher;

        public BenchMatchEvents(BenchRootPublisher publisher)
        {
            Publisher = publisher;
        }
    }

    internal sealed class BenchMatchClock : IBenchBaseSetup
    {
        public readonly BenchRootTime Time;

        public BenchMatchClock(BenchRootTime time)
        {
            Time = time;
        }
    }

    internal sealed class BenchMatchState : IBenchSetup
    {
        public readonly BenchRootConfig Config;
        public readonly BenchRootLog Log;

        public BenchMatchState(BenchRootConfig config, BenchRootLog log)
        {
            Config = config;
            Log = log;
        }
    }

    internal sealed class BenchMatchBoard : IBenchSetup, IBenchSceneService
    {
        public readonly BenchMatchState State;
        public readonly BenchRootPrefabs Prefabs;

        public BenchMatchBoard(BenchMatchState state, BenchRootPrefabs prefabs)
        {
            State = state;
            Prefabs = prefabs;
        }
    }

    internal sealed class BenchMatchRules
    {
        public readonly BenchMatchState State;
        public readonly BenchRootConfig Config;

        public BenchMatchRules(BenchMatchState state, BenchRootConfig config)
        {
            State = state;
            Config = config;
        }
    }

    internal sealed class BenchMatchPlayers : IBenchLoaded
    {
        public readonly BenchMatchState State;
        public readonly BenchRootNetwork Network;

        public BenchMatchPlayers(BenchMatchState state, BenchRootNetwork network)
        {
            State = state;
            Network = network;
        }
    }

    internal sealed class BenchMatchCards : IBenchSetup
    {
        public readonly BenchMatchPlayers Players;
        public readonly BenchMatchBoard Board;
        public readonly BenchRootPrefabs Prefabs;

        public BenchMatchCards(
            BenchMatchPlayers players,
            BenchMatchBoard board,
            BenchRootPrefabs prefabs)
        {
            Players = players;
            Board = board;
            Prefabs = prefabs;
        }
    }

    internal sealed class BenchMatchScore
    {
        public readonly BenchMatchState State;
        public readonly BenchMatchRules Rules;

        public BenchMatchScore(BenchMatchState state, BenchMatchRules rules)
        {
            State = state;
            Rules = rules;
        }
    }

    internal sealed class BenchMatchSync : IBenchSetupAsync
    {
        public readonly BenchMatchState State;
        public readonly BenchRootNetwork Network;

        public BenchMatchSync(BenchMatchState state, BenchRootNetwork network)
        {
            State = state;
            Network = network;
        }
    }

    internal sealed class BenchMatchLoop : IBenchSetup, IBenchLoaded
    {
        public readonly BenchMatchState State;
        public readonly BenchMatchBoard Board;
        public readonly BenchRootUpdater Updater;

        public BenchMatchLoop(
            BenchMatchState state,
            BenchMatchBoard board,
            BenchRootUpdater updater)
        {
            State = state;
            Board = board;
            Updater = updater;
        }
    }

    internal sealed class BenchMatchCamera : IBenchSceneService
    {
        public readonly BenchMatchBoard Board;
        public readonly BenchRootPrefabs Prefabs;

        public BenchMatchCamera(BenchMatchBoard board, BenchRootPrefabs prefabs)
        {
            Board = board;
            Prefabs = prefabs;
        }
    }

    internal sealed class BenchCardId : IBenchEntityComponent
    {
        public readonly BenchMatchState State;

        public BenchCardId(BenchMatchState state)
        {
            State = state;
        }
    }

    internal sealed class BenchCardDefinition
    {
        public readonly BenchMatchCards Cards;
        public readonly BenchRootConfig Config;

        public BenchCardDefinition(BenchMatchCards cards, BenchRootConfig config)
        {
            Cards = cards;
            Config = config;
        }
    }

    internal sealed class BenchCardOwner : IBenchEntityComponent
    {
        public readonly BenchMatchPlayers Players;

        public BenchCardOwner(BenchMatchPlayers players)
        {
            Players = players;
        }
    }

    internal sealed class BenchCardView : IBenchEntityComponent, IBenchSetup
    {
        public readonly BenchCardDefinition Definition;
        public readonly BenchRootPrefabs Prefabs;
        public readonly BenchMatchBoard Board;

        public BenchCardView(
            BenchCardDefinition definition,
            BenchRootPrefabs prefabs,
            BenchMatchBoard board)
        {
            Definition = definition;
            Prefabs = prefabs;
            Board = board;
        }
    }

    internal sealed class BenchCardState
    {
        public readonly BenchCardDefinition Definition;
        public readonly BenchMatchState MatchState;

        public BenchCardState(BenchCardDefinition definition, BenchMatchState matchState)
        {
            Definition = definition;
            MatchState = matchState;
        }
    }

    internal sealed class BenchCardAction : IBenchSetup
    {
        public readonly BenchCardDefinition Definition;
        public readonly BenchMatchRules Rules;
        public readonly BenchMatchScore Score;

        public BenchCardAction(
            BenchCardDefinition definition,
            BenchMatchRules rules,
            BenchMatchScore score)
        {
            Definition = definition;
            Rules = rules;
            Score = score;
        }
    }

    internal sealed class BenchCardAnimator : IBenchEntityComponent
    {
        public readonly BenchRootTime Time;
        public readonly BenchCardDefinition Definition;

        public BenchCardAnimator(BenchRootTime time, BenchCardDefinition definition)
        {
            Time = time;
            Definition = definition;
        }
    }

    internal sealed class BenchCardInput : IBenchSetup
    {
        public readonly BenchRootInput Input;
        public readonly BenchMatchLoop Loop;
        public readonly BenchCardDefinition Definition;

        public BenchCardInput(
            BenchRootInput input,
            BenchMatchLoop loop,
            BenchCardDefinition definition)
        {
            Input = input;
            Loop = loop;
            Definition = definition;
        }
    }
}
