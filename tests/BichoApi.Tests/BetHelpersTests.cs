using FluentAssertions;

public class BetHelpersTests
{
    [Fact]
    public void Extract_RightHundred() =>
        BetHelpers.Extract("7517", PositionPick.RIGHT, "HUND").Should().Be("517");

    [Fact]
    public void Permutations_ThreeDigits() =>
        BetHelpers.Permutations("123").Should().Contain(new[] {"123","132","213","231","312","321"});
}