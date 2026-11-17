using FluentAssertions;
using Internal;
using Xunit;

namespace Framework.Common.Tests;

public class ModifiableListTests
{
    [Fact]
    public void Add_OutsideIteration_IsVisibleImmediately()
    {
        var list = new ModifiableList<int>();

        list.Add(1);
        list.Add(2);

        Collect(list).Should().Equal(1, 2);
    }

    [Fact]
    public void Add_DuringIteration_AppliesAfterPass()
    {
        var list = new ModifiableList<int>();
        list.Add(1);

        var seen = new List<int>();

        foreach (var item in list)
        {
            seen.Add(item);
            list.Add(item + 10);
        }

        seen.Should().Equal(1);
        Collect(list).Should().Equal(1, 11);
    }

    [Fact]
    public void Remove_DuringIteration_StillVisitedThenRemoved()
    {
        var list = new ModifiableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);

        var seen = new List<int>();

        foreach (var item in list)
        {
            seen.Add(item);
            list.Remove(2);
        }

        seen.Should().Equal(1, 2, 3);
        Collect(list).Should().Equal(1, 3);
    }

    [Fact]
    public void AddThenRemove_DuringIteration_NeverLands()
    {
        var list = new ModifiableList<int>();
        list.Add(1);

        foreach (var _ in list)
        {
            list.Add(5);
            list.Remove(5);
        }

        Collect(list).Should().Equal(1);
    }

    [Fact]
    public void Break_DuringIteration_DoesNotLeaveListIterating()
    {
        var list = new ModifiableList<int>();
        list.Add(1);
        list.Add(2);

        foreach (var _ in list)
            break;

        list.Add(3);

        Collect(list).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Exception_DuringIteration_DoesNotLeaveListIterating()
    {
        var list = new ModifiableList<int>();
        list.Add(1);

        var act = () => {
            foreach (var _ in list)
            {
                list.Add(2);
                throw new InvalidOperationException();
            }
        };

        act.Should().Throw<InvalidOperationException>();

        list.Add(3);

        Collect(list).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void NestedIteration_AppliesPendingAfterOutermostPass()
    {
        var list = new ModifiableList<int>();
        list.Add(1);
        list.Add(2);

        var innerPasses = 0;

        foreach (var _ in list)
        {
            foreach (var __ in list)
                innerPasses++;

            list.Add(3);

            // The inner pass has finished, but the outer one is still running: the element is deferred.
            list.Count.Should().Be(2);
        }

        innerPasses.Should().Be(4);
        Collect(list).Should().Equal(1, 2, 3, 3);
    }

    [Fact]
    public void Clear_DuringIteration_AppliesAfterPassAndDropsPending()
    {
        var list = new ModifiableList<int>();
        list.Add(1);
        list.Add(2);

        var seen = new List<int>();

        foreach (var item in list)
        {
            seen.Add(item);
            list.Add(9);
            list.Clear();
        }

        seen.Should().Equal(1, 2);
        Collect(list).Should().BeEmpty();
    }

    [Fact]
    public void Clear_OutsideIteration_Empties()
    {
        var list = new ModifiableList<int>();
        list.Add(1);

        list.Clear();

        list.Count.Should().Be(0);
        Collect(list).Should().BeEmpty();
    }

    private static List<T> Collect<T>(ModifiableList<T> list)
    {
        var result = new List<T>();

        foreach (var item in list)
            result.Add(item);

        return result;
    }
}
