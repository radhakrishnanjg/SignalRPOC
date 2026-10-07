import { Component, OnInit, inject } from '@angular/core';
import { Payment, PnlStatus } from './models/payment';
import { PaymentHubService } from './services/payment-hub.service';
import { PaymentService } from './services/payment.service';

@Component({
  selector: 'app-root',
  imports: [],
  templateUrl: './app.html'
})
export class App implements OnInit {
  private readonly paymentService = inject(PaymentService);
  private readonly hub = inject(PaymentHubService);

  activeTab: PnlStatus = 'valid';
  records: Payment[] = [];
  loading = false;
  error = '';

  ngOnInit(): void {
    this.load();
    // Live updates: replace the table when the server pushes data for the tab being viewed.
    this.hub.start(
      data => { if (this.activeTab === 'valid') this.records = data; },
      data => { if (this.activeTab === 'invalid') this.records = data; });
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
