import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Payment, PnlStatus } from '../models/payment';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/payment`;

  getPnl(status: PnlStatus): Observable<Payment[]> {
    return this.http.get<Payment[]>(`${this.baseUrl}/${status}`);
  }
}
