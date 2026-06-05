import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import {
  AvailabilityRulesResponse,
  RuleSetUpdateRequest,
} from '../../api/data-contracts';
import {
  mapRulesResponse,
  mapToRuleSetRequest,
} from './models/availability.mapper';
import {
  AvailabilityConflictError,
  AvailabilityRulesViewModel,
  RuleSetUpdateViewModel,
} from './models/availability.model';

@Injectable({ providedIn: 'root' })
export class AvailabilityService {
  private readonly http = inject(HttpClient);

  getSchemaRules(schemaId: string): Observable<AvailabilityRulesViewModel> {
    return this.http
      .get<AvailabilityRulesResponse>(
        `/availability/schemas/${schemaId}/rules`,
      )
      .pipe(map(mapRulesResponse));
  }

  upsertSchemaRules(
    schemaId: string,
    viewModel: RuleSetUpdateViewModel,
  ): Observable<void> {
    const request: RuleSetUpdateRequest = mapToRuleSetRequest(viewModel);
    return this.http
      .put<void>(`/availability/schemas/${schemaId}/rules`, request)
      .pipe(
        map(() => void 0),
        catchError((error: HttpErrorResponse) => {
          if (error.status === 400) {
            const conflict: AvailabilityConflictError = {
              type: 'conflict',
              detail:
                error.error?.detail ??
                'Availability rules contain an overlap conflict.',
            };
            return throwError(() => conflict);
          }
          return throwError(() => error);
        }),
      );
  }

  getResourceRules(resourceId: string): Observable<AvailabilityRulesViewModel> {
    return this.http
      .get<AvailabilityRulesResponse>(
        `/availability/resources/${resourceId}/rules`,
      )
      .pipe(map(mapRulesResponse));
  }

  upsertResourceRules(
    resourceId: string,
    viewModel: RuleSetUpdateViewModel,
  ): Observable<void> {
    const request: RuleSetUpdateRequest = mapToRuleSetRequest(viewModel);
    return this.http
      .put<void>(`/availability/resources/${resourceId}/rules`, request)
      .pipe(
        map(() => void 0),
        catchError((error: HttpErrorResponse) => {
          if (error.status === 400) {
            const conflict: AvailabilityConflictError = {
              type: 'conflict',
              detail:
                error.error?.detail ??
                'Availability rules contain an overlap conflict.',
            };
            return throwError(() => conflict);
          }
          return throwError(() => error);
        }),
      );
  }
}
