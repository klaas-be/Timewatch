namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// A single Candidate offered to the user, labelled with how it fits: its Effective
/// Runtime and the Slack it would leave. One item, never a combination.
/// </summary>
public sealed record Suggestion
{
    /// <summary>The item being offered.</summary>
    public required Candidate Candidate { get; init; }

    /// <summary>
    /// Slack: the Remaining Time this was priced against, minus the Candidate's Effective
    /// Runtime. Positive Slack is dead air; negative Slack is overshoot, permitted only
    /// within the Overshoot Tolerance.
    /// </summary>
    public required TimeSpan Slack { get; init; }

    /// <summary>The Candidate's Effective Runtime — what the user is committing to.</summary>
    public TimeSpan EffectiveRuntime => Candidate.EffectiveRuntime;

    /// <summary>
    /// Prices <paramref name="candidate"/> against a Remaining Time, which is the only
    /// way a Suggestion's Slack is ever computed.
    /// </summary>
    public static Suggestion For(Candidate candidate, TimeSpan remainingTime) => new()
    {
        Candidate = candidate,
        Slack = remainingTime - candidate.EffectiveRuntime
    };
}
