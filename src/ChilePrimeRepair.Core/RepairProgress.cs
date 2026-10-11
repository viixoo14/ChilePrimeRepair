namespace ChilePrimeRepair.Core;

public enum RepairStage { Checking, Scanning, Backup, Cleaning, Interfaces, Completed }

/// <summary>Fraction measures processed cache entries; other stages have no measurable total.</summary>
public sealed record RepairProgress(RepairStage Stage, double? Fraction = null);
