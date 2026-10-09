using System;
using System.DirectoryServices.AccountManagement;
using ActiveDirectoryService.Connections;

namespace ActiveDirectoryService.Management;

/// <summary>
/// Manages Active Directory groups and their membership: creation, lookup, and
/// adding/removing user members. Operations run against a secured context produced
/// by the configured <see cref="IDirectoryConnectionProvider"/>.
/// </summary>
/// <remarks>
/// <c>System.DirectoryServices.AccountManagement</c> is largely synchronous, so
/// these methods expose synchronous signatures rather than faking asynchrony.
/// </remarks>
public sealed class GroupManager
{
    private readonly IDirectoryConnectionProvider _connectionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupManager"/> class.
    /// </summary>
    /// <param name="connectionProvider">The secured connection provider. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionProvider"/> is null.</exception>
    public GroupManager(IDirectoryConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
    }

    /// <summary>
    /// Determines whether a group exists with the specified SAM account name.
    /// </summary>
    /// <param name="groupName">The group's SAM account name.</param>
    /// <returns><see langword="true"/> when the group exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="groupName"/> is null or whitespace.</exception>
    public bool Exists(string groupName)
    {
        ValidateGroupName(groupName);

        using PrincipalContext context = _connectionProvider.CreateContext();
        using GroupPrincipal? group = GroupPrincipal.FindByIdentity(context, IdentityType.SamAccountName, groupName);
        return group is not null;
    }

    /// <summary>
    /// Creates a new security group.
    /// </summary>
    /// <param name="groupName">The group's SAM account name.</param>
    /// <param name="description">An optional description for the group.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="groupName"/> is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// client.Groups.Create("Engineering", "Engineering staff");
    /// </code>
    /// </example>
    public void Create(string groupName, string? description = null)
    {
        ValidateGroupName(groupName);

        using PrincipalContext context = _connectionProvider.CreateContext();
        using var group = new GroupPrincipal(context, groupName)
        {
            Description = description,
        };

        group.Save();
    }

    /// <summary>
    /// Adds a user to a group. The operation is idempotent: if the user is already
    /// a member, no change is made.
    /// </summary>
    /// <param name="groupName">The group's SAM account name.</param>
    /// <param name="userSamAccountName">The user's SAM account name.</param>
    /// <returns><see langword="true"/> when the membership was added; <see langword="false"/> when already present.</returns>
    /// <exception cref="ArgumentException">Thrown when a required argument is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the group or user cannot be found.</exception>
    public bool AddMember(string groupName, string userSamAccountName)
    {
        ValidateGroupName(groupName);
        ValidateUserName(userSamAccountName);

        using PrincipalContext context = _connectionProvider.CreateContext();
        using GroupPrincipal group = FindRequiredGroup(context, groupName);
        using UserPrincipal user = FindRequiredUser(context, userSamAccountName);

        if (group.Members.Contains(user))
        {
            return false;
        }

        group.Members.Add(user);
        group.Save();
        return true;
    }

    /// <summary>
    /// Removes a user from a group. The operation is idempotent: if the user is not
    /// a member, no change is made.
    /// </summary>
    /// <param name="groupName">The group's SAM account name.</param>
    /// <param name="userSamAccountName">The user's SAM account name.</param>
    /// <returns><see langword="true"/> when the membership was removed; <see langword="false"/> when not present.</returns>
    /// <exception cref="ArgumentException">Thrown when a required argument is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the group or user cannot be found.</exception>
    public bool RemoveMember(string groupName, string userSamAccountName)
    {
        ValidateGroupName(groupName);
        ValidateUserName(userSamAccountName);

        using PrincipalContext context = _connectionProvider.CreateContext();
        using GroupPrincipal group = FindRequiredGroup(context, groupName);
        using UserPrincipal user = FindRequiredUser(context, userSamAccountName);

        if (!group.Members.Contains(user))
        {
            return false;
        }

        group.Members.Remove(user);
        group.Save();
        return true;
    }

    private static GroupPrincipal FindRequiredGroup(PrincipalContext context, string groupName)
    {
        GroupPrincipal? group = GroupPrincipal.FindByIdentity(context, IdentityType.SamAccountName, groupName);
        return group ?? throw new InvalidOperationException($"Group '{groupName}' was not found.");
    }

    private static UserPrincipal FindRequiredUser(PrincipalContext context, string userSamAccountName)
    {
        UserPrincipal? user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, userSamAccountName);
        return user ?? throw new InvalidOperationException($"User '{userSamAccountName}' was not found.");
    }

    private static void ValidateGroupName(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new ArgumentException("A group name is required.", nameof(groupName));
        }
    }

    private static void ValidateUserName(string userSamAccountName)
    {
        if (string.IsNullOrWhiteSpace(userSamAccountName))
        {
            throw new ArgumentException("A user SAM account name is required.", nameof(userSamAccountName));
        }
    }
}
