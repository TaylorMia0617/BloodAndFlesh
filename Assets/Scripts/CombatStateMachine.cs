public sealed class CombatStateMachine
{
    public CombatPhase Phase { get; private set; } = CombatPhase.Ready;
    public bool BlocksMovement { get; private set; }
    public bool IsReady => Phase == CombatPhase.Ready;

    public void Enter(CombatPhase phase, bool blocksMovement)
    {
        Phase = phase;
        BlocksMovement = blocksMovement;
    }

    public void SetPhase(CombatPhase phase)
    {
        Phase = phase;
    }

    public void Clear()
    {
        Phase = CombatPhase.Ready;
        BlocksMovement = false;
    }

    public void Interrupt()
    {
        Phase = CombatPhase.Interrupted;
        BlocksMovement = false;
    }
}
