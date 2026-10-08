import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ConfirmDialogComponent } from './confirm-dialog.component';
import { ConfirmDialogService } from '../../services/confirm-dialog.service';

// jsdom does not implement the <dialog> methods the modal calls
function stubDialogMethods(): void {
  HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) { this.open = true; };
  HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) { this.open = false; };
}

describe('ConfirmDialogComponent', () => {
  let component: ConfirmDialogComponent;
  let fixture: ComponentFixture<ConfirmDialogComponent>;
  let service: ConfirmDialogService;

  beforeEach(async () => {
    stubDialogMethods();
    await TestBed.configureTestingModule({
      imports: [ConfirmDialogComponent, NoopAnimationsModule],
    }).compileComponents();

    service = TestBed.inject(ConfirmDialogService);
    fixture = TestBed.createComponent(ConfirmDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should open the dialog (state non-null) after confirm() is called', () => {
    expect(service.state()).toBeNull();

    service.confirm({ title: 'Delete item', message: 'Are you sure?' });
    fixture.detectChanges();

    expect(service.state()).not.toBeNull();
    expect(service.state()?.title).toBe('Delete item');
    expect(service.state()?.message).toBe('Are you sure?');
  });

  it('should resolve the promise with true and close the dialog when Confirm is clicked', async () => {
    const promise = service.confirm({
      title: 'Delete item',
      message: 'Are you sure?',
    });
    fixture.detectChanges();

    expect(service.state()).not.toBeNull();

    component.onConfirm();
    fixture.detectChanges();

    const result = await promise;
    expect(result).toBe(true);
    expect(service.state()).toBeNull();
  });

  it('should resolve the promise with false and close the dialog when Cancel is clicked', async () => {
    const promise = service.confirm({
      title: 'Delete item',
      message: 'Are you sure?',
    });
    fixture.detectChanges();

    expect(service.state()).not.toBeNull();

    component.onCancel();
    fixture.detectChanges();

    const result = await promise;
    expect(result).toBe(false);
    expect(service.state()).toBeNull();
  });

  it('should apply the danger CSS class to the confirm button when danger flag is true', () => {
    service.confirm({
      title: 'Delete item',
      message: 'This action is permanent.',
      danger: true,
    });
    fixture.detectChanges();

    const confirmButton: HTMLButtonElement | null =
      fixture.nativeElement.querySelector('.confirm-dialog__btn--confirm');
    expect(confirmButton).not.toBeNull();
    expect(confirmButton?.classList.contains('confirm-dialog__btn--danger')).toBe(
      true,
    );
  });

  it('should NOT apply the danger CSS class when danger flag is false', () => {
    service.confirm({
      title: 'Confirm action',
      message: 'Are you sure?',
      danger: false,
    });
    fixture.detectChanges();

    const confirmButton: HTMLButtonElement | null =
      fixture.nativeElement.querySelector('.confirm-dialog__btn--confirm');
    expect(confirmButton?.classList.contains('confirm-dialog__btn--danger')).toBe(
      false,
    );
  });
});
