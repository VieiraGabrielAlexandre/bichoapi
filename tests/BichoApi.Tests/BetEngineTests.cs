using FluentAssertions;

public class BetEngineTests
{
    [Fact]
    public void GroupTable_GroupOfDozen()
    {
        var g = new GroupTable();
        g.GroupOfDozen("00").Should().Be(25);
        g.GroupOfDozen("17").Should().Be(5);
    }
}