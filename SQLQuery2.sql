SELECT cm.Id, cm.Content, cm.Timestamp, u.UserName
FROM ChatMessages cm
JOIN AspNetUsers u ON cm.UserId = u.Id
WHERE u.UserName = 'Olga' 
AND cm.Timestamp >= '2024-08-16 00:00:00'
AND cm.Timestamp <= '2024-08-16 23:59:59'
ORDER BY cm.Timestamp DESC
OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY;