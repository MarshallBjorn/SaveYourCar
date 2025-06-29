using Core.DTOs;
using Core.Entities;
using FluentValidation;
using Infrastructure.Repositories;

namespace Infrastructure.Services;

public class FirmService : IFirmService
{
    private readonly IFirmRepository _repository;
    private readonly IValidator<Firm> _validator;

    public FirmService(IFirmRepository repository, IValidator<Firm> validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public async Task<List<FirmDto>> GetAllAsync()
    {
        var firms = await _repository.GetAllAsync();

        return firms.Select(f => new FirmDto
        {
            Id = f.Id,
            Name = f.Name,
            CountryCode = f.CountryCode,
            CreatedAt = f.CreatedAt,
            InsuranceTypes = f.InsuranceTypes.Select(it => new InsuranceTypeDto
            {
                Id = it.Id,
                Name = it.Name,
                PolicyDescription = it.PolicyDescription,
                PolicyNumber = it.PolicyNumber,
                Price = it.Price
            }).ToList()
        }).ToList();
    }

    public async Task CreateAsync(CreateFirmDto dto)
    {
        var firm = new Firm
        {
            Name = dto.Name,
            UserId = dto.UserId,
            CountryCode = dto.CountryCode
        };

        var validationResult = await _validator.ValidateAsync(firm);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        await _repository.CreateAsync(firm);
    }

    public async Task UpdateAsync(UpdateFirmDto dto)
    {
        var firm = await _repository.GetByIdAsync(dto.Id);
        if (firm == null) throw new KeyNotFoundException("Firm not found.");

        firm.Name = dto.Name;
        firm.CountryCode = dto.CountryCode;

        var validationResult = await _validator.ValidateAsync(firm);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        await _repository.UpdateAsync(firm);
    }

    public async Task<List<FirmDto>> GetAllByUserAsync(Guid userId)
    {
        var firms = await _repository.GetAllByUserIdAsync(userId);

        return firms.Select(f => new FirmDto
        {
            Id = f.Id,
            Name = f.Name,
            CountryCode = f.CountryCode,
            CreatedAt = f.CreatedAt,
            UserId = f.UserId,
            InsuranceTypes = f.InsuranceTypes.Select(it => new InsuranceTypeDto
            {
                Id = it.Id,
                Name = it.Name,
                PolicyDescription = it.PolicyDescription,
                PolicyNumber = it.PolicyNumber,
                Price = it.Price
            }).ToList()
        }).ToList();
    }

    public async Task<int> CountAsync()
    {
        return await _repository.GetCountAsync();
    }
}
