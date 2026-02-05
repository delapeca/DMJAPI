SELECT T0.[DocDate], T0.[DocNum], T3.Warehouse, T0.[CardCode], T0.[CardName], T1.[LineNum], T1.[ItemCode], T1.[Dscription], T1.[Quantity], T1.[OpenQty], T1.[Price], T1.[DiscPrcnt], T1.[LineTotal] 
FROM OPOR T0 
INNER JOIN POR1 T1 
ON T0.[DocEntry] = T1.[DocEntry]
and T0.CANCELED = 'N'
and T1.[LineStatus] ='O'
INNER JOIN OUSR T2 
ON T0.[UserSign] = T2.[USERID]
INNER JOIN OUDG T3 
ON T2.[DfltsGroup] = T3.[Code]
