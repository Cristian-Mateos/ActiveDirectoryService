using System;
using System.DirectoryServices.AccountManagement;
using ActiveDirectoryService.Connections;

namespace ActiveDirectoryService.Management;

/// <summary>
/// Manages Active Directory user accounts: creation, lookup, enabling/disabling,
/// and password resets. Operations run against a secured context produced by the
/// configured <see cref="IDirectoryConnectionProvider"/>.
/// </summary>
/// <remarks>
/// <c>System.DirectoryServices.AccountManagement</c> is largely synchronous, so
/// these methods expose synchronous signatures rather than faking asynchrony.
/// </remarks>
public sealed class UserManager
{
    private readonly IDirectoryConnectionProvider _connectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManager"/> class.
    /// </summary>
    /// <param name="connectionProvider">The secured connection provider. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionProvider"/> is null.</exception>
    public UserManager(IDirectoryConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
    }

    /// <summary>
    /// Determines whether a user exists with the specified SAM account name.
    /// </summary>
    /// <param name="samAccountName">The pre-Windows 2000 logon name (for example, <c>jdoe</c>).</param>
    /// <returns><see langword="true"/> when the user exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="samAccountName"/> is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// bool exists = client.Users.Exists("jdoe");
    /// </code>
    /// </example>
    public bool Exists(string samAccountName)
    {
        ValidateSamAccountName(samAccountName);

        using PrincipalContext context = _connectionProvider.CreateContext();
        using UserPrincipal? user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, samAccountName);
        return user is not null;
    }

    /// <summary>
    /// Creates a new, enabled user account with an initial password.
    /// </summary>
    /// <param name="samAccountName">The pre-Windows 2000 logon name (for example, <c>jdoe</c>).</param>
    /// <param name="userPrincipalName">The UPN (for example, <c>jdoe@corp.contoso.com</c>).</param>
    /// <param name="displayName">The display name shown in directories.</param>
    /// <param name="initialPassword">
    /// The initial password. Setting a password requires an encrypted channel, which
    /// the connection provider guarantees.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when a required argument is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// client.Users.Create("jdoe", "jdoe@corp.contoso.com", "Jane Doe", initialPassword);
    /// </code>
    /// </example>
    public void Create(string samAccountName, string userPrincipalName, string displayName, string initialPassword)
    {
        ValidateSamAccountName(samAccountName);

        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            throw new ArgumentException("A user principal name is required.", nameof(userPrincipalName));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A display name is required.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(initialPassword))
        {
            throw new ArgumentException("An initial password is required.", nameof(initialPassword));
        }

        using PrincipalContext context = _connectionProvider.CreateContext();
        using var user = new UserPrincipal(context)
        {
            SamAccountName = samAccountName,
            UserPrincipalName = userPrincipalName,
            DisplayName = displayName,
            Enabled = true,
        };

        // SetPassword travels over the encrypted channel established by the provider.
        user.SetPassword(initialPassword);
        user.Save();
    }

    /// <summary>
    /// Enables or disables the specified user account.
    /// </summary>
    /// <param name="samAccountName">The pre-Windows 2000 logon name.</param>
    /// <param name="enabled"><see langword="true"/> to enable; <see langword="false"/> to disable.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="samAccountName"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the user cannot be found.</exception>
    public void SetEnabled(string samAccountName, bool enabled)
    {
        ValidateSamAccountName(samAccountName);

        using PrincipalContext context = _connectionProvider.CreateContext();
        using UserPrincipal user = FindRequired(context, samAccountName);
        user.Enabled = enabled;
        user.Save();
    }

    /// <summary>
    /// Resets the specified user's password.
    /// </summary>
    /// <param name="samAccountName">The pre-Windows 2000 logon name.</param>
    /// <param name="newPassword">The new password. Requires the encrypted channel the provider guarantees.</param>
    /// <exception cref="ArgumentException">Thrown when a required argument is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the user cannot be found.</exception>
    public void ResetPassword(string samAccountName, string newPassword)
    {
        ValidateSamAccountName(samAccountName);

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new ArgumentException("A new password is required.", nameof(newPassword));
        }

        using PrincipalContext context = _connectionProvider.CreateContext();
        using UserPrincipal user = FindRequired(context, samAccountName);
        user.SetPassword(newPassword);
        user.Save();
    }

    private static UserPrincipal FindRequired(PrincipalContext context, string samAccountName)
    {
        UserPrincipal? user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, samAccountName);
        return user ?? throw new InvalidOperationException($"User '{samAccountName}' was not found.");
    }

    private static void ValidateSamAccountName(string samAccountName)
    {
        if (string.IsNullOrWhiteSpace(samAccountName))
        {
            throw new ArgumentException("A SAM account name is required.", nameof(samAccountName));
        }
    }
}
