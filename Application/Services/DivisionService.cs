namespace Application.Services;

using Application.Interfaces;

using Domain.Dto;

using Infrastructure.Interfaces;

using NLog;

public class DivisionService : IDivisionService
{
    private readonly IDivisionRepository _divisions;
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public DivisionService(IDivisionRepository divisions) => _divisions = divisions;

    public async Task<IEnumerable<DivisionDto>> GetAllAsync()
    {
        _log.Debug("GetAllAsync");
        return (await _divisions.GetAllAsync())
            .Select(d => new DivisionDto(d.Id, d.Name));
    }
}
