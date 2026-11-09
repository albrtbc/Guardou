using System.Globalization;
using Guardou.Domain.Files;
using Microsoft.Extensions.Logging;

namespace Guardou.Application.Audit;

/// <summary>
/// Security events, written under the <see cref="Category"/> category with event ids 9000 to 9099. Never pass a password,
/// a token, a secret or a setting value to it.
/// </summary>
public sealed partial class AuditLog
{
    public const string Category = "Guardou.Audit";

    private const int MaxTextLength = 128;

    private readonly ILogger _logger;
    private readonly IAuditContext _context;

    public AuditLog(ILoggerFactory loggerFactory, IAuditContext context)
        : this(loggerFactory.CreateLogger(Category), context)
    {
    }

    internal AuditLog(ILogger logger, IAuditContext context)
    {
        _logger = logger;
        _context = context;
    }

    private string? ActorId => _context.ActorId;

    private string? ClientIp => _context.ClientIp;

    public void SignInSucceeded(string userId, string username)
    {
        username = Text(username);
        LogSignInSucceeded(userId, username, ClientIp);
    }

    public void SignInFailed(string username)
    {
        username = Text(username);
        LogSignInFailed(username, ClientIp);
    }

    public void LockoutStarted(string userId, string username)
    {
        username = Text(username);
        LogLockoutStarted(userId, username, ClientIp);
    }

    public void SignInRefusedLockedOut(string userId, string username)
    {
        username = Text(username);
        LogSignInRefusedLockedOut(userId, username, ClientIp);
    }

    public void SignedOut()
    {
        LogSignedOut(ActorId, ClientIp);
    }

    public void Registered(string userId, string username)
    {
        username = Text(username);
        LogRegistered(userId, username, ClientIp);
    }

    public void UserCreated(string userId, string username, bool isAdmin)
    {
        username = Text(username);
        LogUserCreated(ActorId, ClientIp, userId, username, isAdmin);
    }

    public void UserUpdated(string userId, bool passwordChanged)
    {
        LogUserUpdated(ActorId, ClientIp, userId, passwordChanged);
    }

    public void UserDeleted(string userId, string username)
    {
        username = Text(username);
        LogUserDeleted(ActorId, ClientIp, userId, username);
    }

    public void UserRoleChanged(string userId, bool isAdmin)
    {
        LogUserRoleChanged(ActorId, ClientIp, userId, isAdmin);
    }

    public void UserLoginRemoved(string userId, string provider)
    {
        provider = Text(provider);
        LogUserLoginRemoved(ActorId, ClientIp, userId, provider);
    }

    public void SettingsChanged(string section)
    {
        LogSettingsChanged(ActorId, ClientIp, section);
    }

    public void ShareLinkCreated(Guid shareId, Guid itemId)
    {
        string item = ItemId.Format(itemId);
        LogShareLinkCreated(ActorId, ClientIp, shareId, item);
    }

    public void ShareLinkRevoked(Guid shareId, Guid itemId)
    {
        string item = ItemId.Format(itemId);
        LogShareLinkRevoked(ActorId, ClientIp, shareId, item);
    }

    public void DirectShareAdded(Guid itemId, string recipientId)
    {
        string item = ItemId.Format(itemId);
        LogDirectShareAdded(ActorId, ClientIp, item, recipientId);
    }

    public void DirectShareRemoved(Guid itemId, string recipientId)
    {
        string item = ItemId.Format(itemId);
        LogDirectShareRemoved(ActorId, ClientIp, item, recipientId);
    }

    // Previews and creator pictures are fetched in bursts while a shared page renders or polls, so they stay at Debug.
    // The actor is null for an anonymous visitor and set when a signed-in user opens a link.
    public void SharedItemAccessed(Guid shareId, Guid itemId, ShareAccessKind action)
    {
        if (action is ShareAccessKind.Preview or ShareAccessKind.CreatorPicture)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                string item = ItemId.Format(itemId);
                LogSharedImageServed(ActorId, shareId, item, action, ClientIp);
            }
        }
        else if (_logger.IsEnabled(LogLevel.Information))
        {
            string item = ItemId.Format(itemId);
            LogSharedItemAccessed(ActorId, shareId, item, action, ClientIp);
        }
    }

    // User-controlled text is cut and stripped of control characters, line and paragraph separators and format
    // characters (zero-width and bidirectional overrides), so it cannot forge, split or visually reorder a log line.
    private static string Text(string value)
    {
        string text = value.Length > MaxTextLength ? value[..MaxTextLength] : value;
        return string.Create(text.Length, text, static (span, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                span[index] = IsUnsafe(source[index]) ? '?' : source[index];
            }
        });
    }

    private static bool IsUnsafe(char value)
    {
        return char.IsControl(value)
            || char.GetUnicodeCategory(value) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Format;
    }

    [LoggerMessage(EventId = 9001, EventName = "SignInSucceeded", Level = LogLevel.Information, Message = "Sign-in succeeded for user {UserId} ({Username}) from {ClientIp}.")]
    private partial void LogSignInSucceeded(string userId, string username, string? clientIp);

    [LoggerMessage(EventId = 9002, EventName = "SignInFailed", Level = LogLevel.Warning, Message = "Sign-in failed for username {Username} from {ClientIp}.")]
    private partial void LogSignInFailed(string username, string? clientIp);

    [LoggerMessage(EventId = 9003, EventName = "LockoutStarted", Level = LogLevel.Warning, Message = "User {UserId} ({Username}) was locked out after repeated failed sign-ins from {ClientIp}.")]
    private partial void LogLockoutStarted(string userId, string username, string? clientIp);

    [LoggerMessage(EventId = 9004, EventName = "SignInRefusedLockedOut", Level = LogLevel.Warning, Message = "Sign-in refused for user {UserId} ({Username}) from {ClientIp}: the account is locked out.")]
    private partial void LogSignInRefusedLockedOut(string userId, string username, string? clientIp);

    [LoggerMessage(EventId = 9005, EventName = "SignedOut", Level = LogLevel.Information, Message = "User {ActorId} signed out from {ClientIp}.")]
    private partial void LogSignedOut(string? actorId, string? clientIp);

    [LoggerMessage(EventId = 9006, EventName = "Registered", Level = LogLevel.Information, Message = "User {UserId} ({Username}) registered as the first administrator from {ClientIp}.")]
    private partial void LogRegistered(string userId, string username, string? clientIp);

    [LoggerMessage(EventId = 9010, EventName = "UserCreated", Level = LogLevel.Information, Message = "User {UserId} ({Username}, administrator: {IsAdmin}) was created by {ActorId} from {ClientIp}.")]
    private partial void LogUserCreated(string? actorId, string? clientIp, string userId, string username, bool isAdmin);

    [LoggerMessage(EventId = 9011, EventName = "UserUpdated", Level = LogLevel.Information, Message = "User {UserId} was updated by {ActorId} from {ClientIp} (password changed: {PasswordChanged}).")]
    private partial void LogUserUpdated(string? actorId, string? clientIp, string userId, bool passwordChanged);

    [LoggerMessage(EventId = 9012, EventName = "UserDeleted", Level = LogLevel.Information, Message = "User {UserId} ({Username}) was deleted by {ActorId} from {ClientIp}.")]
    private partial void LogUserDeleted(string? actorId, string? clientIp, string userId, string username);

    [LoggerMessage(EventId = 9013, EventName = "UserRoleChanged", Level = LogLevel.Information, Message = "User {UserId} role changed by {ActorId} from {ClientIp} (administrator: {IsAdmin}).")]
    private partial void LogUserRoleChanged(string? actorId, string? clientIp, string userId, bool isAdmin);

    [LoggerMessage(EventId = 9014, EventName = "UserLoginRemoved", Level = LogLevel.Information, Message = "The {Provider} login of user {UserId} was removed by {ActorId} from {ClientIp}.")]
    private partial void LogUserLoginRemoved(string? actorId, string? clientIp, string userId, string provider);

    [LoggerMessage(EventId = 9020, EventName = "SettingsChanged", Level = LogLevel.Information, Message = "The {Section} settings were changed by {ActorId} from {ClientIp}.")]
    private partial void LogSettingsChanged(string? actorId, string? clientIp, string section);

    [LoggerMessage(EventId = 9030, EventName = "ShareLinkCreated", Level = LogLevel.Information, Message = "Share link {ShareId} for item {ItemId} was created by {ActorId} from {ClientIp}.")]
    private partial void LogShareLinkCreated(string? actorId, string? clientIp, Guid shareId, string itemId);

    [LoggerMessage(EventId = 9031, EventName = "ShareLinkRevoked", Level = LogLevel.Information, Message = "Share link {ShareId} for item {ItemId} was revoked by {ActorId} from {ClientIp}.")]
    private partial void LogShareLinkRevoked(string? actorId, string? clientIp, Guid shareId, string itemId);

    [LoggerMessage(EventId = 9032, EventName = "DirectShareAdded", Level = LogLevel.Information, Message = "Item {ItemId} was shared with user {RecipientId} by {ActorId} from {ClientIp}.")]
    private partial void LogDirectShareAdded(string? actorId, string? clientIp, string itemId, string recipientId);

    [LoggerMessage(EventId = 9033, EventName = "DirectShareRemoved", Level = LogLevel.Information, Message = "Item {ItemId} was unshared from user {RecipientId} by {ActorId} from {ClientIp}.")]
    private partial void LogDirectShareRemoved(string? actorId, string? clientIp, string itemId, string recipientId);

    [LoggerMessage(EventId = 9040, EventName = "SharedItemAccessed", Level = LogLevel.Information, Message = "Share {ShareId} item {ItemId} accessed ({Action}) from {ClientIp}, actor {ActorId}.")]
    private partial void LogSharedItemAccessed(string? actorId, Guid shareId, string itemId, ShareAccessKind action, string? clientIp);

    [LoggerMessage(EventId = 9041, EventName = "SharedImageServed", Level = LogLevel.Debug, Message = "Share {ShareId} item {ItemId} accessed ({Action}) from {ClientIp}, actor {ActorId}.")]
    private partial void LogSharedImageServed(string? actorId, Guid shareId, string itemId, ShareAccessKind action, string? clientIp);
}
