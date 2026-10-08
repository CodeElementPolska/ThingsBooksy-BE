import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  ThingsBooksyModulesResourcesApiRequestsCreateResourceInstanceRequest as CreateResourceInstanceRequest,
  ThingsBooksyModulesResourcesApiRequestsCreateResourceSchemaRequest as CreateResourceSchemaRequest,
  ThingsBooksyModulesResourcesApiRequestsUpdateResourceSchemaRequest as UpdateResourceSchemaRequest,
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

export interface ResourceSchemaSummaryDto {
  readonly id: string;
  readonly groupId: string;
  readonly name: string;
  readonly description: string | null;
  readonly createdAt: string;
  readonly propertyDefinitions: PropertyDefinitionDto[];
}

export type ResourceSchemaDetailDto = ResourceSchemaSummaryDto;

export interface CreateResourceInstancePayload {
  resourceSchemaId: string;
  name: string;
  description: string | null;
  propertyValues: { propertyDefinitionId: string; value: string | null }[];
}

@Injectable({ providedIn: 'root' })
export class ResourcesApiService {
  private readonly http = inject(HttpClient);

  getResourceSchemas(groupId: string): Observable<ResourceSchemaSummaryDto[]> {
    return this.http.get<ResourceSchemaSummaryDto[]>('/resources/schemas', {
      params: { groupId },
    });
  }

  getResourceSchema(id: string): Observable<ResourceSchemaDetailDto> {
    return this.http.get<ResourceSchemaDetailDto>(`/resources/schemas/${id}`);
  }

  createResourceSchema(req: CreateResourceSchemaRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/resources/schemas', req);
  }

  updateResourceSchema(id: string, req: UpdateResourceSchemaRequest): Observable<void> {
    return this.http.put<void>(`/resources/schemas/${id}`, req).pipe(map(() => void 0));
  }

  deleteResourceSchema(id: string): Observable<void> {
    return this.http.delete<void>(`/resources/schemas/${id}`).pipe(map(() => void 0));
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
      resourceSchemaId: payload.resourceSchemaId,
      name: payload.name,
      description: payload.description,
      propertyValues: payload.propertyValues,
    };
    return this.http.post<{ id: string }>('/resources/instances', req);
  }
}
