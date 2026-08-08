namespace Soviet.Vetrov9.Simulation;

public interface ISimClock
{
    public DateTime Now { get; }
    public void AdvanceTo(DateTime t);
}
