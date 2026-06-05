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

import {
  AvailabilityRulesResponse,
  RuleSetUpdateRequest,
} from "./data-contracts";
import { ContentType, HttpClient, RequestParams } from "./http-client";

export class Availability<
  SecurityDataType = unknown,
> extends HttpClient<SecurityDataType> {
  /**
   * @summary Get availability rules for a resource schema
   *
   * @tags Availability
   * @name AvailabilityGetSchemaRules
   * @request GET:/availability/schemas/{schemaId}/rules
   * @secure
   */
  availabilityGetSchemaRules = (
    schemaId: string,
    params: RequestParams = {},
  ) =>
    this.request<AvailabilityRulesResponse, any>({
      path: `/availability/schemas/${schemaId}/rules`,
      method: "GET",
      secure: true,
      format: "json",
      ...params,
    });
  /**
   * @summary Replace the full availability rule set for a resource schema
   *
   * @tags Availability
   * @name AvailabilityUpsertSchemaRules
   * @request PUT:/availability/schemas/{schemaId}/rules
   * @secure
   */
  availabilityUpsertSchemaRules = (
    schemaId: string,
    data: RuleSetUpdateRequest,
    params: RequestParams = {},
  ) =>
    this.request<void, any>({
      path: `/availability/schemas/${schemaId}/rules`,
      method: "PUT",
      body: data,
      secure: true,
      type: ContentType.Json,
      ...params,
    });
  /**
   * @summary Get availability rules for a resource instance
   *
   * @tags Availability
   * @name AvailabilityGetResourceRules
   * @request GET:/availability/resources/{resourceId}/rules
   * @secure
   */
  availabilityGetResourceRules = (
    resourceId: string,
    params: RequestParams = {},
  ) =>
    this.request<AvailabilityRulesResponse, any>({
      path: `/availability/resources/${resourceId}/rules`,
      method: "GET",
      secure: true,
      format: "json",
      ...params,
    });
  /**
   * @summary Replace the full availability rule set for a resource instance
   *
   * @tags Availability
   * @name AvailabilityUpsertResourceRules
   * @request PUT:/availability/resources/{resourceId}/rules
   * @secure
   */
  availabilityUpsertResourceRules = (
    resourceId: string,
    data: RuleSetUpdateRequest,
    params: RequestParams = {},
  ) =>
    this.request<void, any>({
      path: `/availability/resources/${resourceId}/rules`,
      method: "PUT",
      body: data,
      secure: true,
      type: ContentType.Json,
      ...params,
    });
}
