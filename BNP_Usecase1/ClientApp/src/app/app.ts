import { Component, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Payment, PnlStatus } from './models/payment';
import { PaymentHubService } from './services/payment-hub.service';
import { PaymentService } from './services/payment.service';

@Component({
  selector: 'app-root',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './app.html'
})
export class App implements OnInit {
  private readonly paymentService = inject(PaymentService);
  private readonly hub = inject(PaymentHubService);

  activeTab: PnlStatus = 'valid';
  records: Payment[] = [];
  loading = false;
  error = '';

  showAdd = false;
  saving = false;
  addError = '';
  successMessage = '';
  private successTimer?: ReturnType<typeof setTimeout>;

  // Add Payment form (reactive). Rules: SourceSystem varchar(100); AccountNumber digits only, max 15;
  // PnLAmount any integer (negative, zero or positive).
  private readonly fb = inject(FormBuilder);
  paymentForm = this.fb.nonNullable.group({
    sourceSystem: ['', [Validators.required, Validators.maxLength(100)]],
    accountNumber: ['', [Validators.required, Validators.pattern(/^\d{1,15}$/)]],
    pnLAmount: ['', [Validators.required, Validators.pattern(/^-?\d+$/)]]
  });

  formErrors: Record<string, string> = { sourceSystem: '', accountNumber: '', pnLAmount: '' };

  // Messages shown for each validation error of each field.
  validationMessages: Record<string, Record<string, string>> = {
    sourceSystem: {
      required: 'Source system is required.',
      maxlength: 'Source system must be 100 characters or fewer.'
    },
    accountNumber: {
      required: 'Account number is required.',
      pattern: 'Account number must contain digits only, up to 15 digits.'
    },
    pnLAmount: {
      required: 'PnL amount is required.',
      pattern: 'PnL amount must be a whole number (negative, 0 or positive).'
    }
  };

  logValidationErrors(group: FormGroup = this.paymentForm): void {
    Object.keys(group.controls).forEach(key => {
      const control = group.get(key);
      // A value made only of spaces counts as empty.
      if (control && typeof control.value === 'string' && control.value.length > 0 && !control.value.trim().length) {
        control.setValue('', { emitEvent: false });
      }
      this.formErrors[key] = '';
      if (control && !control.valid && (control.touched || control.dirty)) {
        const messages = this.validationMessages[key];
        for (const errorKey in control.errors) {
          this.formErrors[key] += (messages?.[errorKey] ?? '') + ' ';
        }
        this.formErrors[key] = this.formErrors[key].trim();
      }
      if (control instanceof FormGroup) {
        this.logValidationErrors(control);
      }
    });
  }

  ngOnInit(): void {
    this.paymentForm.valueChanges.subscribe(() => this.logValidationErrors());
    this.load();
    // Live updates: replace the table when the server pushes data for the tab being viewed.
    this.hub.start(
      data => { if (this.activeTab === 'valid') this.records = data; },
      data => { if (this.activeTab === 'invalid') this.records = data; });
  }

  openAdd(): void {
    this.paymentForm.reset();
    this.logValidationErrors();
    this.addError = '';
    this.showAdd = true;
  }

  closeAdd(): void {
    this.showAdd = false;
  }

  submitAdd(): void {
    if (this.paymentForm.invalid) {
      // Show every error, including fields the user never touched.
      this.paymentForm.markAllAsTouched();
      this.logValidationErrors();
      return;
    }
    const { sourceSystem, accountNumber, pnLAmount } = this.paymentForm.getRawValue();
    this.saving = true;
    this.addError = '';
    // Sent over SignalR; no manual reload because the server pushes the updated lists back.
    this.hub.addPayment([{
      sourceSystem: sourceSystem.trim(),
      accountNumber: Number(accountNumber),
      pnLAmount: Number(pnLAmount)
    }])
      .then(() => {
        this.saving = false;
        this.showAdd = false;
        this.showSuccess(Number(pnLAmount) === 0
          ? 'Payment saved successfully. An amount of 0 is stored as Invalid, so it is on the Invalid tab.'
          : 'Payment saved successfully.');
      })
      .catch(() => { this.saving = false; this.addError = 'Unable to save the record.'; });
  }

  // Shown above the grid; hides itself after a few seconds.
  showSuccess(message: string): void {
    clearTimeout(this.successTimer);
    this.successMessage = message;
    this.successTimer = setTimeout(() => this.successMessage = '', 5000);
  }

  dismissSuccess(): void {
    clearTimeout(this.successTimer);
    this.successMessage = '';
  }

  select(tab: PnlStatus): void {
    this.activeTab = tab;
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';
    this.paymentService.getPnl(this.activeTab).subscribe({
      next: data => { this.records = data; this.loading = false; },
      error: () => { this.records = []; this.error = 'Unable to load PnL data.'; this.loading = false; }
    });
  }
}
