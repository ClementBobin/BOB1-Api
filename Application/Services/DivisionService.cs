using Domain.Entities;

namespace Application.Services;

using Interfaces;

using Domain.Dto;

using Infrastructure.Interfaces;

using NLog;

public class DivisionService : IDivisionService
{
    private readonly IDivisionRepository _divisions;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public DivisionService(IDivisionRepository divisions) => _divisions = divisions;

    public async Task<IEnumerable<DivisionDto>> GetAllAsync()
    {
        Log.Debug("GetAllAsync");
        return (await _divisions.GetAllAsync())
            .Select(d => new DivisionDto(d.Id, d.Name));
    }

    public async Task<DivisionDto> CreateAsync(CreateDivisionRequest request)
    {
        Log.Info("Creating division={DivisionName}", request.Name);

        if (await _divisions.ExistsByNameAsync(request.Name))
            throw new InvalidOperationException($"Name '{request.Name}' is already taken.");

        var division = new Division
        {
            Id = Guid.NewGuid(),
            Name =  request.Name
        };

        await _divisions.AddAsync(division);

        var created = await _divisions.GetByIdAsync(division.Id)
                      ?? throw new InvalidOperationException("Division not found after creation.");

        return new DivisionDto(created.Id, created.Name);
    }

    public async Task DeleteAsync(Guid id)
    {
        Log.Info("Deleting match {Id}", id);
        await _divisions.DeleteAsync(id);
    }
}
