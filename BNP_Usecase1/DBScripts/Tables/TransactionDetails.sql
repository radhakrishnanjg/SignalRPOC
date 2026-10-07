IF EXISTS(SELECT 1 FROM sys.objects where OBJECT_ID=OBJECT_ID('Payment.TransactionDetails') and type   = (N'U'))
begin
	Drop table Payment.TransactionDetails 
end 
go
IF NOT EXISTS(SELECT 1 FROM sys.objects where OBJECT_ID=OBJECT_ID('Payment.TransactionDetails') and type   = (N'U'))
begin
	Create table Payment.TransactionDetails(
	TransactionId int identity (1,1) not null,
	FileName varchar(400) not null,
	SourceType varchar(10),
	SourceSystem varchar(100),
	AccountNumber int,
	PnLAmount int,
	PaymentStatus varchar(10), 
	CreatedBy int null,
	CreatedDate datetime)
end 
go
 