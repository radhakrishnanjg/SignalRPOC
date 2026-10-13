--create schema Payment
--go


select * from Payment.TransactionDetails


select format(getdate(),'yyyyMMddhhmmssfff')
wait(1)
select format(getdate(),'yyyyMMddhhmmssfff')

----
-- delete Payment.TransactionDetails

--update  Payment.TransactionDetails set  PnLAmount=PnLAmount + 100 
--where 1=1
--and PnLAmount>0