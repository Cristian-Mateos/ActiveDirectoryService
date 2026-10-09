using System.DirectoryServices.AccountManagement;

namespace ActiveDirectoryService.Connections;

/// <summary>
/// Decouples <em>how we connect and authenticate</em> to Active Directory from
/// <em>what we do</em> with it. Mirrors GoogleService's
/// <c>IWorkspaceCredentialProvider</c> so connection strategies (integrated/gMSA
/// versus an explicit service-account bind) can be swapped without touching the
/// managers.
/// </summary>
/// <remarks>
/// Implementations are responsible for producing a <see cref="PrincipalContext"/>
/// bound over an encrypted channel (LDAPS or LDAP + Kerberos sign &amp; seal). The
/// library never connects over an unprotected channel.
/// </remarks>
public interface IDirectoryConnectionProvider
{
    /// <summary>
    /// Creates a new secured <see cref="PrincipalContext"/> for directory
    /// operations. Callers own the returned context and must dispose it.
    /// </summary>
    /// <returns>
    /// A <see cref="PrincipalContext"/> bound to the configured container over an
    /// encrypted channel.
    /// </returns>
    /// <example>
    /// <code>
    /// using PrincipalContext context = provider.CreateContext();
    /// using var user = UserPrincipal.FindByIdentity(context, "jdoe");
    /// </code>
    /// </example>
    PrincipalContext CreateContext();
}
