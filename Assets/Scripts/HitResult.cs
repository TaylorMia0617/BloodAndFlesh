public readonly struct HitResult
{
    public readonly bool accepted;
    public readonly bool dealtDamage;
    public readonly bool killed;
    public readonly bool critical;
    public readonly float finalDamage;
    public readonly FeedbackPayload feedback;

    public HitResult(bool accepted, bool dealtDamage, bool killed, bool critical, float finalDamage, FeedbackPayload feedback)
    {
        this.accepted = accepted;
        this.dealtDamage = dealtDamage;
        this.killed = killed;
        this.critical = critical;
        this.finalDamage = finalDamage;
        this.feedback = feedback;
    }

    public static HitResult Rejected => new HitResult(false, false, false, false, 0f, FeedbackPayload.None);

    public static HitResult AcceptedNoDamage(FeedbackPayload feedback)
    {
        return new HitResult(true, false, false, false, 0f, feedback);
    }
}
