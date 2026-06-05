import {
  ChangeDetectionStrategy,
  Component,
  InputSignal,
  computed,
  input,
  signal,
} from '@angular/core';
import { AvailabilityRule } from '../../models/availability.model';

export interface HolidayEntry {
  date: string;
  name: string;
}

export interface CalendarDay {
  date: Date;
  isCurrentMonth: boolean;
  color: 'available' | 'unavailable' | 'neutral';
  hasHoliday: boolean;
  holidayName: string | null;
  isOvernightTarget: boolean;
}

const DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'] as const;
const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
] as const;

function toDateKey(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function parseLocalDate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

function doesRuleApplyToDay(rule: AvailabilityRule, day: Date): boolean {
  if (rule.ruleType === 'RECURRING') {
    const dow = day.getDay(); // 0 = Sunday
    return (rule.daysOfWeek ?? []).includes(dow);
  }

  if (rule.ruleType === 'ONE_OFF') {
    if (!rule.startDate || !rule.endDate) return false;
    const start = parseLocalDate(rule.startDate);
    const end = parseLocalDate(rule.endDate);
    const dayTime = day.getTime();
    start.setHours(0, 0, 0, 0);
    end.setHours(23, 59, 59, 999);
    return dayTime >= start.getTime() && dayTime <= end.getTime();
  }

  return false;
}

function buildCalendarDays(
  year: number,
  month: number,
  allRules: AvailabilityRule[],
  holidays: HolidayEntry[],
): CalendarDay[] {
  const holidayMap = new Map<string, string>(
    holidays.map(h => [h.date, h.name]),
  );

  const overnightTargetKeys = new Set<string>();
  for (const rule of allRules) {
    if (rule.endDayOffset === 1) {
      // Mark every day that is the overnight target of this rule.
      // For RECURRING: mark the day after each applicable day in the month.
      // For ONE_OFF: mark every day after each start day in [startDate, endDate].
      if (rule.ruleType === 'RECURRING') {
        const daysInMonth = new Date(year, month + 1, 0).getDate();
        for (let d = 1; d <= daysInMonth; d++) {
          const candidate = new Date(year, month, d);
          if ((rule.daysOfWeek ?? []).includes(candidate.getDay())) {
            const nextDay = new Date(year, month, d + 1);
            overnightTargetKeys.add(toDateKey(nextDay));
          }
        }
      } else if (
        rule.ruleType === 'ONE_OFF' &&
        rule.startDate &&
        rule.endDate
      ) {
        const start = parseLocalDate(rule.startDate);
        const end = parseLocalDate(rule.endDate);
        const cursor = new Date(start);
        while (cursor <= end) {
          const nextDay = new Date(cursor.getFullYear(), cursor.getMonth(), cursor.getDate() + 1);
          overnightTargetKeys.add(toDateKey(nextDay));
          cursor.setDate(cursor.getDate() + 1);
        }
      }
    }
  }

  const firstOfMonth = new Date(year, month, 1);
  const startOffset = firstOfMonth.getDay(); // 0 = Sunday
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const totalCells = Math.ceil((startOffset + daysInMonth) / 7) * 7;

  const days: CalendarDay[] = [];

  for (let i = 0; i < totalCells; i++) {
    const dayOffset = i - startOffset;
    const date = new Date(year, month, 1 + dayOffset);
    const isCurrentMonth = date.getMonth() === month;
    const key = toDateKey(date);

    let hasAvailable = false;
    let hasUnavailable = false;

    for (const rule of allRules) {
      if (!doesRuleApplyToDay(rule, date)) continue;
      if (rule.ruleMode === 'UNAVAILABLE') {
        hasUnavailable = true;
      } else if (rule.ruleMode === 'AVAILABLE') {
        hasAvailable = true;
      }
    }

    let color: 'available' | 'unavailable' | 'neutral';
    if (hasUnavailable) {
      color = 'unavailable';
    } else if (hasAvailable) {
      color = 'available';
    } else {
      color = 'neutral';
    }

    days.push({
      date,
      isCurrentMonth,
      color,
      hasHoliday: holidayMap.has(key),
      holidayName: holidayMap.get(key) ?? null,
      isOvernightTarget: overnightTargetKeys.has(key),
    });
  }

  return days;
}

@Component({
  selector: 'tb-availability-calendar-preview',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [],
  templateUrl: './availability-calendar-preview.component.html',
  styleUrl: './availability-calendar-preview.component.scss',
})
export class AvailabilityCalendarPreviewComponent {
  readonly rules: InputSignal<AvailabilityRule[]> = input<AvailabilityRule[]>([]);
  readonly inheritedRules: InputSignal<AvailabilityRule[] | null> = input<AvailabilityRule[] | null>(null);
  readonly holidays: InputSignal<HolidayEntry[]> = input<HolidayEntry[]>([]);

  readonly monthYear = signal<Date>((() => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1);
  })());

  readonly allRules = computed<AvailabilityRule[]>(() => {
    const own = this.rules();
    const inherited = this.inheritedRules() ?? [];
    return [...inherited, ...own];
  });

  readonly calendarDays = computed<CalendarDay[]>(() => {
    const base = this.monthYear();
    return buildCalendarDays(
      base.getFullYear(),
      base.getMonth(),
      this.allRules(),
      this.holidays(),
    );
  });

  readonly calendarWeeks = computed<CalendarDay[][]>(() => {
    const days = this.calendarDays();
    const weeks: CalendarDay[][] = [];
    for (let i = 0; i < days.length; i += 7) {
      weeks.push(days.slice(i, i + 7));
    }
    return weeks;
  });

  readonly displayMonthLabel = computed<string>(() => {
    const d = this.monthYear();
    return `${MONTH_NAMES[d.getMonth()]} ${d.getFullYear()}`;
  });

  readonly dayNames = DAY_NAMES;

  goToPreviousMonth(): void {
    this.monthYear.update(d => new Date(d.getFullYear(), d.getMonth() - 1, 1));
  }

  goToNextMonth(): void {
    this.monthYear.update(d => new Date(d.getFullYear(), d.getMonth() + 1, 1));
  }

  buildDayLabel(day: CalendarDay): string {
    const parts: string[] = [day.date.toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })];
    if (day.color !== 'neutral') {
      parts.push(day.color);
    }
    if (day.hasHoliday && day.holidayName) {
      parts.push(`holiday: ${day.holidayName}`);
    }
    if (day.isOvernightTarget) {
      parts.push('overnight target');
    }
    return parts.join(', ');
  }
}
