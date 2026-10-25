# Service authentication: explicit provisioned material

Scope: SCRUM-314/316 service identity configuration hardening, plus Profile's
service JWT verifier. This does not complete every acceptance criterion in those
tasks. User identity/Firebase/account provisioning behavior is unchanged.

## Startup and configuration

Identity resolves ServiceTokenIssuer before starting HTTP. It requires explicit
ServiceAuth:Issuer, TokenLifetimeSeconds (1–300), PrivateKeyPath and RegistryPath.
The private RSA key must already exist, be unencrypted, and be at least 2048 bits.
The runtime never generates, writes, searches for, or substitutes a signing key.

The entire registry is validated before any client is published: schemaVersion=1,
algorithm=RS256, issuer, lifetime and computed keyId must match. Every client
needs a unique valid clientId and nonempty unique principalId, a canonical
PBKDF2-SHA256 hash (600000 iterations, 16-byte salt, 32-byte digest), and nonempty
explicit audiences/scopes. Duplicate JSON properties, clients, principals or
scopes, malformed entries, wildcards and partially valid registries fail startup.
The existing external setup tool produces this format. The repository's example
registry is input to that tool, not a usable runtime registry with empty hashes.

Profile resolves ServiceTokenValidator before HTTP startup. Configure issuer,
audience=aura-profile and exactly one PublicKeyPath or PublicKeyPem. Only RSA
public PEM labels (SPKI or PKCS#1), at least 2048 bits, are allowed. An invalid
explicit source does not fall through to a local file or network lookup.
Compose mounts only the existing public directory read-only into Profile; no
private key, issuer registry or client credential directory is mounted there.
For host-only execution, set ServiceAuth__PublicKeyPath to the public PEM under
AURA_SERVICE_AUTH_HOME/public and set ServiceAuth__Issuer/Audience explicitly.

Pinned keys require coordinated provisioning/deployment on rotation. This version
does not implement discovery or automatic rotation. Existing runtime files are
not changed by the patch. Identity/Profile now intentionally fail startup if
those files/configuration are missing, malformed or inconsistent; /health cannot
report a running service in that condition. Other dependencies such as PostgreSQL
are outside this startup material check.

## Token validation

Both validators require an RS256 signature, matching issuer, one matching
audience, expiration/lifetime, the computed kid, nonempty UUID sub, client_id,
and exact required scope. Clock skew remains 30 seconds. Token input is bounded
to 16 KiB. Identity also binds client_id/sub/scopes to its loaded registry.
Issuer denies unauthorized targets/scopes; it cannot seed known dev clients or
substitute a missing secret hash. Malformed stored hashes return false safely.
Public errors do not include validator exception messages or configuration bytes.

RSA signature-provider caches are disabled on these owned key objects so that
reloading/disposing one issuer/validator cannot retain a provider backed by a
previous instance's disposed RSA. Key objects themselves live for the singleton
lifetime and are disposed by DI.

## Validation and local preflight

`dotnet run --project tests/AURA.ServiceAuth.Checks/AURA.ServiceAuth.Checks.csproj --configuration Release --no-launch-profile`

The console compiles links to the actual two production source files. It runs
70 checks with freshly generated synthetic RSA/credentials, including malformed
configuration, key persistence across reload, issued JWT verification, audience,
scope, issuer, signature, expiry, unsigned/RS384/malformed/oversized tokens,
missing/wrong kid, client/subject binding, and public-only Profile configuration.
It creates only its own marked random directory under the OS temp directory and
removes only that directory. It uses no Firebase, PostgreSQL or network service.
CI runs this console separately from Profile identity and Analysis checks.

After loading the existing development environment, append
`-- --check-local-material` to the same command. This separate mode requires an
absolute AURA_SERVICE_AUTH_HOME outside the current repository. It reads the
provisioned key pair, registry and each caller credential file, checks metadata,
allowedTargets and secret/hash consistency, signs/verifies tokens in memory,
and verifies that all files read remain byte-for-byte unchanged. It does not
send HTTP, print/store tokens, rotate keys, modify the registry, or restart any
service. Missing/inconsistent material causes STOP with exit code 1. Do not
regenerate the existing credentials just to bypass a failed check; investigate
the mismatched configuration first.

## Evidence boundaries and remaining work

The package author also ran 8 local-material command scenarios with synthetic
files: success, missing/inside-repo home, mismatched public key, secret, scopes,
principal and malformed registry. Every scenario preserved all input bytes and
kept secrets/hashes/PEM out of output. These are not developer credentials.

The package author ran the 70 component checks successfully on Linux using .NET
SDK 10.0.401. The delivery instructions request a separate Windows run, builds
of both services (not completed here because their other packages are absent
from the available offline NuGet cache), the existing Profile identity/Analysis checks and read-only
validation of the developer's actual material before any rollout.

A positive local-material result is not evidence of Docker mount readability,
health after restart, Firebase authentication through Gateway or end-to-end
Analysis upload. Those require their own runtime checks. No service is deployed
by applying this package. The existing context 401 ambiguity between an invalid
service JWT and invalid public user token is unchanged. Analysis durable upload
orchestration/reconciliation and endpoint wiring remain open.

Primary API references:
- https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rsa.importfrompem
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters
