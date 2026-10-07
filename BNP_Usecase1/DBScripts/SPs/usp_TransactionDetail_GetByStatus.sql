IF OBJECT_ID(N'Payment.usp_TransactionDetail_GetByStatus', N'P') IS NOT NULL
BEGIN
	drop proc Payment.usp_TransactionDetail_GetByStatus
END
go

-- Returns stored records filtered by @PaymentStatus ('Valid' / 'Invalid'); NULL returns all records.
Create proc Payment.usp_TransactionDetail_GetByStatus
@PaymentStatus varchar(10) = NULL
As
BEGIN

	SET NOCOUNT ON;

	SELECT FileName, SourceType, SourceSystem, AccountNumber, PnLAmount, PaymentStatus
	FROM Payment.TransactionDetails
	WHERE @PaymentStatus IS NULL OR PaymentStatus = @PaymentStatus
	ORDER BY TransactionId;

	SET NOCOUNT OFF;

END
go
