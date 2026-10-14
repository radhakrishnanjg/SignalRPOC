export type PnlStatus = 'valid' | 'invalid';

// Body item for POST api/payment/realtime (the server sets sourceType and status).
export interface NewPayment {
  sourceSystem: string;
  accountNumber: number;
  pnLAmount: number;
}

export interface Payment {
  fileName: string;
  sourceType: string;
  sourceSystem: string;
  accountNumber: number;
  pnLAmount: number;
  paymentStatus: string;
  createdDate: Date;
}
