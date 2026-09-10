namespace Internal.Tests
{
    // Скоуп сущности на 50 регистраций для замера массового создания (GeneratedContainer_ManyScopes).
    // 40 Singleton + 10 Transient; первые 10 берут сервисы родителя (Match/Root), остальные — соседей
    // по скоупу. В основной граф BenchmarkGraph не входит.
    internal static class BenchmarkLargeRoots
    {
        [ContainerGraphRoot]
        [ContainerScopeParent(typeof(BenchmarkRoots), nameof(BenchmarkRoots.Match))]
        public static void Card50(IBuilder builder)
        {
            builder.Register<BenchLarge01>();
            builder.Register<BenchLarge02>();
            builder.Register<BenchLarge03>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge04>().As<IBenchSetup>();
            builder.Register<BenchLarge05>();
            builder.Register<BenchLarge06>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge07>().As<IBenchLoaded>();
            builder.Register<BenchLarge08>().As<IBenchSetup>();
            builder.Register<BenchLarge09>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge10>();
            builder.Register<BenchLarge11>();
            builder.Register<BenchLarge12>().As<IBenchEntityComponent>().As<IBenchSetup>();
            builder.Register<BenchLarge13>();
            builder.Register<BenchLarge14>().As<IBenchLoaded>();
            builder.Register<BenchLarge15>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge16>().As<IBenchSetup>();
            builder.Register<BenchLarge17>();
            builder.Register<BenchLarge18>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge19>();
            builder.Register<BenchLarge20>().As<IBenchSetup>();
            builder.Register<BenchLarge21>().As<IBenchEntityComponent>().As<IBenchLoaded>();
            builder.Register<BenchLarge22>();
            builder.Register<BenchLarge23>();
            builder.Register<BenchLarge24>().As<IBenchEntityComponent>().As<IBenchSetup>();
            builder.Register<BenchLarge25>();
            builder.Register<BenchLarge26>();
            builder.Register<BenchLarge27>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge28>().As<IBenchSetup>().As<IBenchLoaded>();
            builder.Register<BenchLarge29>();
            builder.Register<BenchLarge30>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge31>();
            builder.Register<BenchLarge32>().As<IBenchSetup>();
            builder.Register<BenchLarge33>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge34>();
            builder.Register<BenchLarge35>().As<IBenchLoaded>();
            builder.Register<BenchLarge36>().As<IBenchEntityComponent>().As<IBenchSetup>();
            builder.Register<BenchLarge37>();
            builder.Register<BenchLarge38>();
            builder.Register<BenchLarge39>().As<IBenchEntityComponent>();
            builder.Register<BenchLarge40>().As<IBenchSetup>();
            builder.Register<BenchLarge41>(ServiceLifetime.Transient);
            builder.Register<BenchLarge42>(ServiceLifetime.Transient).As<IBenchEntityComponent>().As<IBenchLoaded>();
            builder.Register<BenchLarge43>(ServiceLifetime.Transient);
            builder.Register<BenchLarge44>(ServiceLifetime.Transient).As<IBenchSetup>();
            builder.Register<BenchLarge45>(ServiceLifetime.Transient).As<IBenchEntityComponent>();
            builder.Register<BenchLarge46>(ServiceLifetime.Transient);
            builder.Register<BenchLarge47>(ServiceLifetime.Transient);
            builder.Register<BenchLarge48>(ServiceLifetime.Transient).As<IBenchEntityComponent>().As<IBenchSetup>();
            builder.Register<BenchLarge49>(ServiceLifetime.Transient).As<IBenchLoaded>();
            builder.Register<BenchLarge50>(ServiceLifetime.Transient);
        }
    }

    internal sealed class BenchLarge01
    {
        public readonly BenchMatchState Parent;

        public BenchLarge01(BenchMatchState parent)
        {
            Parent = parent;
        }
    }

    internal sealed class BenchLarge02
    {
        public readonly BenchRootConfig Parent;
        public readonly BenchLarge01 Previous;

        public BenchLarge02(BenchRootConfig parent, BenchLarge01 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }

    internal sealed class BenchLarge03 : IBenchEntityComponent
    {
        public readonly BenchMatchBoard Parent;
        public readonly BenchLarge02 Previous;

        public BenchLarge03(BenchMatchBoard parent, BenchLarge02 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }

    internal sealed class BenchLarge04 : IBenchSetup
    {
        public readonly BenchMatchPlayers Parent;
        public readonly BenchLarge03 Previous;

        public BenchLarge04(BenchMatchPlayers parent, BenchLarge03 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }

    internal sealed class BenchLarge05
    {
        public readonly BenchRootTime Parent;
        public readonly BenchLarge04 Previous;

        public BenchLarge05(BenchRootTime parent, BenchLarge04 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }

    internal sealed class BenchLarge06 : IBenchEntityComponent
    {
        public readonly BenchMatchRules Parent;
        public readonly BenchLarge05 Previous;

        public BenchLarge06(BenchMatchRules parent, BenchLarge05 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }

    internal sealed class BenchLarge07 : IBenchLoaded
    {
        public readonly BenchRootPrefabs Parent;
        public readonly BenchLarge06 Previous;

        public BenchLarge07(BenchRootPrefabs parent, BenchLarge06 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }

    internal sealed class BenchLarge08 : IBenchSetup
    {
        public readonly BenchMatchCards Parent;
        public readonly BenchLarge07 Previous;

        public BenchLarge08(BenchMatchCards parent, BenchLarge07 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }

    internal sealed class BenchLarge09 : IBenchEntityComponent
    {
        public readonly BenchRootLog Parent;
        public readonly BenchLarge08 Previous;

        public BenchLarge09(BenchRootLog parent, BenchLarge08 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }

    internal sealed class BenchLarge10
    {
        public readonly BenchMatchScore Parent;
        public readonly BenchLarge09 Previous;

        public BenchLarge10(BenchMatchScore parent, BenchLarge09 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }

    internal sealed class BenchLarge11
    {
        public readonly BenchLarge10 Previous;
        public readonly BenchLarge01 Far;

        public BenchLarge11(BenchLarge10 previous, BenchLarge01 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge12 : IBenchEntityComponent, IBenchSetup
    {
        public readonly BenchLarge11 Previous;
        public readonly BenchLarge02 Far;

        public BenchLarge12(BenchLarge11 previous, BenchLarge02 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge13
    {
        public readonly BenchLarge12 Previous;
        public readonly BenchLarge03 Far;

        public BenchLarge13(BenchLarge12 previous, BenchLarge03 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge14 : IBenchLoaded
    {
        public readonly BenchLarge13 Previous;
        public readonly BenchLarge04 Far;

        public BenchLarge14(BenchLarge13 previous, BenchLarge04 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge15 : IBenchEntityComponent
    {
        public readonly BenchLarge14 Previous;
        public readonly BenchLarge05 Far;

        public BenchLarge15(BenchLarge14 previous, BenchLarge05 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge16 : IBenchSetup
    {
        public readonly BenchLarge15 Previous;
        public readonly BenchLarge06 Far;

        public BenchLarge16(BenchLarge15 previous, BenchLarge06 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge17
    {
        public readonly BenchLarge16 Previous;
        public readonly BenchLarge07 Far;

        public BenchLarge17(BenchLarge16 previous, BenchLarge07 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge18 : IBenchEntityComponent
    {
        public readonly BenchLarge17 Previous;
        public readonly BenchLarge08 Far;

        public BenchLarge18(BenchLarge17 previous, BenchLarge08 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge19
    {
        public readonly BenchLarge18 Previous;
        public readonly BenchLarge09 Far;

        public BenchLarge19(BenchLarge18 previous, BenchLarge09 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge20 : IBenchSetup
    {
        public readonly BenchLarge19 Previous;
        public readonly BenchLarge10 Far;

        public BenchLarge20(BenchLarge19 previous, BenchLarge10 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge21 : IBenchEntityComponent, IBenchLoaded
    {
        public readonly BenchLarge20 Previous;
        public readonly BenchLarge11 Far;

        public BenchLarge21(BenchLarge20 previous, BenchLarge11 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge22
    {
        public readonly BenchLarge21 Previous;
        public readonly BenchLarge12 Far;

        public BenchLarge22(BenchLarge21 previous, BenchLarge12 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge23
    {
        public readonly BenchLarge22 Previous;
        public readonly BenchLarge13 Far;

        public BenchLarge23(BenchLarge22 previous, BenchLarge13 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge24 : IBenchEntityComponent, IBenchSetup
    {
        public readonly BenchLarge23 Previous;
        public readonly BenchLarge14 Far;

        public BenchLarge24(BenchLarge23 previous, BenchLarge14 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge25
    {
        public readonly BenchLarge24 Previous;
        public readonly BenchLarge15 Far;

        public BenchLarge25(BenchLarge24 previous, BenchLarge15 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge26
    {
        public readonly BenchLarge25 Previous;
        public readonly BenchLarge16 Far;

        public BenchLarge26(BenchLarge25 previous, BenchLarge16 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge27 : IBenchEntityComponent
    {
        public readonly BenchLarge26 Previous;
        public readonly BenchLarge17 Far;

        public BenchLarge27(BenchLarge26 previous, BenchLarge17 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge28 : IBenchSetup, IBenchLoaded
    {
        public readonly BenchLarge27 Previous;
        public readonly BenchLarge18 Far;

        public BenchLarge28(BenchLarge27 previous, BenchLarge18 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge29
    {
        public readonly BenchLarge28 Previous;
        public readonly BenchLarge19 Far;

        public BenchLarge29(BenchLarge28 previous, BenchLarge19 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge30 : IBenchEntityComponent
    {
        public readonly BenchLarge29 Previous;
        public readonly BenchLarge20 Far;

        public BenchLarge30(BenchLarge29 previous, BenchLarge20 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge31
    {
        public readonly BenchLarge30 Previous;
        public readonly BenchLarge21 Far;

        public BenchLarge31(BenchLarge30 previous, BenchLarge21 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge32 : IBenchSetup
    {
        public readonly BenchLarge31 Previous;
        public readonly BenchLarge22 Far;

        public BenchLarge32(BenchLarge31 previous, BenchLarge22 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge33 : IBenchEntityComponent
    {
        public readonly BenchLarge32 Previous;
        public readonly BenchLarge23 Far;

        public BenchLarge33(BenchLarge32 previous, BenchLarge23 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge34
    {
        public readonly BenchLarge33 Previous;
        public readonly BenchLarge24 Far;

        public BenchLarge34(BenchLarge33 previous, BenchLarge24 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge35 : IBenchLoaded
    {
        public readonly BenchLarge34 Previous;
        public readonly BenchLarge25 Far;

        public BenchLarge35(BenchLarge34 previous, BenchLarge25 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge36 : IBenchEntityComponent, IBenchSetup
    {
        public readonly BenchLarge35 Previous;
        public readonly BenchLarge26 Far;

        public BenchLarge36(BenchLarge35 previous, BenchLarge26 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge37
    {
        public readonly BenchLarge36 Previous;
        public readonly BenchLarge27 Far;

        public BenchLarge37(BenchLarge36 previous, BenchLarge27 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge38
    {
        public readonly BenchLarge37 Previous;
        public readonly BenchLarge28 Far;

        public BenchLarge38(BenchLarge37 previous, BenchLarge28 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge39 : IBenchEntityComponent
    {
        public readonly BenchLarge38 Previous;
        public readonly BenchLarge29 Far;

        public BenchLarge39(BenchLarge38 previous, BenchLarge29 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge40 : IBenchSetup
    {
        public readonly BenchLarge39 Previous;
        public readonly BenchLarge30 Far;

        public BenchLarge40(BenchLarge39 previous, BenchLarge30 far)
        {
            Previous = previous;
            Far = far;
        }
    }

    internal sealed class BenchLarge41
    {
        public readonly BenchLarge31 Near;
        public readonly BenchLarge21 Far;

        public BenchLarge41(BenchLarge31 near, BenchLarge21 far)
        {
            Near = near;
            Far = far;
        }
    }

    internal sealed class BenchLarge42 : IBenchEntityComponent, IBenchLoaded
    {
        public readonly BenchLarge32 Near;
        public readonly BenchLarge22 Far;

        public BenchLarge42(BenchLarge32 near, BenchLarge22 far)
        {
            Near = near;
            Far = far;
        }
    }

    internal sealed class BenchLarge43
    {
        public readonly BenchLarge33 Near;
        public readonly BenchLarge23 Far;

        public BenchLarge43(BenchLarge33 near, BenchLarge23 far)
        {
            Near = near;
            Far = far;
        }
    }

    internal sealed class BenchLarge44 : IBenchSetup
    {
        public readonly BenchLarge34 Near;
        public readonly BenchLarge24 Far;

        public BenchLarge44(BenchLarge34 near, BenchLarge24 far)
        {
            Near = near;
            Far = far;
        }
    }

    internal sealed class BenchLarge45 : IBenchEntityComponent
    {
        public readonly BenchLarge35 Near;
        public readonly BenchLarge25 Far;

        public BenchLarge45(BenchLarge35 near, BenchLarge25 far)
        {
            Near = near;
            Far = far;
        }
    }

    internal sealed class BenchLarge46
    {
        public readonly BenchLarge36 Near;
        public readonly BenchLarge26 Far;

        public BenchLarge46(BenchLarge36 near, BenchLarge26 far)
        {
            Near = near;
            Far = far;
        }
    }

    internal sealed class BenchLarge47
    {
        public readonly BenchLarge37 Near;
        public readonly BenchLarge27 Far;

        public BenchLarge47(BenchLarge37 near, BenchLarge27 far)
        {
            Near = near;
            Far = far;
        }
    }

    internal sealed class BenchLarge48 : IBenchEntityComponent, IBenchSetup
    {
        public readonly BenchLarge38 Near;
        public readonly BenchLarge28 Far;

        public BenchLarge48(BenchLarge38 near, BenchLarge28 far)
        {
            Near = near;
            Far = far;
        }
    }

    internal sealed class BenchLarge49 : IBenchLoaded
    {
        public readonly BenchLarge39 Near;
        public readonly BenchLarge29 Far;

        public BenchLarge49(BenchLarge39 near, BenchLarge29 far)
        {
            Near = near;
            Far = far;
        }
    }

    internal sealed class BenchLarge50
    {
        public readonly BenchLarge40 Near;
        public readonly BenchLarge30 Far;

        public BenchLarge50(BenchLarge40 near, BenchLarge30 far)
        {
            Near = near;
            Far = far;
        }
    }
}
