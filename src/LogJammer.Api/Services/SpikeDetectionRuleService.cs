using LogJammer.Api.Dtos;
using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;

namespace LogJammer.Api.Services;

public class SpikeDetectionRuleService(ISpikeDetectionRuleRepository ruleRepo) : ISpikeDetectionRuleService
{
    public async Task<IReadOnlyList<SpikeDetectionRuleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rules = await ruleRepo.GetAllAsync(cancellationToken);
        return rules.Select(MapToDto).ToList();
    }

    public async Task<SpikeDetectionRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await ruleRepo.GetByIdAsync(id, cancellationToken);
        return rule is null ? null : MapToDto(rule);
    }

    public async Task<SpikeDetectionRuleDto> CreateAsync(CreateSpikeDetectionRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = new SpikeDetectionRule
        {
            KnownErrorId = request.KnownErrorId,
            ThresholdType = request.ThresholdType,
            ThresholdValue = request.ThresholdValue,
            WindowMinutes = request.WindowMinutes,
            LookbackMinutes = request.LookbackMinutes,
            Enabled = request.Enabled
        };

        await ruleRepo.AddAsync(rule, cancellationToken);
        return MapToDto(rule);
    }

    public async Task<SpikeDetectionRuleDto?> UpdateAsync(Guid id, UpdateSpikeDetectionRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await ruleRepo.GetByIdAsync(id, cancellationToken);
        if (rule is null) return null;

        if (request.ThresholdType.HasValue) rule.ThresholdType = request.ThresholdType.Value;
        if (request.ThresholdValue.HasValue) rule.ThresholdValue = request.ThresholdValue.Value;
        if (request.WindowMinutes.HasValue) rule.WindowMinutes = request.WindowMinutes.Value;
        if (request.LookbackMinutes.HasValue) rule.LookbackMinutes = request.LookbackMinutes.Value;
        if (request.Enabled.HasValue) rule.Enabled = request.Enabled.Value;

        await ruleRepo.UpdateAsync(rule, cancellationToken);
        return MapToDto(rule);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await ruleRepo.GetByIdAsync(id, cancellationToken);
        if (rule is null) return false;

        await ruleRepo.DeleteAsync(id, cancellationToken);
        return true;
    }

    private static SpikeDetectionRuleDto MapToDto(SpikeDetectionRule r) => new()
    {
        Id = r.Id,
        KnownErrorId = r.KnownErrorId,
        KnownErrorMessage = r.KnownError?.RepresentativeMessage,
        ThresholdType = r.ThresholdType,
        ThresholdValue = r.ThresholdValue,
        WindowMinutes = r.WindowMinutes,
        LookbackMinutes = r.LookbackMinutes,
        Enabled = r.Enabled,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };
}
