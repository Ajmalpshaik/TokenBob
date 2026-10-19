using TokenBob.Core.Physics;

namespace TokenBob.Core.Tests.Physics;

public class ResetTimelineTests
{
    [Fact]
    public void Starts_with_the_old_ornament_visible_at_full_size()
    {
        var frame = ResetTimeline.At(0);
        Assert.Equal(ResetPhase.Squeeze, frame.Phase);
        Assert.True(frame.OrnamentVisible);
        Assert.Equal(1, frame.OrnamentScale, 3);
        Assert.Equal(0, frame.Glow, 3);
    }

    [Fact]
    public void Squeezes_to_85_percent_and_full_glow_just_before_the_blast()
    {
        var frame = ResetTimeline.At(ResetTimeline.BlastAt - 0.001);
        Assert.Equal(ResetPhase.Squeeze, frame.Phase);
        Assert.InRange(frame.OrnamentScale, 0.85, 0.87);
        Assert.InRange(frame.Glow, 0.95, 1);
    }

    [Fact]
    public void Ornament_is_hidden_between_the_blast_and_the_arrival()
    {
        var frame = ResetTimeline.At((ResetTimeline.BlastAt + ResetTimeline.ArriveAt) / 2);
        Assert.Equal(ResetPhase.Blast, frame.Phase);
        Assert.False(frame.OrnamentVisible);
    }

    [Fact]
    public void New_ornament_grows_from_nothing_overshoots_and_settles_at_full_size()
    {
        var start = ResetTimeline.At(ResetTimeline.ArriveAt);
        Assert.Equal(ResetPhase.Arrive, start.Phase);
        Assert.True(start.OrnamentVisible);
        Assert.InRange(start.OrnamentScale, 0, 0.05);

        var overshoot = 0.0;
        for (var t = ResetTimeline.ArriveAt; t < ResetTimeline.Duration; t += 0.01)
            overshoot = Math.Max(overshoot, ResetTimeline.At(t).OrnamentScale);
        Assert.InRange(overshoot, 1.08, 1.25);

        var end = ResetTimeline.At(ResetTimeline.Duration);
        Assert.Equal(ResetPhase.Done, end.Phase);
        Assert.Equal(1, end.OrnamentScale, 3);
        Assert.Equal(0, end.Glow, 3);
    }

    [Fact]
    public void Scale_never_goes_negative()
    {
        for (var t = 0.0; t <= ResetTimeline.Duration + 1; t += 0.005)
            Assert.True(ResetTimeline.At(t).OrnamentScale >= 0, $"negative scale at {t}");
    }

    [Fact]
    public void Blast_and_arrival_each_happen_exactly_once_when_stepped()
    {
        var timeline = new ResetTimeline();
        int blasts = 0, arrivals = 0;
        for (var t = 0.0; t < ResetTimeline.Duration + 0.5; t += 1 / 60.0)
        {
            var step = timeline.Advance(1 / 60.0);
            if (step.BlastNow) blasts++;
            if (step.ArriveNow) arrivals++;
        }
        Assert.Equal(1, blasts);
        Assert.Equal(1, arrivals);
        Assert.True(timeline.IsDone);
    }

    [Fact]
    public void A_huge_frame_gap_still_fires_blast_and_arrival_once()
    {
        var timeline = new ResetTimeline();
        var step = timeline.Advance(10);
        Assert.True(step.BlastNow);
        Assert.True(step.ArriveNow);
        Assert.True(timeline.IsDone);
        Assert.Equal(1, step.Frame.OrnamentScale, 3);
    }
}
