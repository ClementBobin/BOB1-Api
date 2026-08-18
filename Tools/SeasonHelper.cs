namespace Tools;

public static class SeasonHelper
{
    /// <summary>
    /// Returns the season start year for a given date.
    /// A season runs from 01/09/YYYY to 31/08/(YYYY+1).
    /// e.g. 2024-10-01 → 2024, 2025-03-01 → 2024, 2025-09-15 → 2025
    /// </summary>
    public static int GetSeasonId(DateTime date) =>
        date.Month >= 9 ? date.Year : date.Year - 1;

    /// <summary>Current season based on UTC now.</summary>
    public static int CurrentSeasonId => GetSeasonId(DateTime.UtcNow);
}