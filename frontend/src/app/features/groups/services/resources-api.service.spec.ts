import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as resourcesApiModule from './resources-api.service';

/**
 * Story 016, AC-8: the hand-written Resources API service talks to the /resources/schemas
 * addresses and sends the schema reference of a new resource as `resourceSchemaId`.
 *
 * The service is looked up from the module's runtime exports and its methods are called by name
 * (the method names of data-model.md), so this spec compiles against the service before and after
 * the rename; a missing method fails at run time with a clear message.
 */

type ServiceMethod = (...args: unknown[]) => unknown;

/** Name of the schema reference before story 016 (concatenated: the old name must not appear in frontend/src). */
const LEGACY_SCHEMA_REFERENCE = 'resource' + 'TypeId';

const GROUP_ID = '11111111-1111-7111-8111-111111111111';
const SCHEMA_ID = '22222222-2222-7222-8222-222222222222';

function findServiceClass(): Type<unknown> {
  const exported: unknown[] = Object.values(resourcesApiModule);
  const candidates = exported.filter((value): value is Type<unknown> => typeof value === 'function');
  const service = candidates.find((c) => /Service$/.test(c.name)) ?? candidates[0];
  if (!service) {
    throw new Error('resources-api.service exports no service class');
  }
  return service;
}

function method(service: unknown, name: string): ServiceMethod {
  const fn = (service as Record<string, unknown>)[name];
  if (typeof fn !== 'function') {
    throw new Error(`Resources API service has no method '${name}'`);
  }
  return (fn as ServiceMethod).bind(service);
}

function methodNames(service: unknown): string[] {
  const proto = Object.getPrototypeOf(service) as Record<string, unknown>;
  return Object.getOwnPropertyNames(proto).filter((n) => n !== 'constructor' && typeof proto[n] === 'function');
}

/** Starts the call whether the service returns an Observable or a Promise. */
function start(result: unknown): void {
  const maybeObservable = result as { subscribe?: (observer: unknown) => unknown } | null;
  const maybePromise = result as { then?: unknown; catch?: (cb: () => void) => unknown } | null;
  if (maybeObservable && typeof maybeObservable.subscribe === 'function') {
    maybeObservable.subscribe({ next: () => undefined, error: () => undefined });
  } else if (maybePromise && typeof maybePromise.then === 'function' && typeof maybePromise.catch === 'function') {
    maybePromise.catch(() => undefined);
  }
}

describe('ResourcesApiService (story 016)', () => {
  let service: unknown;
  let http: HttpTestingController;

  beforeEach(() => {
    const serviceClass = findServiceClass();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), serviceClass],
    });
    service = TestBed.inject(serviceClass);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    try {
      http.verify();
    } finally {
      TestBed.resetTestingModule();
    }
  });

  function expectSingle(methodName: string, predicate: (req: TestRequest['request']) => boolean): TestRequest {
    return http.expectOne((r) => predicate(r), methodName);
  }

  it('[AC-8] getResourceSchemas lists a group\'s schemas from GET /resources/schemas?groupId=', () => {
    start(method(service, 'getResourceSchemas')(GROUP_ID));

    const req = expectSingle('getResourceSchemas', (r) => r.method === 'GET' && r.url.endsWith('/resources/schemas'));
    expect(req.request.urlWithParams).toContain(`groupId=${GROUP_ID}`);
    req.flush([]);
  });

  it('[AC-8] getResourceSchema reads one schema from GET /resources/schemas/{id}', () => {
    start(method(service, 'getResourceSchema')(SCHEMA_ID));

    const req = expectSingle('getResourceSchema', (r) => r.method === 'GET' && r.url.endsWith(`/resources/schemas/${SCHEMA_ID}`));
    req.flush({ id: SCHEMA_ID, groupId: GROUP_ID, name: 'Kayaks', description: null, createdAt: '2026-01-01T00:00:00Z', propertyDefinitions: [] });
  });

  it('[AC-8] createResourceSchema posts to POST /resources/schemas', () => {
    start(
      method(service, 'createResourceSchema')({
        groupId: GROUP_ID,
        name: 'Kayaks',
        description: null,
        propertyDefinitions: [],
      }),
    );

    const req = expectSingle('createResourceSchema', (r) => r.method === 'POST' && r.url.endsWith('/resources/schemas'));
    req.flush({ id: SCHEMA_ID });
  });

  it('[AC-8] updateResourceSchema puts to PUT /resources/schemas/{id}', () => {
    start(
      method(service, 'updateResourceSchema')(SCHEMA_ID, {
        name: 'Kayaks',
        description: 'Sea kayaks',
        propertyDefinitions: [],
      }),
    );

    const req = expectSingle('updateResourceSchema', (r) => r.method === 'PUT' && r.url.endsWith(`/resources/schemas/${SCHEMA_ID}`));
    req.flush(null);
  });

  it('[AC-8] deleteResourceSchema deletes via DELETE /resources/schemas/{id}', () => {
    start(method(service, 'deleteResourceSchema')(SCHEMA_ID));

    const req = expectSingle('deleteResourceSchema', (r) => r.method === 'DELETE' && r.url.endsWith(`/resources/schemas/${SCHEMA_ID}`));
    req.flush(null);
  });

  it('[AC-8] creating a resource posts resourceSchemaId to POST /resources/instances', () => {
    const createInstance = methodNames(service).find((n) => /^create/i.test(n) && !/schema/i.test(n));
    if (!createInstance) {
      throw new Error(`Resources API service has no create-resource method (methods: ${methodNames(service).join(', ')})`);
    }

    start(
      method(service, createInstance)({
        resourceSchemaId: SCHEMA_ID,
        name: 'Kayak 1',
        description: null,
        propertyValues: [],
      }),
    );

    const req = expectSingle(createInstance, (r) => r.method === 'POST' && r.url.endsWith('/resources/instances'));
    const body = req.request.body as Record<string, unknown>;
    expect(body['resourceSchemaId']).toBe(SCHEMA_ID);
    expect(Object.keys(body)).not.toContain(LEGACY_SCHEMA_REFERENCE);
    req.flush({ id: '33333333-3333-7333-8333-333333333333' });
  });
});
