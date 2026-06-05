import {
  AvailabilityRuleDto,
  AvailabilityRuleMode as DtoRuleMode,
  AvailabilityRuleType as DtoRuleType,
  AvailabilityRulesResponse,
  NewRuleRequest,
  RuleSetUpdateRequest,
} from '../../../api/data-contracts';
import {
  AvailabilityRule,
  AvailabilityRulesViewModel,
  NewRuleViewModel,
  RuleSetUpdateViewModel,
} from './availability.model';

export function mapRuleDto(dto: AvailabilityRuleDto): AvailabilityRule {
  return {
    ruleId: dto.ruleId,
    ruleType: dto.ruleType as unknown as 'RECURRING' | 'ONE_OFF',
    ruleMode: dto.ruleMode as unknown as 'AVAILABLE' | 'UNAVAILABLE',
    daysOfWeek: dto.daysOfWeek ?? null,
    startTime: dto.startTime,
    endTime: dto.endTime,
    endDayOffset: dto.endDayOffset,
    startDate: dto.startDate ?? null,
    endDate: dto.endDate ?? null,
  };
}

export function mapRulesResponse(
  dto: AvailabilityRulesResponse,
): AvailabilityRulesViewModel {
  return {
    bufferMinutes: dto.bufferMinutes,
    rules: dto.rules.map(mapRuleDto),
    inheritedRules: dto.inheritedRules ? dto.inheritedRules.map(mapRuleDto) : null,
  };
}

export function mapToRuleSetRequest(
  vm: RuleSetUpdateViewModel,
): RuleSetUpdateRequest {
  return {
    bufferMinutes: vm.bufferMinutes,
    rules: vm.rules.map(mapNewRuleToRequest),
  };
}

function mapNewRuleToRequest(vm: NewRuleViewModel): NewRuleRequest {
  return {
    ruleType: vm.ruleType as unknown as DtoRuleType,
    ruleMode: vm.ruleMode as unknown as DtoRuleMode,
    daysOfWeek: vm.daysOfWeek,
    startTime: vm.startTime,
    endTime: vm.endTime,
    startDate: vm.startDate,
    endDate: vm.endDate,
  };
}
