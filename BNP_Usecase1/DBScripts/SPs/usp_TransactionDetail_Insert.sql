IF OBJECT_ID(N'Payment.usp_TransactionDetail_Insert', N'P') IS NOT NULL
BEGIN
	drop proc Payment.usp_TransactionDetail_Insert
END
go

-- @JSON supplied  : inserts the records and returns the inserted row count.
-- @JSON is NULL   : returns the stored records (all, or filtered by @PaymentStatus).
Create proc Payment.usp_TransactionDetail_Insert
@JSON varchar(max) = NULL 
As
BEGIN

	SET NOCOUNT ON;
	 

	INSERT INTO Payment.TransactionDetails
		(SourceType, SourceSystem, AccountNumber, PnLAmount, PaymentStatus, FileName, CreatedBy, CreatedDate)
	SELECT SourceType, SourceSystem, AccountNumber, PnLAmount, PaymentStatus, FileName, NULL, GETDATE()
	FROM OPENJSON(@JSON)
	WITH (
		SourceType    varchar(10)  '$.SourceType',
		SourceSystem  varchar(100) '$.SourceSystem',
		AccountNumber int          '$.AccountNumber',
		PnLAmount     int          '$.PnLAmount',
		PaymentStatus varchar(10)  '$.PaymentStatus',
		FileName      varchar(400) '$.FileName'
	);

	SELECT @@ROWCOUNT AS RowsInserted;

	SET NOCOUNT OFF;

END
go
