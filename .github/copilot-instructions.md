# Copilot Instructions — ActiveDirectoryService

## Purpose

`ActiveDirectoryService` is a reusable .NET class library (DLL) that manages the
**on-premises Active Directory** side of our tenant: creating and managing users,
groups, group membership, enabling/disabling accounts and resetting passwords.

It is the on-prem counterpart to our existing **GoogleService** library (which
manages the Google Workspace side). The main web application — hosted on **IIS on
a domain-joined server** — references both DLLs to manage identities in both
directories. Keep this library focused on Active Directory only; Google Workspace
concerns live in GoogleService.

## Mirror the GoogleService design

For consistency across the two libraries, mirror GoogleService's shape and naming:

- A **facade** (`ActiveDirectoryClient`) is the single entry point, exposing
  `Users` and `Groups` managers.
- A **connection/credential abstraction** (`IDirectoryConnectionProvider` or
  similar) decouples *how we connect/authenticate* from *what we do*, exactly like
  GoogleService's `IWorkspaceCredentialProvider`. This lets us swap connection
  strategies (gMSA/integrated vs. explicit service-account bind) without touching
  the managers.
- `UserManager` and `GroupManager` classes expose the operations.
- Validate arguments at entry points; throw `ArgumentException`/`ArgumentNullException`.
- Favor async signatures where the underlying API supports it. Note that
  `System.DirectoryServices.AccountManagement` is largely synchronous — wrap
  carefully and do not fake async. Use `System.DirectoryServices.Protocols` when
  you need true async or explicit transport control.

## Recommended technology

- **Target framework:** .NET Framework 4.8.1 to match GoogleService (both DLLs are
  consumed by the same IIS app). Modern .NET is acceptable if the web app moves to
  ASP.NET Core, but AD features are Windows-only regardless.
- **Primary library:** `System.DirectoryServices.AccountManagement` (SDS.AM) for
  everyday user/group/membership operations — `PrincipalContext`, `UserPrincipal`,
  `GroupPrincipal`.
- **Lower-level library:** `System.DirectoryServices.Protocols` (SDS.P) when you
  need explicit **LDAPS** / signing+sealing, paged searches, or async.
- Add these as framework references (they ship with the Windows .NET platform).

## Security — how this library must connect (IMPORTANT)

The web app runs on IIS on a domain-joined server that can reach the DC. Follow
these rules; they are the whole reason this library exists securely.

### Identity (authentication to AD)

- **Prefer a gMSA (group Managed Service Account) as the IIS app pool identity.**
  There is no stored password; AD rotates it automatically. The library should
  then connect using the **process/integrated identity** (no credentials in code
  or config).
- Authenticate with **Kerberos**, not NTLM.
- If a gMSA is not available, fall back to a dedicated least-privilege service
  account whose password is supplied via a secret store / environment variable —
  **never** hardcoded (see the OWASP instruction file). Make this path pluggable
  via the connection-provider abstraction.

### Transport (encrypting the channel)

- **Never** use plain LDAP on port 389 with no protection.
- Any password-related write (create user, reset password, set password) **requires**
  an encrypted channel — enforce this.
- **Support both, with an automatic fallback (project decision):** attempt
  **LDAPS first**, and if the secure TLS bind fails (e.g., no DC certificate),
  **fall back to LDAP + Kerberos sign & seal**. Surface the chosen mode in logs,
  make the preference configurable, and never silently fall back to an
  unencrypted channel.
  - **LDAPS** (LDAP over TLS, port 636) — requires the DC to have a server
	certificate (AD CS). Validate the server certificate; do not blindly accept.
  - **LDAP + Kerberos sign & seal** (port 389 with `Sealing`) — no PKI required,
	strong in-domain. Good default when there is no certificate infrastructure.
- With SDS.AM, use `ContextOptions.Negotiate | ContextOptions.Sealing` (sign+seal)
  or `ContextOptions.SecureSocketLayer` (LDAPS). With SDS.P, set
  `LdapConnection.SessionOptions` (`SecureSocketLayer = true` for LDAPS, or
  `Sealing`/`Signing` for Kerberos protection) and verify the certificate callback.

### Authorization (least privilege)

- The service identity must have **only** the delegated AD permissions it needs on
  the specific OU(s) (e.g., create/manage users, reset passwords) — never Domain
  Admin.
- Document the exact delegated rights in the README.

### Secrets & data

- No hardcoded credentials, DNs with passwords, or connection strings with secrets.
- Read any required configuration (domain, container/OU DN, LDAPS on/off) from
  config/secret store, with clear placeholders.

## Coding standards

- The shared `.github/*.instructions.md` files apply (C# style, security/OWASP,
  markdown, docs-on-code-change). Follow them.
- Key C# rules: file-scoped namespaces, `is null` / `is not null`, `nameof`,
  latest C# language version, XML doc comments on public APIs with `<example>`.
- Add "why" comments on security-relevant decisions (why sealing, why LDAPS, why
  gMSA), mirroring the comments in GoogleService.

## Documentation

- Maintain a `README.md` (per the docs-on-code-change instruction) covering:
  features, requirements, how to run the IIS app pool under a gMSA, LDAPS vs.
  sign & seal setup, the delegated AD permissions required, and code samples for
  every public method (match the thoroughness of the GoogleService README).

## Repository & build

- Initialize git with a `.gitignore` that excludes build output, VS cruft, and —
  critically — any credential material or config containing secrets.
- Verify the build compiles before finishing a change.
- When tests are added, mock the connection-provider abstraction so unit tests do
  not require a live domain controller.

## Suggested first steps for this project

1. Create the class library project `ActiveDirectoryService` targeting .NET
   Framework 4.8.1, add `System.DirectoryServices.AccountManagement` and
   `System.DirectoryServices.Protocols` references, and set `LangVersion` to latest.
2. Add the connection abstraction `IDirectoryConnectionProvider` and an
   integrated/gMSA implementation that builds a secured `PrincipalContext`
   (sign & seal or LDAPS).
3. Add `UserManager`, `GroupManager`, and the `ActiveDirectoryClient` facade,
   mirroring GoogleService.
4. Write the README with setup + per-method samples.
