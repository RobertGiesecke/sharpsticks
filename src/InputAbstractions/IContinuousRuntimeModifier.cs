namespace SharpSticks.InputAbstractions;

/// <summary>
/// Marks a runtime axis modifier whose output keeps changing over wall-clock time even
/// when the input is steady (it integrates toward a target). The runtime gives such a
/// modifier a steady update tick so it converges smoothly during input gaps, instead of
/// only advancing when a device happens to report.
/// </summary>
public interface IContinuousRuntimeModifier;
