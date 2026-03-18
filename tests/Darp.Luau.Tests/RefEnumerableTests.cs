using Darp.Luau.Internal;
using Shouldly;

namespace Darp.Luau.Tests;

public sealed class RefEnumerableTests
{
    [Fact]
    public void Default_ShouldBeEmpty()
    {
        RefEnumerable<int> values = default;

        values.Length.ShouldBe(0);
        RefEnumerable<int>.MaxLength.ShouldBe(4);
    }

    [Fact]
    public void Add_ShouldStoreItemsInOrder()
    {
        RefEnumerable<int> values = default;

        values.Add(10);
        values.Add(20);
        values.Add(30);
        values.Add(40);

        values.Length.ShouldBe(4);
        values[0].ShouldBe(10);
        values[1].ShouldBe(20);
        values[2].ShouldBe(30);
        values[3].ShouldBe(40);
    }

    [Fact]
    public void Indexer_WithNegativeIndex_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(static () => ReadValueAt(CreateValues(1, 2), -1));
    }

    [Fact]
    public void Indexer_WithIndexAtLength_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(static () => ReadValueAt(CreateValues(1, 2), 2));
    }

    [Fact]
    public void Add_WhenFull_ShouldThrow()
    {
        Should.Throw<InvalidOperationException>(static () => AddOnePastCapacity());
    }

    [Fact]
    public void ParamsCapture_ShouldBuildExpectedSequence()
    {
        RefEnumerable<int> empty = Capture();
        empty.Length.ShouldBe(0);

        RefEnumerable<int> values = Capture(1, 2, 3, 4);
        values.Length.ShouldBe(4);
        values[0].ShouldBe(1);
        values[1].ShouldBe(2);
        values[2].ShouldBe(3);
        values[3].ShouldBe(4);
    }

    [Fact]
    public void ParamsCapture_WhenTooManyItems_ShouldThrow()
    {
        Should.Throw<InvalidOperationException>(static () => CaptureFive());
    }

    [Fact]
    public void Enumerator_ShouldYieldValuesInOrder()
    {
        RefEnumerable<int> values = CreateValues(3, 5, 8);
        var enumerator = new RefEnumerable<int>.Enumerator(values);

        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe(3);

        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe(5);

        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe(8);

        enumerator.MoveNext().ShouldBeFalse();
    }

    [Fact]
    public void Enumerator_CurrentBeforeMoveNext_ShouldThrow()
    {
        Should.Throw<InvalidOperationException>(static () => ReadCurrentBeforeMoveNext());
    }

    private static RefEnumerable<int> Capture(params RefEnumerable<int> values)
    {
        RefEnumerable<int> copied = default;
        for (int i = 0; i < values.Length; i++)
            copied.Add(values[i]);
        return copied;
    }

    private static RefEnumerable<int> CreateValues(params int[] source)
    {
        RefEnumerable<int> values = default;
        foreach (int item in source)
            values.Add(item);
        return values;
    }

    private static int ReadValueAt(RefEnumerable<int> values, int index) => values[index];

    private static void AddOnePastCapacity()
    {
        RefEnumerable<int> values = CreateValues(1, 2, 3, 4);
        values.Add(5);
    }

    private static void CaptureFive() => Capture(1, 2, 3, 4, 5);

    private static int ReadCurrentBeforeMoveNext()
    {
        var enumerator = new RefEnumerable<int>.Enumerator(CreateValues(1));
        return enumerator.Current;
    }
}
