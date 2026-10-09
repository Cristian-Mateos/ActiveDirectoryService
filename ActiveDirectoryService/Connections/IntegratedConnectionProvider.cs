using System;
using System.DirectoryServices.AccountManagement;
using ActiveDirectoryService.Configuration;

namespace ActiveDirectoryService.Connections;

/// <summary>
/// Connects to Active Directory using the current process (integrated) identity —
/// ideally the IIS app pool's gMSA — authenticating with Kerberos. No credentials
/// are stored in code or configuration; AD rotates the gMSA password automatically.
/// </summary>
/// <remarks>
/// <para>
/// The channel is always encrypted. Depending on <see cref="DirectoryConnectionOptions.SecureChannelMode"/>
/// this provider:
/// </para>
/// <list type="bullet">
/// <item><description>attempts LDAPS first (<see cref="ContextOptions.SecureSocketLayer"/>), then</description></item>
/// <item><description>falls back to LDAP + Kerberos sign &amp; seal (<see cref="ContextOptions.Negotiate"/> | <see cref="ContextOptions.Sealing"/>).</description></item>
/// </list>
/// <para>
/// We prefer sign &amp; seal as a fallback because it needs no PKI and is strong
/// in-domain; we never silently fall back to an unencrypted channel.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var options = new DirectoryConnectionOptions { DomainName = "corp.contoso.com" };
/// IDirectoryConnectionProvider provider = new IntegratedConnectionProvider(options);
/// using PrincipalContext context = provider.CreateContext();
/// </code>
/// </example>
public sealed class IntegratedConnectionProvider : IDirectoryConnectionProvider
{
    private readonly DirectoryConnectionOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegratedConnectionProvider"/> class.
    /// </summary>
    /// <param name="options">The connection settings. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="options"/> is invalid.</exception>
    public IntegratedConnectionProvider(DirectoryConnectionOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.Validate();
        _options = options;
    }

    /// <inheritdoc />
    public PrincipalContext CreateContext()
    {
        // Sign & seal uses Negotiate (Kerberos in-domain) and encrypts the traffic.
        const ContextOptions signAndSeal = ContextOptions.Negotiate | ContextOptions.Sealing;
        const ContextOptions ldaps = ContextOptions.Negotiate | ContextOptions.SecureSocketLayer;

        return _options.SecureChannelMode switch
        {
            SecureChannelMode.LdapsOnly => BuildContext(ldaps),
            SecureChannelMode.SignAndSealOnly => BuildContext(signAndSeal),
            _ => CreateWithFallback(ldaps, signAndSeal),
        };
    }

    /// <summary>
    /// Attempts the preferred encrypted mode first and falls back to the secondary
    /// encrypted mode. The fallback is still an encrypted channel — never plaintext.
    /// </summary>
    private PrincipalContext CreateWithFallback(ContextOptions preferred, ContextOptions fallback)
    {
        try
        {
            // Why LDAPS first: it provides TLS with validated DC certificate when AD CS is present.
            return BuildContext(preferred);
        }
        catch (PrincipalServerDownException)
        {
            // No DC certificate / LDAPS unavailable: fall back to Kerberos sign & seal.
            return BuildContext(fallback);
        }
        catch (PrincipalException)
        {
            return BuildContext(fallback);
        }
    }

    /// <summary>
    /// Builds a <see cref="PrincipalContext"/> bound with the integrated identity
    /// over the specified encrypted <paramref name="contextOptions"/>.
    /// </summary>
    private PrincipalContext BuildContext(ContextOptions contextOptions)
    {
        // Passing null credentials uses the current (process/gMSA) identity with Kerberos.
        return string.IsNullOrWhiteSpace(_options.ContainerDistinguishedName)
            ? new PrincipalContext(ContextType.Domain, _options.DomainName, null, contextOptions)
            : new PrincipalContext(ContextType.Domain, _options.DomainName, _options.ContainerDistinguishedName, contextOptions);
    }
}
