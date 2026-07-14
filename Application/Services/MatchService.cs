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

    public async Task<IEnumerable<MatchDto>> GetAllAsync()
    {
        var matches = await _matches.GetAllAsync();
        return matches.Select(m => ToDto(m, null));
    }

    public async Task<IEnumerable<MatchDto>> GetByMonthAsync(int year, int month)
    {
        var matches = await _matches.GetByMonthAsync(year, month);
        return matches.Select(m => ToDto(m, null));
    }

    public async Task<IEnumerable<MatchDto>> GetByDivisionAsync(Guid divisionId)
    {
        var matches = await _matches.GetByDivisionAsync(divisionId);
        return matches.Select(m => ToDto(m, null));
    }

    public async Task<MatchDto> GetByIdAsync(Guid id)
    {
        var match = await _matches.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Match {id} not found.");
        return ToDto(match, null);
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
        return ToDto(created, null);
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

        var updated = await _matches.GetByIdAsync(matchId)!;
        return ToDto(updated!, sub.Status);
    }

    public async Task<MatchDto> UnsubscribeAsync(Guid matchId, Guid userId)
    {
        _log.Info("User {UserId} unsubscribing from match {MatchId}", userId, matchId);

        var sub = await _subscriptions.GetAsync(userId, matchId)
            ?? throw new InvalidOperationException("No subscription found.");

        await _subscriptions.DeleteAsync(sub.Id);

        var match = await _matches.GetByIdAsync(matchId)!;
        return ToDto(match!, null);
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
            null,
            null,
            m.Location.GeocodedAt != null,
            m.Location.GeocodedAt),
        m.Slots.Select(s => new RoleSlotDto(
            s.Role,
            s.AssignedUser is null ? null
                : new UserDto(s.AssignedUser.Id, s.AssignedUser.Email,
                    s.AssignedUser.FirstName, s.AssignedUser.LastName, s.AssignedUser.Role))),
        currentUserStatus);
}
