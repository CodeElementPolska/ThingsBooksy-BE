import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideAnimations } from '@angular/platform-browser/animations';
import { Router } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { DashboardPageComponent } from './dashboard-page.component';
import { AuthService } from '../../auth/auth.service';

describe('DashboardPageComponent', () => {
  let component: DashboardPageComponent;
  let fixture: ComponentFixture<DashboardPageComponent>;
  let authServiceMock: Partial<AuthService>;
  let routerMock: { navigate: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    authServiceMock = {
      displayName: signal('Alice Smith'),
      initials: signal('AS'),
      signOut: vi.fn(() => of(undefined)),
    } as unknown as Partial<AuthService>;

    routerMock = {
      navigate: vi.fn().mockResolvedValue(true),
    };

    await TestBed.configureTestingModule({
      imports: [DashboardPageComponent],
      providers: [
        provideAnimations(),
        { provide: AuthService, useValue: authServiceMock },
        { provide: Router, useValue: routerMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render tb-animated-background', () => {
    const el = fixture.debugElement.query(By.css('tb-animated-background'));
    expect(el).toBeTruthy();
  });

  it('should render tb-dashboard-header', () => {
    const el = fixture.debugElement.query(By.css('tb-dashboard-header'));
    expect(el).toBeTruthy();
  });

  it('should render tb-dashboard-welcome-bar', () => {
    const el = fixture.debugElement.query(By.css('tb-dashboard-welcome-bar'));
    expect(el).toBeTruthy();
  });

  it('should render tb-dashboard-history-panel', () => {
    const el = fixture.debugElement.query(By.css('tb-dashboard-history-panel'));
    expect(el).toBeTruthy();
  });

  it('should render tb-dashboard-admin-panel', () => {
    const el = fixture.debugElement.query(By.css('tb-dashboard-admin-panel'));
    expect(el).toBeTruthy();
  });

  it('should render tb-create-group-modal', () => {
    const el = fixture.debugElement.query(By.css('tb-create-group-modal'));
    expect(el).toBeTruthy();
  });

  it('should have modalOpen initialised to false', () => {
    expect(component.modalOpen()).toBe(false);
  });

  it('onCreateGroup() should set modalOpen to true', () => {
    component.onCreateGroup();
    expect(component.modalOpen()).toBe(true);
  });

  it('onCloseModal() should set modalOpen to false after opening', () => {
    component.onCreateGroup();
    component.onCloseModal();
    expect(component.modalOpen()).toBe(false);
  });

  it('onLogout() should call authService.signOut and navigate to /', () => {
    component.onLogout();
    expect(authServiceMock.signOut).toHaveBeenCalledTimes(1);
    expect(routerMock.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should pass historyRows to tb-dashboard-history-panel', () => {
    const panel = fixture.debugElement.query(By.css('tb-dashboard-history-panel'));
    expect(panel.componentInstance.rows()).toBe(component.historyRows);
  });

  it('should pass ownedGroups and memberGroups to tb-dashboard-admin-panel', () => {
    const panel = fixture.debugElement.query(By.css('tb-dashboard-admin-panel'));
    expect(panel.componentInstance.memberGroups()).toBe(component.memberGroups);
    expect(panel.componentInstance.ownedGroups()).toBe(component.ownedGroups);
  });
});
