import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { By } from '@angular/platform-browser';
import { MembersPanelComponent } from './members-panel.component';
import { ThingsBooksyModulesManagementGroupsCoreFeaturesGetGroupMembersGroupMemberDto } from '../../../api/data-contracts';

const OWNER_MEMBER: ThingsBooksyModulesManagementGroupsCoreFeaturesGetGroupMembersGroupMemberDto =
  {
    memberId: 'member-1',
    userId: 'user-1',
    email: 'owner@example.com',
    joinedAt: '2024-01-01T00:00:00Z',
    isOwner: true,
  };

const REGULAR_MEMBER: ThingsBooksyModulesManagementGroupsCoreFeaturesGetGroupMembersGroupMemberDto =
  {
    memberId: 'member-2',
    userId: 'user-2',
    email: 'member@example.com',
    joinedAt: '2024-01-02T00:00:00Z',
    isOwner: false,
  };

describe('MembersPanelComponent', () => {
  let component: MembersPanelComponent;
  let fixture: ComponentFixture<MembersPanelComponent>;

  beforeEach(async () => {
    // IntersectionObserver is not available in jsdom — provide a no-op mock
    const mockIntersectionObserver = vi.fn().mockImplementation(function () {
      return { observe: vi.fn(), unobserve: vi.fn(), disconnect: vi.fn() };
    });
    vi.stubGlobal('IntersectionObserver', mockIntersectionObserver);

    await TestBed.configureTestingModule({
      imports: [MembersPanelComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(MembersPanelComponent);
    component = fixture.componentInstance;

    fixture.componentRef.setInput('members', [OWNER_MEMBER, REGULAR_MEMBER]);
    fixture.componentRef.setInput('nextCursor', null);
    fixture.componentRef.setInput('loadingMore', false);

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('renders a list item for each member', () => {
    const rows = fixture.debugElement.queryAll(By.css('.members-panel__row'));
    expect(rows.length).toBe(2);
  });

  it('renders the email for each member', () => {
    const emails = fixture.debugElement.queryAll(
      By.css('.members-panel__email'),
    );
    expect(emails[0].nativeElement.textContent.trim()).toBe('owner@example.com');
    expect(emails[1].nativeElement.textContent.trim()).toBe(
      'member@example.com',
    );
  });

  it('renders "Admin" badge for the owner row', () => {
    const rows = fixture.debugElement.queryAll(By.css('.members-panel__row'));
    const adminBadge = rows[0].query(By.css('.members-panel__badge--admin'));
    expect(adminBadge).toBeTruthy();
    expect(adminBadge.nativeElement.textContent.trim()).toBe('Admin');
  });

  it('renders "Member" badge for non-owner row', () => {
    const rows = fixture.debugElement.queryAll(By.css('.members-panel__row'));
    const memberBadge = rows[1].query(
      By.css('.members-panel__badge--member'),
    );
    expect(memberBadge).toBeTruthy();
    expect(memberBadge.nativeElement.textContent.trim()).toBe('Member');
  });

  it('"Add member" button is disabled', () => {
    const btn = fixture.debugElement.query(
      By.css('.members-panel__add-btn'),
    );
    expect(btn.nativeElement.disabled).toBe(true);
    expect(btn.nativeElement.getAttribute('aria-disabled')).toBe('true');
  });

  it('shows member count in the count chip', () => {
    const chip = fixture.debugElement.query(By.css('tb-count-chip'));
    expect(chip).toBeTruthy();
    expect(component.memberCount()).toBe(2);
  });

  it('renders tb-avatar for each member', () => {
    const avatars = fixture.debugElement.queryAll(By.css('tb-avatar'));
    expect(avatars.length).toBe(2);
  });

  it('emits loadMore when the infinite scroll sentinel intersects', () => {
    const emittedValues: void[] = [];
    component.loadMore.subscribe(() => emittedValues.push(undefined));

    // Enable infinite scroll by providing a non-null cursor
    fixture.componentRef.setInput('nextCursor', 'some-cursor');
    fixture.componentRef.setInput('loadingMore', false);
    fixture.detectChanges();

    // Retrieve the IntersectionObserver mock instance and trigger intersection
    const MockIO = vi.mocked(IntersectionObserver);
    const callArgs = MockIO.mock.calls[MockIO.mock.calls.length - 1];
    const callbackFn = callArgs[0] as IntersectionObserverCallback;

    callbackFn(
      [{ isIntersecting: true } as IntersectionObserverEntry],
      {} as IntersectionObserver,
    );

    expect(emittedValues.length).toBeGreaterThanOrEqual(1);
  });

  it('shows empty state when members list is empty', async () => {
    fixture.componentRef.setInput('members', []);
    fixture.detectChanges();

    const empty = fixture.debugElement.query(By.css('.members-panel__empty'));
    expect(empty).toBeTruthy();
    expect(empty.nativeElement.textContent.trim()).toBe('No members yet.');
  });
});
