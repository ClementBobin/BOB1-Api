using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;

namespace Bob1.Modules.Participation.Application;

internal static class TeamRules
{
    /// <summary>
    /// Parents can only take part in matches of a team one of their players belongs to (this season).
    /// Staff and admins are always allowed, in a team or not.
    /// </summary>
    public static async Task<Error?> EnsureAllowedAsync(
        ICurrentUser user, IIdentityApi identity, MatchInfo? match, CancellationToken ct)
    {
        if (match is null) return null;   // the "match not found / closed" rules report it
        if (user.IsInRole(Policies.Staff) || user.IsInRole(Policies.Admin)) return null;
        return await identity.IsInTeamsAsync(user.UserId, user.FamilyId, [match.HomeTeamId, match.AwayTeamId], ct)
            ? null
            : ParticipationErrors.NotInTeam;
    }
}
