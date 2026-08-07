namespace Soviet.Vetrov9.Simulation;

public interface ISimClock
{
    public DateTime now { get; }
    void AdvanceTo(DateTime t);
}
