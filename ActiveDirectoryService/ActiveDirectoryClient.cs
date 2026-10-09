using System;
using ActiveDirectoryService.Configuration;
using ActiveDirectoryService.Connections;
using ActiveDirectoryService.Management;

namespace ActiveDirectoryService;

/// <summary>
/// The single entry point for managing the on-premises Active Directory side of the
/// tenant. Exposes <see cref="Users"/> and <see cref="Groups"/> managers, mirroring
/// the GoogleService facade for consistency across the two libraries.
/// </summary>
/// <example>
/// <code>
/// var options = new DirectoryConnectionOptions
/// {
///     DomainName = "corp.contoso.com",
///     ContainerDistinguishedName = "OU=Managed,DC=corp,DC=contoso,DC=com",
/// };
///
/// var client = new ActiveDirectoryClient(options);
/// if (!client.Users.Exists("jdoe"))
/// {
///     client.Users.Create("jdoe", "jdoe@corp.contoso.com", "Jane Doe", initialPassword);
/// }
///
/// client.Groups.AddMember("Engineering", "jdoe");
/// </code>
/// </example>
public sealed class ActiveDirectoryClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveDirectoryClient"/> class
    /// using the integrated (gMSA) identity and the supplied connection options.
    /// </summary>
    /// <param name="options">The connection settings. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public ActiveDirectoryClient(DirectoryConnectionOptions options)
        : this(new IntegratedConnectionProvider(options ?? throw new ArgumentNullException(nameof(options))))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveDirectoryClient"/> class
    /// using a custom connection provider. Use this overload to supply an alternate
    /// connection strategy (for example, an explicit service-account bind) or a
    /// mock in unit tests.
    /// </summary>
    /// <param name="connectionProvider">The secured connection provider. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionProvider"/> is null.</exception>
    public ActiveDirectoryClient(IDirectoryConnectionProvider connectionProvider)
    {
        if (connectionProvider is null)
        {
            throw new ArgumentNullException(nameof(connectionProvider));
        }

        Users = new UserManager(connectionProvider);
        Groups = new GroupManager(connectionProvider);
    }

    /// <summary>
    /// Gets the manager for user account operations.
    /// </summary>
    public UserManager Users { get; }

    /// <summary>
    /// Gets the manager for group and membership operations.
    /// </summary>
    public GroupManager Groups { get; }
}
