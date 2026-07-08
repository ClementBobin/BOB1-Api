namespace Application.Services;

using Interfaces;

using Domain.Dto;
using Domain.Entities;

using Infrastructure.Interfaces;

using NLog;

public class PenaltyService : IPenaltyService
{
    private readonly IPenaltyRepository _penalties;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public PenaltyService(IPenaltyRepository penalties) => _penalties = penalties;

    public async Task<IEnumerable<PenaltyDto>> GetByUserAsync(Guid userId)
    {
        Log.Debug("GetByUserAsync {UserId}", userId);
        return (await _penalties.GetByUserAsync(userId)).Select(ToDto);
    }

    public async Task<IEnumerable<PenaltyDto>> GetAllAsync()
    {
        Log.Debug("GetAllAsync");
        return (await _penalties.GetAllAsync()).Select(ToDto);
    }

    public async Task<PenaltyDto> CreateAsync(CreatePenaltyRequest request)
    {
        Log.Info("CreateAsync user={UserId} points={Points}", request.UserId, request.Points);

        var penalty = new Penalty
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            MatchId = request.MatchId,
            Reason = request.Reason,
            Points = request.Points,
            CreatedAt = DateTime.UtcNow,
        };

        await _penalties.AddAsync(penalty);
        return ToDto(penalty);
    }

    private static PenaltyDto ToDto(Penalty p) =>
        new(p.Id, p.Reason, p.Points, p.CreatedAt, p.UserId, p.MatchId);
}
