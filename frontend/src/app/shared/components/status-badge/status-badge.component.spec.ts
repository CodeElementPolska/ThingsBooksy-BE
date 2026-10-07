import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { StatusBadgeComponent } from './status-badge.component';
import { By } from '@angular/platform-browser';
import { Component, signal } from '@angular/core';

@Component({
  standalone: true,
  imports: [StatusBadgeComponent],
  template: `<tb-status-badge [status]="status()" [label]="label()" />`,
})
class TestHostComponent {
  readonly status = signal<'confirmed' | 'cancelled'>('confirmed');
  readonly label = signal('');
}

describe('StatusBadgeComponent', () => {
  let fixture: ComponentFixture<TestHostComponent>;
  let host: TestHostComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHostComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TestHostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    const badge = fixture.debugElement.query(By.directive(StatusBadgeComponent));
    expect(badge).toBeTruthy();
  });

  it('should render "Confirmed" default label for confirmed status', () => {
    host.status.set('confirmed');
    fixture.detectChanges();
    const span: HTMLElement = fixture.debugElement.query(By.css('.badge')).nativeElement;
    expect(span.textContent?.trim()).toBe('Confirmed');
  });

  it('should render "Cancelled" default label for cancelled status', () => {
    host.status.set('cancelled');
    fixture.detectChanges();
    const span: HTMLElement = fixture.debugElement.query(By.css('.badge')).nativeElement;
    expect(span.textContent?.trim()).toBe('Cancelled');
  });

  it('should apply the confirmed CSS class for confirmed status', () => {
    host.status.set('confirmed');
    fixture.detectChanges();
    const span: HTMLElement = fixture.debugElement.query(By.css('.badge')).nativeElement;
    expect(span.classList).toContain('confirmed');
    expect(span.classList).not.toContain('cancelled');
  });

  it('should apply the cancelled CSS class for cancelled status', () => {
    host.status.set('cancelled');
    fixture.detectChanges();
    const span: HTMLElement = fixture.debugElement.query(By.css('.badge')).nativeElement;
    expect(span.classList).toContain('cancelled');
    expect(span.classList).not.toContain('confirmed');
  });

  it('should render the override label when label input is provided', () => {
    host.status.set('confirmed');
    host.label.set('Active');
    fixture.detectChanges();
    const span: HTMLElement = fixture.debugElement.query(By.css('.badge')).nativeElement;
    expect(span.textContent?.trim()).toBe('Active');
  });

  it('should render override label for cancelled status when label is provided', () => {
    host.status.set('cancelled');
    host.label.set('Rejected');
    fixture.detectChanges();
    const span: HTMLElement = fixture.debugElement.query(By.css('.badge')).nativeElement;
    expect(span.textContent?.trim()).toBe('Rejected');
  });
});
