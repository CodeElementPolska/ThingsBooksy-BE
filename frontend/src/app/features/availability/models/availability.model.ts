export type AvailabilityRuleType = 'RECURRING' | 'ONE_OFF';
export type AvailabilityRuleMode = 'AVAILABLE' | 'UNAVAILABLE';

export interface AvailabilityRule {
  ruleId: string;
  ruleType: AvailabilityRuleType;
  ruleMode: AvailabilityRuleMode;
  daysOfWeek: number[] | null;
  startTime: string;
  endTime: string;
  endDayOffset: number;
  startDate: string | null;
  endDate: string | null;
}

export interface AvailabilityRulesViewModel {
  bufferMinutes: number;
  rules: AvailabilityRule[];
  inheritedRules: AvailabilityRule[] | null;
}

export interface NewRuleViewModel {
  ruleType: AvailabilityRuleType;
  ruleMode: AvailabilityRuleMode;
  daysOfWeek: number[] | null;
  startTime: string;
  endTime: string;
  startDate: string | null;
  endDate: string | null;
}

export interface RuleSetUpdateViewModel {
  bufferMinutes: number;
  rules: NewRuleViewModel[];
}

export interface AvailabilityConflictError {
  type: 'conflict';
  detail: string;
}

export function isAvailabilityConflictError(
  err: unknown,
): err is AvailabilityConflictError {
  return (
    typeof err === 'object' &&
    err !== null &&
    (err as AvailabilityConflictError).type === 'conflict'
  );
}
