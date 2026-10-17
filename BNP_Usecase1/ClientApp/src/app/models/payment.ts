export type PnlStatus = 'valid' | 'invalid';

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
