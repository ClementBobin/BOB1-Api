namespace Application.Services;

using Application.Interfaces;

using Domain.Dto;
using Domain.Entities;
using Domain.Enums;

using Infrastructure.Interfaces;

using NLog;

public class MatchService : IMatchService
{
    private readonly IMatchRepository _matches;
    private readonly ISubscriptionRepository _subscriptions;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public MatchService(IMatchRepository matches, ISubscriptionRepository subscriptions)
    {
        _matches = matches;
        _subscriptions = subscriptions;
    }
    // Add methods that include user context
    public async Task<IEnumerable<MatchDto>> GetAllAsync(Guid userId)
    {
        var matches = await _matches.GetAllAsync();
        return await MapWithUserStatusAsync(matches, userId);
    }

    public async Task<IEnumerable<MatchDto>> GetByMonthAsync(int year, int month, Guid userId)
    {
        var matches = await _matches.GetByMonthAsync(year, month);
        return await MapWithUserStatusAsync(matches, userId);
    }

    public async Task<IEnumerable<MatchDto>> GetByDivisionAsync(Guid divisionId, Guid userId)
    {
        var matches = await _matches.GetByDivisionAsync(divisionId);
        return await MapWithUserStatusAsync(matches, userId);
    }

    public async Task<MatchDto> GetByIdAsync(Guid id, Guid userId)
    {
        var match = await _matches.GetByIdAsync(id)
                    ?? throw new KeyNotFoundException($"Match {id} not found.");

        var status = await GetUserStatusAsync(userId, id);
        return ToDto(match, status);
    }

    public async Task<MatchDto> CreateAsync(CreateMatchRequest request)
    {
        _log.Info("Creating match on {Date} division={DivisionId}", request.DateUtc, request.DivisionId);

        var match = new Match
        {
            Id = Guid.NewGuid(),
            DateUtc = request.DateUtc,
            EmergencyDateUtc = request.EmergencyDateUtc,
            EmergencyPoints = request.EmergencyPoints,
            DivisionId = request.DivisionId,
            HomeTeamId = request.HomeTeamId,
            AwayTeamId = request.AwayTeamId,
            LocationId = request.LocationId,
            Slots = Enum.GetValues<OfficialRole>().Select(role => new RoleSlot
            {
                Role = role,
                MatchId = Guid.Empty, // EF sets this on save
            }).ToList(),
        };

        await _matches.AddAsync(match);

        var created = await _matches.GetByIdAsync(match.Id)
            ?? throw new InvalidOperationException("Match not found after creation.");
        return ToDto(created, MatchSubscriptionStatus.Neutral);
    }

    public async Task DeleteAsync(Guid id)
    {
        _log.Info("Deleting match {Id}", id);
        await _matches.DeleteAsync(id);
    }

    public async Task<MatchDto> SubscribeAsync(Guid matchId, Guid userId, SubscribeRequest request)
    {
        _log.Info("User {UserId} subscribing to match {MatchId} as {Role}", userId, matchId, request.Role);

        var match = await _matches.GetByIdAsync(matchId)
            ?? throw new KeyNotFoundException($"Match {matchId} not found.");

        var slot = match.Slots.FirstOrDefault(s => s.Role == request.Role)
            ?? throw new InvalidOperationException($"Role {request.Role} does not exist on this match.");

        if (slot.AssignedUserId is not null)
            throw new InvalidOperationException($"Role {request.Role} is already taken.");

        var existing = await _subscriptions.GetAsync(userId, matchId);
        if (existing is not null)
            throw new InvalidOperationException("Already subscribed to this match.");

        var sub = new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatchId = matchId,
            Role = request.Role,
            Status = MatchSubscriptionStatus.Subscribed,
            CreatedAt = DateTime.UtcNow,
        };

        await _subscriptions.AddAsync(sub);

        // Update the slot so the assigned user is visible to all clients
        slot.AssignedUserId = userId;
        await _matches.UpdateSlotAsync(slot);

        var updated = await _matches.GetByIdAsync(matchId)!;
        return ToDto(updated!, sub.Status);
    }

    public async Task<MatchDto> UnsubscribeAsync(Guid matchId, Guid userId)
    {
        _log.Info("User {UserId} unsubscribing from match {MatchId}", userId, matchId);

        var sub = await _subscriptions.GetAsync(userId, matchId)
            ?? throw new InvalidOperationException("No subscription found.");

        await _subscriptions.DeleteAsync(sub.Id);

        var match = await _matches.GetByIdAsync(matchId)
            ?? throw new KeyNotFoundException($"Match {matchId} not found.");

        // Clear the slot so it appears vacant to all clients
        var slot = match.Slots.FirstOrDefault(s => s.AssignedUserId == userId);
        if (slot is not null)
        {
            slot.AssignedUserId = null;
            await _matches.UpdateSlotAsync(slot);
        }

        return ToDto(match, MatchSubscriptionStatus.Neutral);
    }

    public async Task<MatchDto> ConfirmPresenceAsync(Guid matchId, Guid userId)
    {
        _log.Info("User {UserId} confirming presence on match {MatchId}", userId, matchId);

        var sub = await _subscriptions.GetAsync(userId, matchId)
            ?? throw new InvalidOperationException("No subscription found.");

        var match = await _matches.GetByIdAsync(matchId)
            ?? throw new KeyNotFoundException($"Match {matchId} not found.");

        var newStatus = DateTime.UtcNow >= match.DateUtc.AddDays(-4)
            ? MatchSubscriptionStatus.ConfirmedJ4
            : MatchSubscriptionStatus.ConfirmedJ15;

        sub.Status = newStatus;
        sub.ConfirmedAt = DateTime.UtcNow;
        await _subscriptions.UpdateAsync(sub);

        return ToDto(match, newStatus);
    }

    // Helper method to get user status for a single match
    private async Task<MatchSubscriptionStatus?> GetUserStatusAsync(Guid userId, Guid matchId)
    {
        var subscription = await _subscriptions.GetAsync(userId, matchId);
        return subscription?.Status;
    }

    // Helper method to get user statuses for multiple matches
    private async Task<Dictionary<Guid, MatchSubscriptionStatus?>> GetUserStatusesAsync(
        Guid userId, IEnumerable<Match> matches)
    {
        var matchIds = matches.Select(m => m.Id);
        var subscriptions = await _subscriptions.GetByUserAndMatchesAsync(userId, matchIds);
        return subscriptions.ToDictionary(
            s => s.MatchId,
            s => (MatchSubscriptionStatus?)s.Status
        );
    }

    // Helper method to map matches with user statuses
    private async Task<IEnumerable<MatchDto>> MapWithUserStatusAsync(
        IEnumerable<Match> matches, Guid userId)
    {
        var statusDict = await GetUserStatusesAsync(userId, matches);
        return matches.Select(m => ToDto(m, statusDict.GetValueOrDefault(m.Id)));
    }


    // ── Mapping ───────────────────────────────────────────────────────────

    private static MatchDto ToDto(Match m, MatchSubscriptionStatus? currentUserStatus) => new(
        m.Id,
        m.DateUtc,
        m.EmergencyDateUtc,
        m.EmergencyPoints,
        new DivisionDto(m.Division.Id, m.Division.Name),
        new TeamDto(m.HomeTeam.Id, m.HomeTeam.Name, m.HomeTeam.LogoUrl,
            new DivisionDto(m.HomeTeam.Division.Id, m.HomeTeam.Division.Name)),
        new TeamDto(m.AwayTeam.Id, m.AwayTeam.Name, m.AwayTeam.LogoUrl,
            new DivisionDto(m.AwayTeam.Division.Id, m.AwayTeam.Division.Name)),
        new LocationDto(
            m.Location.Id,
            m.Location.Name,
            m.Location.Address,
            m.Location.Coordinates?.Latitude,
            m.Location.Coordinates?.Longitude,
            m.Location.IsGeocoded,
            m.Location.GeocodedAt),
        m.Slots.Select(s => new RoleSlotDto(
            s.Role,
            s.AssignedUser is null ? null
                : new UserDto(s.AssignedUser.Id, s.AssignedUser.Email,
                    s.AssignedUser.FirstName, s.AssignedUser.LastName, s.AssignedUser.Role))),
        currentUserStatus);
}