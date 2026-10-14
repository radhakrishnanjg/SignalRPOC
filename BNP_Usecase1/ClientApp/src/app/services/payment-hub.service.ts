import { Injectable } from '@angular/core';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { environment } from '../../environments/environment';
import { NewPayment, Payment } from '../models/payment';

@Injectable({ providedIn: 'root' })
export class PaymentHubService {
  private readonly connection = new HubConnectionBuilder()
    .withUrl(`${environment.apiBaseUrl}/hubs/payment`)
    .withAutomaticReconnect()
    .build();

  // The server pushes the same lists that GET valid / invalid return.
  start(onValid: (data: Payment[]) => void, onInvalid: (data: Payment[]) => void): void {
    this.connection.on('ValidUpdated', onValid);
    this.connection.on('InvalidUpdated', onInvalid);
    this.connection.start().catch(err => console.error('SignalR connection failed', err));
  }

  // Client -> server: calls PaymentHub.AddPayment. The server then pushes the updated lists back.
  addPayment(payments: NewPayment[]): Promise<unknown> {
    return this.connection.invoke('AddPayment', payments);
  }
}
