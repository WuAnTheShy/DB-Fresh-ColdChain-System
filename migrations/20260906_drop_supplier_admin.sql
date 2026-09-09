
DELETE FROM Inv_Suppliers
WHERE LoginAccount = 'admin'
  AND Status IN ('Active', 'Pending', 'Disabled');

