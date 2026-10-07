export type PnlStatus = 'valid' | 'invalid';

export interface Payment {
  fileName: string;
  sourceType: string;
  sourceSystem: string;
  accountNumber: number;
  pnLAmount: number;
  paymentStatus: string;
}
