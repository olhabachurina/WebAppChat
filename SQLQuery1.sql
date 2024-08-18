CREATE VIEW ChatMessageView AS
SELECT
    cm.Id AS MessageId,
    cm.Content,
    cm.Timestamp,
    u.UserName,
    u.Id AS UserId
FROM
    ChatMessages cm
JOIN
    AspNetUsers u ON cm.UserId = u.Id;