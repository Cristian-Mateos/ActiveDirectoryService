# ActiveDirectoryService

`ActiveDirectoryService` is a reusable .NET Framework 4.8.1 class library that manages
the **on-premises Active Directory** side of our tenant: creating and managing users,
groups, group membership, enabling/disabling accounts, and resetting passwords.

It is the on-prem counterpart to **GoogleService** (which manages the Google Workspace
side). The main web application — hosted on **IIS on a domain-joined server** —
references both DLLs to manage identities in both directories.

## Features

- `ActiveDirectoryClient` facade exposing `Users` and `Groups` managers.
- Pluggable connection/credential abstraction (`IDirectoryConnectionProvider`) that
  decouples *how we connect* from *what we do*.
- Integrated (gMSA) connection provider that authenticates with Kerberos and uses an
  encrypted channel — **LDAPS first, with automatic fallback to LDAP + Kerberos
  sign &amp; seal**.
- User operations: existence check, create, enable/disable, reset password.
- Group operations: existence check, create, add/remove member (idempotent).

## Requirements

- Windows with .NET Framework 4.8.1 (the AD APIs are Windows-only).
- A domain-joined host that can reach a domain controller.
- Framework references `System.DirectoryServices.AccountManagement` and
  `System.DirectoryServices.Protocols` (ship with the Windows .NET platform).

## Installation

Reference the project (or the built `ActiveDirectoryService.dll`) from the consuming
application:

```xml
<ProjectReference Include="..\ActiveDirectoryService\ActiveDirectoryService.csproj" />
```

## Security

### Identity (authentication)

- **Preferred:** run the IIS app pool under a **gMSA** (group Managed Service Account).
  There is no stored password — AD rotates it automatically — and the library connects
  using the process/integrated identity with **Kerberos**. No credentials live in code
  or config.
- **Fallback:** a dedicated least-privilege service account whose password comes from a
  secret store or environment variable — **never hardcoded**. Supply it through a custom
  `IDirectoryConnectionProvider`.

### Transport (channel encryption)

Plain LDAP on port 389 with no protection is never used. Any password-related write
requires an encrypted channel.

- **LDAPS** (LDAP over TLS, port 636) — requires a DC server certificate (AD CS). The
  server certificate is validated; it is not blindly accepted.
- **LDAP + Kerberos sign &amp; seal** (port 389 with sealing) — no PKI required, strong
  in-domain. Good default when there is no certificate infrastructure.

Configure the preference with `SecureChannelMode`:

| Mode | Behavior |
| --- | --- |
| `LdapsWithSignSealFallback` (default) | Try LDAPS, fall back to sign &amp; seal. Never unencrypted. |
| `LdapsOnly` | Require LDAPS; fail if the DC certificate is unavailable. |
| `SignAndSealOnly` | Require LDAP + Kerberos sign &amp; seal. |

### Authorization (least privilege)

The service identity must have **only** the delegated AD permissions it needs on the
specific OU(s) — never Domain Admin. Delegate the following rights on the managed OU:

- Create/delete user objects.
- Read/write user properties (`displayName`, `userPrincipalName`, `userAccountControl`).
- Reset user passwords.
- Create/delete group objects and write the `member` attribute.

## Configuration

Read all values from configuration or a secret store — never hardcode them.

```csharp
var options = new DirectoryConnectionOptions
{
	DomainName = "corp.contoso.com", // TODO: load from configuration
	ContainerDistinguishedName = "OU=Managed,DC=corp,DC=contoso,DC=com",
	SecureChannelMode = SecureChannelMode.LdapsWithSignSealFallback,
};
```

## Usage

### Create the client

```csharp
using ActiveDirectoryService;
using ActiveDirectoryService.Configuration;

var options = new DirectoryConnectionOptions { DomainName = "corp.contoso.com" };
var client = new ActiveDirectoryClient(options);
```

### Users

```csharp
// Check existence
bool exists = client.Users.Exists("jdoe");

// Create an enabled user with an initial password
client.Users.Create("jdoe", "jdoe@corp.contoso.com", "Jane Doe", initialPassword);

// Enable or disable
client.Users.SetEnabled("jdoe", enabled: false);

// Reset password
client.Users.ResetPassword("jdoe", newPassword);
```

### Groups

```csharp
// Check existence
bool exists = client.Groups.Exists("Engineering");

// Create a group
client.Groups.Create("Engineering", "Engineering staff");

// Add / remove members (idempotent; returns whether a change was made)
bool added = client.Groups.AddMember("Engineering", "jdoe");
bool removed = client.Groups.RemoveMember("Engineering", "jdoe");
```

### Custom connection strategy / unit testing

Supply your own `IDirectoryConnectionProvider` to use an alternate authentication
strategy or to mock the connection in unit tests (no live DC required):

```csharp
IDirectoryConnectionProvider provider = new MyServiceAccountProvider(/* ... */);
var client = new ActiveDirectoryClient(provider);
```

## Build

```pwsh
dotnet build ActiveDirectoryService.slnx
```
