using System.Diagnostics;
using OpenTelemetry;

namespace BankingApp.Telemetry;

/// <summary>
/// Stamps every emitted span with the participant's identity. Guarantees that
/// Langfuse telemetry for a participant-fork can be correlated back to its
/// owner even when the trace leaves the app (gateway, eval, leaderboard).
/// </summary>
public sealed class ParticipantAttributeProcessor : BaseProcessor<Activity>
{
    private readonly FdeOptions _fde;

    public ParticipantAttributeProcessor(FdeOptions fde)
    {
        _fde = fde;
    }

    public override void OnEnd(Activity activity)
    {
        activity.SetTag("fde.participant_id", _fde.ParticipantId);
        activity.SetTag("fde.pod_id", _fde.PodId);
        activity.SetTag("fde.event_id", _fde.EventId);
        activity.SetTag("fde.workload_type", _fde.WorkloadType);
    }
}

/// <summary>Central activity source for the consolidated app.</summary>
public static class BankingActivitySources
{
    public const string Name = "BankingApp";

    public static readonly ActivitySource Source = new(Name);
}