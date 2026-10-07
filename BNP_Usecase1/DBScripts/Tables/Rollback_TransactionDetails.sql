IF EXISTS(SELECT 1 FROM sys.objects where OBJECT_ID=OBJECT_ID('Payment.TransactionDetails') and type   = (N'U'))
begin
	Drop table Payment.TransactionDetails 
end 
go