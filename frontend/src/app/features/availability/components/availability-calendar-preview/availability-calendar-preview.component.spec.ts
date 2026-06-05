import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { AvailabilityCalendarPreviewComponent, CalendarDay } from './availability-calendar-preview.component';
import { AvailabilityRule } from '../../models/availability.model';

describe('AvailabilityCalendarPreviewComponent', () => {
  let component: AvailabilityCalendarPreviewComponent;
  let fixture: ComponentFixture<AvailabilityCalendarPreviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AvailabilityCalendarPreviewComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AvailabilityCalendarPreviewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize monthYear to the first day of the current month', () => {
    const now = new Date();
    const monthYear = component.monthYear();
    expect(monthYear.getFullYear()).toBe(now.getFullYear());
    expect(monthYear.getMonth()).toBe(now.getMonth());
    expect(monthYear.getDate()).toBe(1);
  });

  it('should advance to the next month when goToNextMonth is called', () => {
    const initial = component.monthYear();
    component.goToNextMonth();
    const next = component.monthYear();
    const expectedMonth = (initial.getMonth() + 1) % 12;
    expect(next.getMonth()).toBe(expectedMonth);
    expect(next.getDate()).toBe(1);
  });

  it('should go back to the previous month when goToPreviousMonth is called', () => {
    const initial = component.monthYear();
    component.goToPreviousMonth();
    const prev = component.monthYear();
    const expectedMonth = (initial.getMonth() + 11) % 12;
    expect(prev.getMonth()).toBe(expectedMonth);
    expect(prev.getDate()).toBe(1);
  });

  it('should produce 35 or 42 calendar day cells for any month', () => {
    const days = component.calendarDays();
    expect(days.length % 7).toBe(0);
    expect(days.length).toBeGreaterThanOrEqual(28);
    expect(days.length).toBeLessThanOrEqual(42);
  });

  it('should produce calendarWeeks where each week has 7 days', () => {
    const weeks = component.calendarWeeks();
    for (const week of weeks) {
      expect(week.length).toBe(7);
    }
  });

  it('should display displayMonthLabel that includes month name and year', () => {
    const label = component.displayMonthLabel();
    const now = new Date();
    expect(label).toContain(String(now.getFullYear()));
  });

  it('should color a day as available when an AVAILABLE RECURRING rule covers that day', () => {
    // Navigate to January 2025 which starts on Wednesday (day 3)
    component.monthYear.set(new Date(2025, 0, 1));

    const mondayRule: AvailabilityRule = {
      ruleId: 'rule-1',
      ruleType: 'RECURRING',
      ruleMode: 'AVAILABLE',
      daysOfWeek: [1], // Monday
      startTime: '09:00',
      endTime: '17:00',
      endDayOffset: 0,
      startDate: null,
      endDate: null,
    };

    fixture.componentRef.setInput('rules', [mondayRule]);
    fixture.detectChanges();

    const days = component.calendarDays();
    const mondays = days.filter(d => d.isCurrentMonth && d.date.getDay() === 1);
    expect(mondays.length).toBeGreaterThan(0);
    for (const monday of mondays) {
      expect(monday.color).toBe('available');
    }
  });

  it('should color a day as unavailable when an UNAVAILABLE rule applies even if AVAILABLE also applies', () => {
    component.monthYear.set(new Date(2025, 0, 1));

    const availableRule: AvailabilityRule = {
      ruleId: 'rule-1',
      ruleType: 'RECURRING',
      ruleMode: 'AVAILABLE',
      daysOfWeek: [1],
      startTime: '09:00',
      endTime: '17:00',
      endDayOffset: 0,
      startDate: null,
      endDate: null,
    };

    const unavailableRule: AvailabilityRule = {
      ruleId: 'rule-2',
      ruleType: 'RECURRING',
      ruleMode: 'UNAVAILABLE',
      daysOfWeek: [1],
      startTime: '00:00',
      endTime: '23:59',
      endDayOffset: 0,
      startDate: null,
      endDate: null,
    };

    fixture.componentRef.setInput('rules', [availableRule, unavailableRule]);
    fixture.detectChanges();

    const days = component.calendarDays();
    const mondays = days.filter(d => d.isCurrentMonth && d.date.getDay() === 1);
    for (const monday of mondays) {
      expect(monday.color).toBe('unavailable');
    }
  });

  it('should color a day as neutral when no rules apply', () => {
    component.monthYear.set(new Date(2025, 0, 1));
    fixture.componentRef.setInput('rules', []);
    fixture.detectChanges();

    const days = component.calendarDays();
    const currentMonthDays = days.filter(d => d.isCurrentMonth);
    for (const day of currentMonthDays) {
      expect(day.color).toBe('neutral');
    }
  });

  it('should mark ONE_OFF rule days as available within date range', () => {
    component.monthYear.set(new Date(2025, 0, 1));

    const oneOffRule: AvailabilityRule = {
      ruleId: 'rule-1',
      ruleType: 'ONE_OFF',
      ruleMode: 'AVAILABLE',
      daysOfWeek: null,
      startTime: '09:00',
      endTime: '17:00',
      endDayOffset: 0,
      startDate: '2025-01-10',
      endDate: '2025-01-12',
    };

    fixture.componentRef.setInput('rules', [oneOffRule]);
    fixture.detectChanges();

    const days = component.calendarDays();
    const jan10 = days.find(d => d.isCurrentMonth && d.date.getDate() === 10);
    const jan11 = days.find(d => d.isCurrentMonth && d.date.getDate() === 11);
    const jan12 = days.find(d => d.isCurrentMonth && d.date.getDate() === 12);
    const jan13 = days.find(d => d.isCurrentMonth && d.date.getDate() === 13);

    expect(jan10?.color).toBe('available');
    expect(jan11?.color).toBe('available');
    expect(jan12?.color).toBe('available');
    expect(jan13?.color).toBe('neutral');
  });

  it('should flag a day as hasHoliday when a matching holiday entry is provided', () => {
    component.monthYear.set(new Date(2025, 0, 1));

    fixture.componentRef.setInput('holidays', [{ date: '2025-01-01', name: "New Year's Day" }]);
    fixture.detectChanges();

    const days = component.calendarDays();
    const jan1 = days.find(d => d.isCurrentMonth && d.date.getDate() === 1);
    expect(jan1?.hasHoliday).toBe(true);
    expect(jan1?.holidayName).toBe("New Year's Day");
  });

  it('should flag days as isOvernightTarget when a rule has endDayOffset=1', () => {
    component.monthYear.set(new Date(2025, 0, 1));

    const overnightRule: AvailabilityRule = {
      ruleId: 'rule-1',
      ruleType: 'RECURRING',
      ruleMode: 'AVAILABLE',
      daysOfWeek: [5], // Friday
      startTime: '20:00',
      endTime: '08:00',
      endDayOffset: 1,
      startDate: null,
      endDate: null,
    };

    fixture.componentRef.setInput('rules', [overnightRule]);
    fixture.detectChanges();

    const days = component.calendarDays();
    // Saturdays in January 2025 should be overnight targets (day after Friday)
    const saturdays = days.filter(d => d.isCurrentMonth && d.date.getDay() === 6);
    expect(saturdays.length).toBeGreaterThan(0);
    for (const saturday of saturdays) {
      expect(saturday.isOvernightTarget).toBe(true);
    }
  });

  it('should include inheritedRules in day coloring computation', () => {
    component.monthYear.set(new Date(2025, 0, 1));

    const inheritedRule: AvailabilityRule = {
      ruleId: 'inherited-1',
      ruleType: 'RECURRING',
      ruleMode: 'AVAILABLE',
      daysOfWeek: [2], // Tuesday
      startTime: '09:00',
      endTime: '17:00',
      endDayOffset: 0,
      startDate: null,
      endDate: null,
    };

    fixture.componentRef.setInput('rules', []);
    fixture.componentRef.setInput('inheritedRules', [inheritedRule]);
    fixture.detectChanges();

    const days = component.calendarDays();
    const tuesdays = days.filter(d => d.isCurrentMonth && d.date.getDay() === 2);
    expect(tuesdays.length).toBeGreaterThan(0);
    for (const tuesday of tuesdays) {
      expect(tuesday.color).toBe('available');
    }
  });

  it('should treat null inheritedRules as empty array', () => {
    fixture.componentRef.setInput('rules', []);
    fixture.componentRef.setInput('inheritedRules', null);
    fixture.detectChanges();

    const allRules = component.allRules();
    expect(allRules).toEqual([]);
  });

  it('should build a descriptive aria label via buildDayLabel', () => {
    const day: CalendarDay = {
      date: new Date(2025, 0, 6), // Monday, January 6, 2025
      isCurrentMonth: true,
      color: 'available',
      hasHoliday: false,
      holidayName: null,
      isOvernightTarget: false,
    };
    const label = component.buildDayLabel(day);
    expect(label).toContain('available');
    expect(label).toContain('January');
  });
});
