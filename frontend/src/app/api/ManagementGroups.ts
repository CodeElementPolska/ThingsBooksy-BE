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
  MicrosoftAspNetCoreMvcProblemDetails,
  ThingsBooksyModulesManagementGroupsApiRequestsAddGroupMemberRequest,
  ThingsBooksyModulesManagementGroupsApiRequestsCreateManagementGroupRequest,
  ThingsBooksyModulesManagementGroupsApiRequestsUpdateManagementGroupRequest,
  ThingsBooksyModulesManagementGroupsCoreFeaturesGetGroupMembersGetGroupMembersQueryResult,
  ThingsBooksyModulesManagementGroupsCoreFeaturesGetManagementGroupGetManagementGroupQueryResult,
  ThingsBooksyModulesManagementGroupsCoreFeaturesIsGroupNameAvailableIsGroupNameAvailableQueryResult,
} from "./data-contracts";
import { ContentType, HttpClient, RequestParams } from "./http-client";

export class ManagementGroups<
  SecurityDataType = unknown,
> extends HttpClient<SecurityDataType> {
  /**
   * No description
   *
   * @tags ManagementGroups
   * @name IsGroupNameAvailable
   * @summary Check whether a group name is available for the authenticated user.
   * @request GET:/management-groups/name-available
   * @secure
   */
  isGroupNameAvailable = (
    query?: {
      name?: string;
    },
    params: RequestParams = {},
  ) =>
    this.request<
      ThingsBooksyModulesManagementGroupsCoreFeaturesIsGroupNameAvailableIsGroupNameAvailableQueryResult,
      MicrosoftAspNetCoreMvcProblemDetails
    >({
      path: `/management-groups/name-available`,
      method: "GET",
      query: query,
      secure: true,
      format: "json",
      ...params,
    });
  /**
   * No description
   *
   * @tags ManagementGroups
   * @name GetGroupMembers
   * @summary Get paginated list of members for a management group.
   * @request GET:/management-groups/{id}/members
   * @secure
   */
  getGroupMembers = (
    id: string,
    query?: {
      /** @format uuid */
      afterId?: string;
      /** @format int32 */
      take?: number;
    },
    params: RequestParams = {},
  ) =>
    this.request<
      ThingsBooksyModulesManagementGroupsCoreFeaturesGetGroupMembersGetGroupMembersQueryResult,
      MicrosoftAspNetCoreMvcProblemDetails
    >({
      path: `/management-groups/${id}/members`,
      method: "GET",
      query: query,
      secure: true,
      format: "json",
      ...params,
    });
  /**
   * No description
   *
   * @tags ManagementGroups
   * @name AddGroupMember
   * @request POST:/management-groups/{id}/members
   * @secure
   */
  addGroupMember = (
    id: string,
    data: ThingsBooksyModulesManagementGroupsApiRequestsAddGroupMemberRequest,
    params: RequestParams = {},
  ) =>
    this.request<void, any>({
      path: `/management-groups/${id}/members`,
      method: "POST",
      body: data,
      secure: true,
      type: ContentType.Json,
      ...params,
    });
  /**
   * No description
   *
   * @tags ManagementGroups
   * @name CreateManagementGroup
   * @request POST:/management-groups
   * @secure
   */
  createManagementGroup = (
    data: ThingsBooksyModulesManagementGroupsApiRequestsCreateManagementGroupRequest,
    params: RequestParams = {},
  ) =>
    this.request<void, any>({
      path: `/management-groups`,
      method: "POST",
      body: data,
      secure: true,
      type: ContentType.Json,
      ...params,
    });
  /**
   * No description
   *
   * @tags ManagementGroups
   * @name GetManagementGroups
   * @request GET:/management-groups
   * @secure
   */
  getManagementGroups = (params: RequestParams = {}) =>
    this.request<void, any>({
      path: `/management-groups`,
      method: "GET",
      secure: true,
      ...params,
    });
  /**
   * No description
   *
   * @tags ManagementGroups
   * @name GetManagementGroup
   * @request GET:/management-groups/{id}
   * @secure
   */
  getManagementGroup = (id: string, params: RequestParams = {}) =>
    this.request<
      ThingsBooksyModulesManagementGroupsCoreFeaturesGetManagementGroupGetManagementGroupQueryResult,
      MicrosoftAspNetCoreMvcProblemDetails
    >({
      path: `/management-groups/${id}`,
      method: "GET",
      secure: true,
      format: "json",
      ...params,
    });
  /**
   * No description
   *
   * @tags ManagementGroups
   * @name UpdateManagementGroup
   * @request PUT:/management-groups/{id}
   * @secure
   */
  updateManagementGroup = (
    id: string,
    data: ThingsBooksyModulesManagementGroupsApiRequestsUpdateManagementGroupRequest,
    params: RequestParams = {},
  ) =>
    this.request<void, any>({
      path: `/management-groups/${id}`,
      method: "PUT",
      body: data,
      secure: true,
      type: ContentType.Json,
      ...params,
    });
  /**
   * No description
   *
   * @tags ManagementGroups
   * @name DeleteManagementGroup
   * @request DELETE:/management-groups/{id}
   * @secure
   */
  deleteManagementGroup = (id: string, params: RequestParams = {}) =>
    this.request<void, any>({
      path: `/management-groups/${id}`,
      method: "DELETE",
      secure: true,
      ...params,
    });
  /**
   * No description
   *
   * @tags ManagementGroups
   * @name RestoreManagementGroup
   * @request POST:/management-groups/{id}/restore
   * @secure
   */
  restoreManagementGroup = (id: string, params: RequestParams = {}) =>
    this.request<void, any>({
      path: `/management-groups/${id}/restore`,
      method: "POST",
      secure: true,
      ...params,
    });
  /**
   * No description
   *
   * @tags ManagementGroups
   * @name RemoveGroupMember
   * @request DELETE:/management-groups/{id}/members/{memberId}
   * @secure
   */
  removeGroupMember = (
    id: string,
    memberId: string,
    params: RequestParams = {},
  ) =>
    this.request<void, any>({
      path: `/management-groups/${id}/members/${memberId}`,
      method: "DELETE",
      secure: true,
      ...params,
    });
}
