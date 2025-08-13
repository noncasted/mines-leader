using Internal;

namespace GamePlay.Boards
{
    public static class BoardsServicesExtensions
    {
        public static IScopeBuilder AddBoardServices(this IScopeBuilder builder)
        {
            builder.Register<CellFlagAction>()
                .As<ICellFlagAction>();

            builder.Register<CellOpenAction>()
                .As<ICellOpenAction>();

            builder.Register<CellsSelection>()
                .As<ICellsSelection>();

            return builder;
        }
    }
}