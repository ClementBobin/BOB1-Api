namespace Application.Services;

using Interfaces;

using Domain.Dto;

using Infrastructure.Interfaces;

using NLog;

public class PointRuleService : IPointRuleService
{
    private readonly IPointRuleRepository _rules;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public PointRuleService(IPointRuleRepository rules) => _rules = rules;

    public async Task<IEnumerable<PointRuleDto>> GetAllAsync()
    {
        Log.Debug("GetAllAsync");
        return (await _rules.GetAllAsync())
            .Select(r => new PointRuleDto(r.Id, r.Role, r.PointsOnJ15, r.PointsOnJ4, r.PointsEmergency));
    }

    public async Task<PointRuleDto> UpdateAsync(Guid id, UpdatePointRuleRequest request)
    {
        Log.Info("UpdateAsync {Id}", id);

        var rule = await _rules.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"PointRule {id} not found.");

        rule.PointsOnJ15 = request.PointsOnJ15;
        rule.PointsOnJ4 = request.PointsOnJ4;
        rule.PointsEmergency = request.PointsEmergency;

        await _rules.UpdateAsync(rule);
        return new PointRuleDto(rule.Id, rule.Role, rule.PointsOnJ15, rule.PointsOnJ4, rule.PointsEmergency);
    }
}
