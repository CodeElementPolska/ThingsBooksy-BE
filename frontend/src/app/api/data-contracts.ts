/* eslint-disable */
/* tslint:disable */
// @ts-nocheck
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

/** @format int32 */
export enum ThingsBooksyModulesResourcesCoreDomainPropertyDataType {
  Value0 = 0,
  Value1 = 1,
  Value2 = 2,
}

export interface ThingsBooksyModulesManagementGroupsApiRequestsAddGroupMemberRequest {
  email?: string | null;
}

export interface ThingsBooksyModulesManagementGroupsApiRequestsCreateManagementGroupRequest {
  name?: string | null;
  description?: string | null;
}

export interface ThingsBooksyModulesManagementGroupsApiRequestsUpdateManagementGroupRequest {
  name?: string | null;
  description?: string | null;
}

export interface ThingsBooksyModulesManagementGroupsCoreFeaturesGetManagementGroupModelsResultsManagementGroupMemberResult {
  /** @format uuid */
  userId?: string;
  /** @format date-time */
  joinedAt?: string;
}

export interface ThingsBooksyModulesManagementGroupsCoreFeaturesGetManagementGroupGetManagementGroupQueryResult {
  /** @format uuid */
  id?: string;
  name?: string | null;
  description?: string | null;
  /** @format uuid */
  ownerId?: string;
  /** @format date-time */
  createdAt?: string;
  /** @format int32 */
  memberCount?: number;
  members?:
    | ThingsBooksyModulesManagementGroupsCoreFeaturesGetManagementGroupModelsResultsManagementGroupMemberResult[]
    | null;
}

export interface ThingsBooksyModulesManagementGroupsCoreFeaturesGetGroupMembersGroupMemberDto {
  /** @format uuid */
  memberId?: string;
  /** @format uuid */
  userId?: string;
  email?: string | null;
  /** @format date-time */
  joinedAt?: string;
  isOwner?: boolean;
}

export interface ThingsBooksyModulesManagementGroupsCoreFeaturesGetGroupMembersGetGroupMembersQueryResult {
  items?: ThingsBooksyModulesManagementGroupsCoreFeaturesGetGroupMembersGroupMemberDto[] | null;
  /** @format uuid */
  nextCursor?: string | null;
}

export interface ThingsBooksyModulesManagementGroupsCoreFeaturesIsGroupNameAvailableIsGroupNameAvailableQueryResult {
  available?: boolean;
}

export interface ThingsBooksyModulesResourcesApiRequestsCreateResourceInstanceRequest {
  /** @format uuid */
  resourceTypeId?: string;
  name?: string | null;
  description?: string | null;
  propertyValues?:
    | ThingsBooksyModulesResourcesApiRequestsPropertyValueInputDto[]
    | null;
}

export interface ThingsBooksyModulesResourcesApiRequestsCreateResourceTypeRequest {
  /** @format uuid */
  groupId?: string;
  name?: string | null;
  description?: string | null;
  propertyDefinitions?:
    | ThingsBooksyModulesResourcesApiRequestsPropertyDefinitionInputDto[]
    | null;
}

export interface ThingsBooksyModulesResourcesApiRequestsPropertyDefinitionInputDto {
  name?: string | null;
  dataType?: ThingsBooksyModulesResourcesCoreDomainPropertyDataType;
  isRequired?: boolean;
}

export interface ThingsBooksyModulesResourcesApiRequestsPropertyDefinitionUpdateInputDto {
  /** @format uuid */
  id?: string | null;
  name?: string | null;
  dataType?: ThingsBooksyModulesResourcesCoreDomainPropertyDataType;
  isRequired?: boolean;
}

export interface ThingsBooksyModulesResourcesApiRequestsPropertyValueInputDto {
  /** @format uuid */
  propertyDefinitionId?: string;
  value?: string | null;
}

export interface ThingsBooksyModulesResourcesApiRequestsUpdatePropertyValueDto {
  /** @format uuid */
  propertyDefinitionId?: string;
  value?: string | null;
}

export interface ThingsBooksyModulesResourcesApiRequestsUpdateResourceInstanceRequest {
  name?: string | null;
  description?: string | null;
  propertyValues?:
    | ThingsBooksyModulesResourcesApiRequestsUpdatePropertyValueDto[]
    | null;
}

export interface ThingsBooksyModulesResourcesApiRequestsUpdateResourceTypeRequest {
  name?: string | null;
  description?: string | null;
  propertyDefinitions?:
    | ThingsBooksyModulesResourcesApiRequestsPropertyDefinitionUpdateInputDto[]
    | null;
}

export interface ThingsBooksyModulesResourcesCoreFeaturesGetResourceInstanceModelsPropertyValueResult {
  /** @format uuid */
  propertyDefinitionId?: string;
  propertyName?: string | null;
  dataType?: string | null;
  value?: string | null;
}

export interface ThingsBooksyModulesResourcesCoreFeaturesGetResourceInstancesResourceInstanceRowDto {
  /** @format uuid */
  id?: string;
  /** @format uuid */
  resourceTypeId?: string;
  /** @format uuid */
  groupId?: string;
  name?: string | null;
  description?: string | null;
  /** @format uuid */
  ownerId?: string;
  /** @format date-time */
  createdAt?: string;
  /** @format date-time */
  deletedAt?: string | null;
  propertyValues?:
    | ThingsBooksyModulesResourcesCoreFeaturesGetResourceInstanceModelsPropertyValueResult[]
    | null;
}

export interface ThingsBooksyModulesResourcesCoreFeaturesGetResourceInstancesGetResourceInstancesQueryResult {
  items?:
    | ThingsBooksyModulesResourcesCoreFeaturesGetResourceInstancesResourceInstanceRowDto[]
    | null;
  /** @format uuid */
  nextCursor?: string | null;
}

export interface ThingsBooksyModulesUsersApiRequestsSignInRequest {
  email?: string | null;
  password?: string | null;
}

export interface ThingsBooksyModulesUsersApiRequestsSignUpRequest {
  email?: string | null;
  password?: string | null;
  jobTitle?: string | null;
  role?: string | null;
}

export interface ThingsBooksySharedAbstractionsAppInfo {
  name?: string | null;
  version?: string | null;
}

export interface ThingsBooksySharedInfrastructureModulesModuleInfo {
  name?: string | null;
  policies?: string[] | null;
}

export enum AvailabilityRuleType {
  RECURRING = "RECURRING",
  ONE_OFF = "ONE_OFF",
}

export enum AvailabilityRuleMode {
  AVAILABLE = "AVAILABLE",
  UNAVAILABLE = "UNAVAILABLE",
}

export interface AvailabilityRuleDto {
  /** @format uuid */
  ruleId: string;
  ruleType: AvailabilityRuleType;
  ruleMode: AvailabilityRuleMode;
  /** DayOfWeek values (0=Sunday … 6=Saturday); populated for RECURRING, null for ONE_OFF */
  daysOfWeek?: number[] | null;
  /** Wall-clock time in HH:mm format */
  startTime: string;
  /** Wall-clock time in HH:mm format */
  endTime: string;
  /**
   * 0 (same day) or 1 (next calendar day, auto-set when endTime < startTime)
   * @format int32
   */
  endDayOffset: number;
  /** ISO 8601 date (YYYY-MM-DD); populated for ONE_OFF, null for RECURRING */
  startDate?: string | null;
  /** ISO 8601 date (YYYY-MM-DD); populated for ONE_OFF, null for RECURRING */
  endDate?: string | null;
}

export interface AvailabilityRulesResponse {
  /**
   * Effective buffer in minutes (schema default or resource override)
   * @format int32
   */
  bufferMinutes: number;
  /** Resource-specific rules (or schema rules for schema-level GET) */
  rules: AvailabilityRuleDto[];
  /** Schema rules when endpoint is resource-level; null for schema-level GET */
  inheritedRules?: AvailabilityRuleDto[] | null;
}

export interface NewRuleRequest {
  ruleType: AvailabilityRuleType;
  ruleMode: AvailabilityRuleMode;
  /** Required for RECURRING; must be omitted or null for ONE_OFF */
  daysOfWeek?: number[] | null;
  /** Required; HH:mm format */
  startTime: string;
  /** Required; HH:mm format */
  endTime: string;
  /** Required for ONE_OFF (YYYY-MM-DD); must be omitted or null for RECURRING */
  startDate?: string | null;
  /** Required for ONE_OFF (YYYY-MM-DD); must be omitted or null for RECURRING */
  endDate?: string | null;
}

export interface RuleSetUpdateRequest {
  /**
   * Required; 0 = no buffer; maximum 1440 (24 hours)
   * @format int32
   */
  bufferMinutes: number;
  /** Full replacement set; empty array = clear all rules */
  rules: NewRuleRequest[];
}
