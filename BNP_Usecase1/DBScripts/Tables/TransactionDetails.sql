IF EXISTS(SELECT 1 FROM sys.objects where OBJECT_ID=OBJECT_ID('Payment.TransactionDetails') and type   = (N'U'))
begin
	Drop table Payment.TransactionDetails 
end 
go
IF NOT EXISTS(SELECT 1 FROM sys.objects where OBJECT_ID=OBJECT_ID('Payment.TransactionDetails') and type   = (N'U'))
begin
	Create table Payment.TransactionDetails(
	TransactionId int identity (1,1) not null,
	MessageId uniqueidentifier null,
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
-- Unique when present: the same Kafka message can never be stored twice (file/UI rows keep MessageId NULL).
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name = N'UX_TransactionDetails_MessageId' AND object_id = OBJECT_ID('Payment.TransactionDetails'))
begin
	CREATE UNIQUE NONCLUSTERED INDEX UX_TransactionDetails_MessageId
	ON Payment.TransactionDetails(MessageId) WHERE MessageId IS NOT NULL
end
go
 