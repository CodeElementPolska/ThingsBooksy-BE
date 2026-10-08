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
  ThingsBooksyModulesResourcesApiRequestsCreateResourceInstanceRequest,
  ThingsBooksyModulesResourcesApiRequestsCreateResourceSchemaRequest,
  ThingsBooksyModulesResourcesApiRequestsUpdateResourceInstanceRequest,
  ThingsBooksyModulesResourcesApiRequestsUpdateResourceSchemaRequest,
  ThingsBooksyModulesResourcesCoreFeaturesGetResourceInstancesGetResourceInstancesQueryResult,
} from "./data-contracts";
import { ContentType, HttpClient, RequestParams } from "./http-client";

export class Resources<
  SecurityDataType = unknown,
> extends HttpClient<SecurityDataType> {
  /**
   * No description
   *
   * @tags Resources
   * @name CreateResourceSchema
   * @request POST:/resources/schemas
   * @secure
   */
  createResourceSchema = (
    data: ThingsBooksyModulesResourcesApiRequestsCreateResourceSchemaRequest,
    params: RequestParams = {},
  ) =>
    this.request<void, any>({
      path: `/resources/schemas`,
      method: "POST",
      body: data,
      secure: true,
      type: ContentType.Json,
      ...params,
    });
  /**
   * No description
   *
   * @tags Resources
   * @name GetResourceSchemas
   * @request GET:/resources/schemas
   * @secure
   */
  getResourceSchemas = (
    query: {
      /** @format uuid */
      groupId: string;
    },
    params: RequestParams = {},
  ) =>
    this.request<void, any>({
      path: `/resources/schemas`,
      method: "GET",
      query: query,
      secure: true,
      ...params,
    });
  /**
   * No description
   *
   * @tags Resources
   * @name UpdateResourceSchema
   * @request PUT:/resources/schemas/{id}
   * @secure
   */
  updateResourceSchema = (
    id: string,
    data: ThingsBooksyModulesResourcesApiRequestsUpdateResourceSchemaRequest,
    params: RequestParams = {},
  ) =>
    this.request<void, any>({
      path: `/resources/schemas/${id}`,
      method: "PUT",
      body: data,
      secure: true,
      type: ContentType.Json,
      ...params,
    });
  /**
   * No description
   *
   * @tags Resources
   * @name DeleteResourceSchema
   * @request DELETE:/resources/schemas/{id}
   * @secure
   */
  deleteResourceSchema = (id: string, params: RequestParams = {}) =>
    this.request<void, any>({
      path: `/resources/schemas/${id}`,
      method: "DELETE",
      secure: true,
      ...params,
    });
  /**
   * No description
   *
   * @tags Resources
   * @name GetResourceSchema
   * @request GET:/resources/schemas/{id}
   * @secure
   */
  getResourceSchema = (id: string, params: RequestParams = {}) =>
    this.request<void, any>({
      path: `/resources/schemas/${id}`,
      method: "GET",
      secure: true,
      ...params,
    });
  /**
   * No description
   *
   * @tags Resources
   * @name CreateResourceInstance
   * @request POST:/resources/instances
   * @secure
   */
  createResourceInstance = (
    data: ThingsBooksyModulesResourcesApiRequestsCreateResourceInstanceRequest,
    params: RequestParams = {},
  ) =>
    this.request<void, any>({
      path: `/resources/instances`,
      method: "POST",
      body: data,
      secure: true,
      type: ContentType.Json,
      ...params,
    });
  /**
   * No description
   *
   * @tags Resources
   * @name GetResourceInstances
   * @summary Returns a cursor-paginated list of resource instances. Use afterId + take for forward-only infinite scroll.
   * @request GET:/resources/instances
   * @secure
   */
  getResourceInstances = (
    query?: {
      /** @format uuid */
      resourceSchemaId?: string;
      /** @format uuid */
      groupId?: string;
      includeDeleted?: boolean;
      /** @format uuid */
      afterId?: string;
      /** @format int32 */
      take?: number;
    },
    params: RequestParams = {},
  ) =>
    this.request<
      ThingsBooksyModulesResourcesCoreFeaturesGetResourceInstancesGetResourceInstancesQueryResult,
      any
    >({
      path: `/resources/instances`,
      method: "GET",
      query: query,
      secure: true,
      format: "json",
      ...params,
    });
  /**
   * No description
   *
   * @tags Resources
   * @name GetResourceInstance
   * @request GET:/resources/instances/{id}
   * @secure
   */
  getResourceInstance = (id: string, params: RequestParams = {}) =>
    this.request<void, any>({
      path: `/resources/instances/${id}`,
      method: "GET",
      secure: true,
      ...params,
    });
  /**
   * No description
   *
   * @tags Resources
   * @name UpdateResourceInstance
   * @request PUT:/resources/instances/{id}
   * @secure
   */
  updateResourceInstance = (
    id: string,
    data: ThingsBooksyModulesResourcesApiRequestsUpdateResourceInstanceRequest,
    params: RequestParams = {},
  ) =>
    this.request<void, any>({
      path: `/resources/instances/${id}`,
      method: "PUT",
      body: data,
      secure: true,
      type: ContentType.Json,
      ...params,
    });
  /**
   * No description
   *
   * @tags Resources
   * @name DeleteResourceInstance
   * @request DELETE:/resources/instances/{id}
   * @secure
   */
  deleteResourceInstance = (id: string, params: RequestParams = {}) =>
    this.request<void, any>({
      path: `/resources/instances/${id}`,
      method: "DELETE",
      secure: true,
      ...params,
    });
}
