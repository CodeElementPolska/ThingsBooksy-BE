import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  ThingsBooksyModulesResourcesApiRequestsCreateResourceInstanceRequest as CreateResourceInstanceRequest,
  ThingsBooksyModulesResourcesApiRequestsCreateResourceTypeRequest as CreateResourceTypeRequest,
  ThingsBooksyModulesResourcesApiRequestsUpdateResourceTypeRequest as UpdateResourceTypeRequest,
  ThingsBooksyModulesResourcesCoreFeaturesGetResourceInstancesGetResourceInstancesQueryResult as ResourceInstancesPage,
} from '../../../api/data-contracts';
import { CursorParams } from './groups-api.service';

export type ResourceInstancesPageDto = ResourceInstancesPage;

export interface PropertyDefinitionDto {
  readonly id: string;
  readonly name: string;
  readonly dataType: 'Text' | 'Number' | 'Boolean';
  readonly isRequired: boolean;
}

export interface ResourceTypeSummaryDto {
  readonly id: string;
  readonly groupId: string;
  readonly name: string;
  readonly description: string | null;
  readonly createdAt: string;
  readonly propertyDefinitions: PropertyDefinitionDto[];
}

export type ResourceTypeDetailDto = ResourceTypeSummaryDto;

export interface CreateResourceInstancePayload {
  resourceTypeId: string;
  name: string;
  description: string | null;
  propertyValues: { propertyDefinitionId: string; value: string | null }[];
}

@Injectable({ providedIn: 'root' })
export class ResourcesApiService {
  private readonly http = inject(HttpClient);

  getResourceTypes(groupId: string): Observable<ResourceTypeSummaryDto[]> {
    return this.http.get<ResourceTypeSummaryDto[]>('/resources/types', {
      params: { groupId },
    });
  }

  getResourceType(id: string): Observable<ResourceTypeDetailDto> {
    return this.http.get<ResourceTypeDetailDto>(`/resources/types/${id}`);
  }

  createResourceType(req: CreateResourceTypeRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/resources/types', req);
  }

  updateResourceType(id: string, req: UpdateResourceTypeRequest): Observable<void> {
    return this.http.put<void>(`/resources/types/${id}`, req).pipe(map(() => void 0));
  }

  deleteResourceType(id: string): Observable<void> {
    return this.http.delete<void>(`/resources/types/${id}`).pipe(map(() => void 0));
  }

  getResourceInstances(
    params: CursorParams & { groupId: string },
  ): Observable<ResourceInstancesPageDto> {
    const query: Record<string, string> = { groupId: params.groupId };
    if (params.afterId) query['afterId'] = params.afterId;
    if (params.take != null) query['take'] = String(params.take);
    return this.http.get<ResourceInstancesPageDto>('/resources/instances', {
      params: query,
    });
  }

  createResourceInstance(
    payload: CreateResourceInstancePayload,
  ): Observable<{ id: string }> {
    const req: CreateResourceInstanceRequest = {
      resourceTypeId: payload.resourceTypeId,
      name: payload.name,
      description: payload.description,
      propertyValues: payload.propertyValues,
    };
    return this.http.post<{ id: string }>('/resources/instances', req);
  }

  getResourceInstance(id: string): Observable<{ id: string; name: string; resourceTypeId: string; resourceTypeName?: string }> {
    return this.http.get<{ id: string; name: string; resourceTypeId: string; resourceTypeName?: string }>(`/resources/instances/${id}`);
  }
}
