namespace GamePlay.Boards
{
    // Поля игроков делят один базовый префаб клетки и различаются только спрайтами,
    // поэтому доска выбирает вариант по своей принадлежности.
    public enum BoardCellKind
    {
        Own = 0,
        Opponent = 1
    }
}
