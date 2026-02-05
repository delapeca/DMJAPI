--declare @empID as int;

--set @empID = 1;

SELECT [U_empID] as empID
      ,[U_DocDate] as DocDate
      ,[U_DocNum] as DocNum
      ,[U_DocEntry] as DocEntry
      ,[U_CardCode] as CardCode
      ,[U_CardName] as CardName
      ,[U_LineNum] as LineNum
      ,[U_ItemCode] as ItemCode
      ,[U_Dscription] as Dscription
      ,sum([U_RealQuantity]) as RealQuantity
      ,[U_ProposedQuantity] as ProposedQuantity
      ,[U_Price] as Price
      ,[U_DiscPrcnt] as DiscPrcnt
      ,[U_LineStatus] as LineStatus
      ,[U_LineObservation] as LineObservation
  FROM [dbo].[@XNTMPGOODRECEIPT]
  WHERE [U_empID]=@empID
  and U_LineStatus=0
  and U_RealQuantity > 0
  group by [U_empID],[U_DocDate],[U_DocNum],[U_DocEntry],[U_CardCode],[U_CardName],[U_LineNum],[U_ItemCode],[U_Dscription],[U_ProposedQuantity],[U_Price],[U_DiscPrcnt],[U_LineStatus],[U_LineObservation]
  order by U_CardCode,U_DocNum, U_LineNum
