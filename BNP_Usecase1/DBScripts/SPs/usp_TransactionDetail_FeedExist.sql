IF OBJECT_ID(N'Payment.usp_TransactionDetail_FeedExist', N'P') IS NOT NULL
BEGIN
	drop proc Payment.usp_TransactionDetail_FeedExist
END
go

-- Returns 1 when @FileName is NOT yet in Payment.TransactionDetails (feed can be loaded), 0 when it already exists.
Create proc Payment.usp_TransactionDetail_FeedExist
@FileName varchar(400)
As
BEGIN

	SET NOCOUNT ON;

	SELECT CASE WHEN EXISTS (SELECT 1 FROM Payment.TransactionDetails WHERE FileName = @FileName)
				THEN 0 ELSE 1 END AS FeedNotExist;

	SET NOCOUNT OFF;

END
go
