using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

public class SuggestionResultTests
{
    private static Suggestion Offering(string title, int minutes) => Suggestion.For(
        new Candidate { ItemId = Guid.NewGuid(), Title = title, Runtime = TimeSpan.FromMinutes(minutes) },
        TimeSpan.FromMinutes(60));

    [Fact]
    public void Offering_suggestions_yields_an_offered_result_holding_them_in_order()
    {
        var first = Offering("Severance S1E3", 43);
        var second = Offering("Arrival", 116);

        var result = SuggestionResult.Offer([first, second]);

        var offered = Assert.IsType<SuggestionResult.Offered>(result);
        Assert.Equal(new[] { first, second }, offered.Suggestions);
    }

    [Fact]
    public void Offering_nothing_yields_spent_rather_than_an_empty_offered_result()
    {
        var result = SuggestionResult.Offer([]);

        Assert.IsType<SuggestionResult.Spent>(result);
    }

    [Fact]
    public void An_offered_result_can_never_be_constructed_empty()
    {
        Assert.Throws<ArgumentException>(() => new SuggestionResult.Offered([]));
    }

    [Fact]
    public void Spent_is_a_result_in_its_own_right_and_carries_no_suggestions()
    {
        SuggestionResult result = SuggestionResult.Spent.Instance;

        var suggestions = result switch
        {
            SuggestionResult.Offered offered => offered.Suggestions,
            SuggestionResult.Spent => [],
            _ => throw new InvalidOperationException("unreachable")
        };

        Assert.Empty(suggestions);
    }
}
