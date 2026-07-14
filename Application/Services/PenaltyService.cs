namespace Application.Services;

using Application.Interfaces;

using Domain.Dto;
using Domain.Entities;

using Infrastructure.Interfaces;

using NLog;

public class PenaltyService : IPenaltyService
{
    private readonly IPenaltyRepository _penalties;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public PenaltyService(IPenaltyRepository penalties) => _penalties = penalties;

    public async Task<IEnumerable<PenaltyDto>> GetByUserAsync(Guid userId)
    {
        _log.Debug("GetByUserAsync {UserId}", userId);
        return (await _penalties.GetByUserAsync(userId)).Select(ToDto);
    }

    public async Task<IEnumerable<PenaltyDto>> GetAllAsync()
    {
        _log.Debug("GetAllAsync");
        return (await _penalties.GetAllAsync()).Select(ToDto);
    }

    public async Task<PenaltyDto> CreateAsync(CreatePenaltyRequest request)
    {
        _log.Info("CreateAsync user={UserId} points={Points}", request.UserId, request.Points);

        var penalty = new Penalty
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            MatchId = request.MatchId,
            Reason = request.Reason,
            Points = request.Points,
            KickedOut = request.KickedOut,
            CreatedAt = DateTime.UtcNow,
        };

        await _penalties.AddAsync(penalty);
        return ToDto(penalty);
    }

    public async Task AcknowledgeAsync(Guid penaltyId, Guid userId)
    {
        _log.Info("AcknowledgeAsync {Id} user={UserId}", penaltyId, userId);

        var penalty = await _penalties.GetByIdAsync(penaltyId)
            ?? throw new KeyNotFoundException($"Penalty {penaltyId} not found.");

        if (penalty.UserId != userId)
            throw new UnauthorizedAccessException("Cannot acknowledge another user's penalty.");

        if (penalty.AcknowledgedAt is not null)
            throw new InvalidOperationException("Penalty already acknowledged.");

        penalty.AcknowledgedAt = DateTime.UtcNow;
        await _penalties.UpdateAsync(penalty);
    }

    private static PenaltyDto ToDto(Penalty p) =>
        new(p.Id, p.Reason, p.Points, p.KickedOut, p.AcknowledgedAt, p.CreatedAt, p.UserId, p.MatchId);
}
