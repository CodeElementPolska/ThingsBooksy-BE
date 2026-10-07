import { NO_ERRORS_SCHEMA, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, ParamMap, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { GroupDetailPageComponent } from './group-detail-page.component';
import { GroupContextStore } from '../group-context.store';
import { GroupsService } from '../groups.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { NotificationService } from '../../../shared/services/notification.service';

function makeParamMap(params: Record<string, string>): ParamMap {
  return convertToParamMap(params);
}

describe('GroupDetailPageComponent', () => {
  let component: GroupDetailPageComponent;
  let fixture: ComponentFixture<GroupDetailPageComponent>;

  let storeMock: Partial<GroupContextStore>;
  let groupsServiceMock: Partial<GroupsService>;
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let confirmDialogMock: Partial<ConfirmDialogService>;
  let notificationsMock: Partial<NotificationService>;
  let activatedRouteMock: Partial<ActivatedRoute>;

  const groupId = '11111111-1111-7111-8111-111111111111';

  beforeEach(async () => {
    storeMock = {
      initialLoading: signal(false),
      initialError: signal<string | null>(null),
      group: signal(null),
      schemas: signal([]),
      schemasWithCounts: signal([]),
      resources: signal({ items: [], nextCursor: null, loading: false }),
      members: signal({ items: [], nextCursor: null, loading: false }),
      isOwner: signal(false),
      loadGroup: vi.fn().mockResolvedValue(undefined),
      replaceGroup: vi.fn(),
      removeGroup: vi.fn(),
      loadMoreMembers: vi.fn().mockResolvedValue(undefined),
      loadMoreResources: vi.fn().mockResolvedValue(undefined),
    } as unknown as Partial<GroupContextStore>;

    groupsServiceMock = {
      deleteGroup: vi.fn().mockReturnValue(of(undefined)),
    };

    routerMock = {
      navigate: vi.fn().mockResolvedValue(true),
    };

    confirmDialogMock = {
      confirm: vi.fn().mockResolvedValue(false),
    };

    notificationsMock = {
      success: vi.fn().mockReturnValue('toast-id'),
      error: vi.fn().mockReturnValue('toast-id'),
    };

    activatedRouteMock = {
      paramMap: of(makeParamMap({ groupId })),
    };

    await TestBed.configureTestingModule({
      imports: [GroupDetailPageComponent],
      providers: [
        { provide: GroupContextStore, useValue: storeMock },
        { provide: GroupsService, useValue: groupsServiceMock },
        { provide: Router, useValue: routerMock },
        { provide: ConfirmDialogService, useValue: confirmDialogMock },
        { provide: NotificationService, useValue: notificationsMock },
        { provide: ActivatedRoute, useValue: activatedRouteMock },
      ],
      schemas: [NO_ERRORS_SCHEMA],
    })
      // The component declares its own GroupContextStore provider, which would shadow the mock above.
      .overrideComponent(GroupDetailPageComponent, {
        set: {
          imports: [],
          schemas: [NO_ERRORS_SCHEMA],
          providers: [{ provide: GroupContextStore, useValue: storeMock }],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(GroupDetailPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should read groupId from route paramMap and call store.loadGroup', () => {
    expect(storeMock.loadGroup).toHaveBeenCalledWith(groupId);
  });

  it('should compute groupId from paramMap', () => {
    expect(component.groupId()).toBe(groupId);
  });

  it('should render tb-animated-background', () => {
    const el = fixture.debugElement.query(By.css('tb-animated-background'));
    expect(el).toBeTruthy();
  });

  it('should render breadcrumb nav', () => {
    const nav = fixture.debugElement.query(By.css('nav[aria-label="Breadcrumb"]'));
    expect(nav).toBeTruthy();
  });

  it('should render main element with aria-labelledby', () => {
    const main = fixture.debugElement.query(By.css('main'));
    expect(main).toBeTruthy();
    expect(main.nativeElement.getAttribute('aria-labelledby')).toBe('group-heading');
  });

  it('should render skeleton when initialLoading is true', async () => {
    (storeMock.initialLoading as ReturnType<typeof signal<boolean>>).set(true);
    fixture.detectChanges();

    const skeleton = fixture.debugElement.query(By.css('.group-detail-page__skeleton'));
    expect(skeleton).toBeTruthy();

    const panels = fixture.debugElement.queryAll(By.css('.group-detail-page__panels'));
    expect(panels.length).toBe(0);
  });

  it('should render error card when initialError is set', async () => {
    (storeMock.initialError as ReturnType<typeof signal<string | null>>).set('Network error');
    fixture.detectChanges();

    const errorEl = fixture.debugElement.query(By.css('.group-detail-page__error'));
    expect(errorEl).toBeTruthy();

    const errorMsg = fixture.debugElement.query(By.css('.group-detail-page__error-message'));
    expect(errorMsg.nativeElement.textContent).toContain('Network error');
  });

  it('should render panels container when loaded', () => {
    const panels = fixture.debugElement.query(By.css('.group-detail-page__panels'));
    expect(panels).toBeTruthy();
  });

  it('should call store.loadGroup again on retry click', async () => {
    (storeMock.initialError as ReturnType<typeof signal<string | null>>).set('Error');
    fixture.detectChanges();

    const retryBtn = fixture.debugElement.query(By.css('.group-detail-page__retry-btn'));
    retryBtn.nativeElement.click();

    expect(storeMock.loadGroup).toHaveBeenCalledTimes(2);
  });

  it('onEdit() should set editOpen to true', () => {
    expect(component.editOpen()).toBe(false);
    component.onEdit();
    expect(component.editOpen()).toBe(true);
  });

  it('onEditModalClose() should set editOpen to false', () => {
    component.onEdit();
    component.onEditModalClose();
    expect(component.editOpen()).toBe(false);
  });

  it('onAddSchema() should navigate to /groups/:id/schemas/new', () => {
    component.onAddSchema();
    expect(routerMock.navigate).toHaveBeenCalledWith(['/groups', groupId, 'schemas', 'new']);
  });

  it('onSelectSchema() should navigate to /groups/:id/schemas/:schemaId', () => {
    const schemaId = 'schema-abc';
    component.onSelectSchema(schemaId);
    expect(routerMock.navigate).toHaveBeenCalledWith(['/groups', groupId, 'schemas', schemaId]);
  });

  it('onDelete() should call confirmDialog.confirm and not delete if cancelled', async () => {
    (confirmDialogMock.confirm as ReturnType<typeof vi.fn>).mockResolvedValue(false);
    await component.onDelete();
    expect(groupsServiceMock.deleteGroup).not.toHaveBeenCalled();
  });

  it('onDelete() should delete group and navigate to dashboard on confirm', async () => {
    (confirmDialogMock.confirm as ReturnType<typeof vi.fn>).mockResolvedValue(true);
    (groupsServiceMock.deleteGroup as ReturnType<typeof vi.fn>).mockReturnValue(of(undefined));

    await component.onDelete();

    expect(groupsServiceMock.deleteGroup).toHaveBeenCalledWith(groupId);
    expect(notificationsMock.success).toHaveBeenCalledWith('Group deleted');
    expect(routerMock.navigate).toHaveBeenCalledWith(['/dashboard']);
  });
});
