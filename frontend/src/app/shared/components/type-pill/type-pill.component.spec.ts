import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { describe, it, expect, beforeEach } from 'vitest';
import { TypePillComponent, FieldType } from './type-pill.component';

describe('TypePillComponent', () => {
  let component: TypePillComponent;
  let fixture: ComponentFixture<TypePillComponent>;

  function create(value: FieldType, readonly = false): void {
    fixture = TestBed.createComponent(TypePillComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('readonly', readonly);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TypePillComponent],
    }).compileComponents();
  });

  // (a) Renders the correct label per value

  it('renders label "Text" for value "text"', () => {
    create('text');
    const label = fixture.debugElement.query(By.css('.type-pill__label'));
    expect(label.nativeElement.textContent.trim()).toBe('Text');
  });

  it('renders label "Number" for value "number"', () => {
    create('number');
    const label = fixture.debugElement.query(By.css('.type-pill__label'));
    expect(label.nativeElement.textContent.trim()).toBe('Number');
  });

  it('renders label "Yes / No" for value "boolean"', () => {
    create('boolean');
    const label = fixture.debugElement.query(By.css('.type-pill__label'));
    expect(label.nativeElement.textContent.trim()).toBe('Yes / No');
  });

  it('applies the correct modifier class for "text"', () => {
    create('text');
    const pill = fixture.debugElement.query(By.css('.type-pill--text'));
    expect(pill).not.toBeNull();
  });

  it('applies the correct modifier class for "number"', () => {
    create('number');
    const pill = fixture.debugElement.query(By.css('.type-pill--number'));
    expect(pill).not.toBeNull();
  });

  it('applies the correct modifier class for "boolean"', () => {
    create('boolean');
    const pill = fixture.debugElement.query(By.css('.type-pill--boolean'));
    expect(pill).not.toBeNull();
  });

  // (b) Click emits the next value (cycle behaviour)

  it('emits "number" when clicking a "text" pill', () => {
    create('text');
    const emitted: FieldType[] = [];
    component.valueChange.subscribe((v: FieldType) => emitted.push(v));
    fixture.debugElement.query(By.css('button')).nativeElement.click();
    expect(emitted).toEqual(['number']);
  });

  it('emits "boolean" when clicking a "number" pill', () => {
    create('number');
    const emitted: FieldType[] = [];
    component.valueChange.subscribe((v: FieldType) => emitted.push(v));
    fixture.debugElement.query(By.css('button')).nativeElement.click();
    expect(emitted).toEqual(['boolean']);
  });

  it('emits "text" when clicking a "boolean" pill (wraps around)', () => {
    create('boolean');
    const emitted: FieldType[] = [];
    component.valueChange.subscribe((v: FieldType) => emitted.push(v));
    fixture.debugElement.query(By.css('button')).nativeElement.click();
    expect(emitted).toEqual(['text']);
  });

  // (c) readonly=true renders a <span> and does not emit on click

  it('renders a <span> when readonly is true', () => {
    create('text', true);
    const span = fixture.debugElement.query(By.css('span.type-pill'));
    const button = fixture.debugElement.query(By.css('button'));
    expect(span).not.toBeNull();
    expect(button).toBeNull();
  });

  it('does not emit when readonly is true and onCycle is called', () => {
    create('text', true);
    const emitted: FieldType[] = [];
    component.valueChange.subscribe((v: FieldType) => emitted.push(v));
    component.onCycle();
    expect(emitted).toEqual([]);
  });

  // (d) Tab order: focusable when not readonly

  it('button is focusable (no tabindex=-1) when not readonly', () => {
    create('text');
    const button: HTMLButtonElement = fixture.debugElement.query(By.css('button')).nativeElement;
    expect(button.tabIndex).not.toBe(-1);
  });

  it('span is not a focusable element when readonly', () => {
    create('text', true);
    const span: HTMLElement = fixture.debugElement.query(By.css('span.type-pill')).nativeElement;
    // span has no tabIndex by default (returns -1 when unset, which means not in tab order)
    expect(span.getAttribute('tabindex')).toBeNull();
  });
});
