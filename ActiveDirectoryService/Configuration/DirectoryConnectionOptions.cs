using System;

namespace ActiveDirectoryService.Configuration;

/// <summary>
/// Specifies how the library should protect the LDAP channel when binding to a
/// domain controller. Password-related writes always require an encrypted channel.
/// </summary>
public enum SecureChannelMode
{
    /// <summary>
    /// Attempt LDAPS (LDAP over TLS, port 636) first and, if the secure TLS bind
    /// fails (for example, the DC has no server certificate), fall back to LDAP +
    /// Kerberos sign &amp; seal. The library never silently falls back to an
    /// unencrypted channel.
    /// </summary>
    LdapsWithSignSealFallback = 0,

    /// <summary>
    /// Require LDAPS (LDAP over TLS, port 636). Fails if the DC certificate cannot
    /// be validated. Requires AD CS / a DC server certificate.
    /// </summary>
    LdapsOnly = 1,

    /// <summary>
    /// Require LDAP + Kerberos sign &amp; seal (port 389 with sealing). No PKI
    /// required; strong in-domain protection. A good default when there is no
    /// certificate infrastructure.
    /// </summary>
    SignAndSealOnly = 2,
}

/// <summary>
/// Connection settings used to build a secured <c>PrincipalContext</c> against a
/// domain controller. Values should be supplied from configuration or a secret
/// store — never hardcoded.
/// </summary>
/// <remarks>
/// This type intentionally carries no credentials. The preferred identity is the
/// IIS app pool's integrated (gMSA) identity authenticating with Kerberos.
/// </remarks>
/// <example>
/// <code>
/// var options = new DirectoryConnectionOptions
/// {
///     DomainName = "corp.contoso.com",
///     ContainerDistinguishedName = "OU=Managed,DC=corp,DC=contoso,DC=com",
///     SecureChannelMode = SecureChannelMode.LdapsWithSignSealFallback,
/// };
/// </code>
/// </example>
public sealed class DirectoryConnectionOptions
{
    /// <summary>
    /// Gets or sets the fully qualified DNS name of the domain (for example,
    /// <c>corp.contoso.com</c>) or a specific domain controller host name.
    /// </summary>
    public string DomainName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the distinguished name of the container or organizational unit
    /// (OU) that scopes operations (for example,
    /// <c>OU=Managed,DC=corp,DC=contoso,DC=com</c>). Optional; when not set the
    /// domain default naming context is used.
    /// </summary>
    public string? ContainerDistinguishedName { get; set; }

    /// <summary>
    /// Gets or sets how the LDAP channel is protected. Defaults to attempting
    /// LDAPS first and falling back to Kerberos sign &amp; seal.
    /// </summary>
    public SecureChannelMode SecureChannelMode { get; set; } = SecureChannelMode.LdapsWithSignSealFallback;

    /// <summary>
    /// Validates that the required options are present.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="DomainName"/> is null, empty, or whitespace.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DomainName))
        {
            throw new ArgumentException("A domain name must be supplied.", nameof(DomainName));
        }
    }
}
