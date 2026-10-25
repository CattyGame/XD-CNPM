using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AURA.Identity.Api;
using AURA.Profile.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

if (args.Length == 1 && args[0] == "--check-local-material")
{
    try { CheckLocalMaterial(); return 0; }
    catch (Exception ex)
    {
        Console.Error.WriteLine("STOP: Local service-auth material check failed (" + ex.GetType().Name + "). No files or services were changed.");
        return 1;
    }
}
if (args.Length != 0) { Console.Error.WriteLine("Usage: [--check-local-material]"); return 1; }

var tests = new List<(string Name, Action Run)>();
using var fixture = new Fixture();
void Test(string name, Action action) => tests.Add((name, action));
void InvalidIssuer(string name, Action<JsonObject>? mutate = null, Dictionary<string, string?>? overrides = null, string? raw = null)
{
    Test(name, () =>
    {
        var registry = fixture.Registry.DeepClone().AsObject(); mutate?.Invoke(registry);
        File.WriteAllText(fixture.RegistryPath, raw ?? registry.ToJsonString());
        try { ThrowsConfiguration(() => fixture.Issuer(overrides)); }
        finally { File.WriteAllText(fixture.RegistryPath, fixture.Registry.ToJsonString()); }
    });
}

InvalidIssuer("Issuer rejects missing private key configuration", overrides: new() { ["ServiceAuth:PrivateKeyPath"] = null });
InvalidIssuer("Issuer rejects nonexistent key without generating it", overrides: new() { ["ServiceAuth:PrivateKeyPath"] = fixture.MissingKey });
InvalidIssuer("Issuer rejects a public key as signing material", overrides: new() { ["ServiceAuth:PrivateKeyPath"] = fixture.PublicPath });
InvalidIssuer("Issuer rejects malformed signing PEM", overrides: new() { ["ServiceAuth:PrivateKeyPath"] = fixture.BadKeyPath });
InvalidIssuer("Issuer rejects RSA below 2048 bits", overrides: new() { ["ServiceAuth:PrivateKeyPath"] = fixture.WeakKeyPath });
InvalidIssuer("Issuer rejects missing registry configuration", overrides: new() { ["ServiceAuth:RegistryPath"] = null });
InvalidIssuer("Issuer rejects nonexistent registry without fallback", overrides: new() { ["ServiceAuth:RegistryPath"] = fixture.MissingRegistry });
InvalidIssuer("Issuer rejects malformed registry", raw: "{not-json}");
InvalidIssuer("Issuer rejects duplicate JSON property", raw: "{\"schemaVersion\":1,\"schemaVersion\":1}");
InvalidIssuer("Issuer rejects empty clients", r => r["clients"] = new JsonArray());
InvalidIssuer("Issuer rejects missing secret hash", r => Client(r).Remove("clientSecretHash"));
InvalidIssuer("Issuer rejects empty secret hash", r => Client(r)["clientSecretHash"] = "");
InvalidIssuer("Issuer rejects malformed Base64 hash", r => Client(r)["clientSecretHash"] = "pbkdf2-sha256$600000$!$!");
InvalidIssuer("Issuer rejects short PBKDF2 salt", r => Client(r)["clientSecretHash"] = "pbkdf2-sha256$600000$AA==$" + Convert.ToBase64String(new byte[32]));
InvalidIssuer("Issuer rejects wrong PBKDF2 iteration count", r => Client(r)["clientSecretHash"] = Fixture.SecretHash.Replace("600000", "1"));
InvalidIssuer("Issuer rejects wrong hash length", r => Client(r)["clientSecretHash"] = "pbkdf2-sha256$600000$" + Convert.ToBase64String(new byte[16]) + "$AA==");
InvalidIssuer("Issuer rejects duplicate client ID", r => { var c = Client(r).DeepClone().AsObject(); c["principalId"] = Guid.NewGuid(); r["clients"]!.AsArray().Add(c); });
InvalidIssuer("Issuer rejects duplicate principal", r => { var c = Client(r).DeepClone().AsObject(); c["clientId"] = "other-api"; r["clients"]!.AsArray().Add(c); });
InvalidIssuer("Issuer rejects empty principal", r => Client(r)["principalId"] = Guid.Empty);
InvalidIssuer("Issuer rejects malformed target", r => Client(r)["allowedTargets"] = new JsonObject { ["aura profile"] = new JsonArray("profile:resolve") });
InvalidIssuer("Issuer rejects empty target scopes", r => Client(r)["allowedTargets"]!["aura-profile"] = new JsonArray());
InvalidIssuer("Issuer rejects duplicate scopes", r => Client(r)["allowedTargets"]!["aura-profile"] = new JsonArray("profile:resolve", "profile:resolve"));
InvalidIssuer("Issuer rejects wildcard scope", r => Client(r)["allowedTargets"]!["aura-profile"] = new JsonArray("profile:*"));
InvalidIssuer("Issuer rejects mismatched registry issuer", r => r["issuer"] = "wrong-issuer");
InvalidIssuer("Issuer rejects mismatched signing key ID", r => r["keyId"] = "wrong-key");
InvalidIssuer("Issuer rejects wrong algorithm", r => r["algorithm"] = "HS256");
InvalidIssuer("Issuer rejects unknown registry version", r => r["schemaVersion"] = 2);
InvalidIssuer("Issuer rejects partial registry instead of retaining valid client", r => r["clients"]!.AsArray().Add(new JsonObject { ["clientId"] = "broken-client" }));
InvalidIssuer("Issuer rejects missing issuer configuration", overrides: new() { ["ServiceAuth:Issuer"] = null });
InvalidIssuer("Issuer rejects lifetime above 300 seconds", overrides: new() { ["ServiceAuth:TokenLifetimeSeconds"] = "301" });
InvalidIssuer("Issuer rejects zero lifetime", overrides: new() { ["ServiceAuth:TokenLifetimeSeconds"] = "0" });
InvalidIssuer("Issuer rejects malformed lifetime", overrides: new() { ["ServiceAuth:TokenLifetimeSeconds"] = "not-an-int" });
InvalidIssuer("Issuer rejects missing lifetime", overrides: new() { ["ServiceAuth:TokenLifetimeSeconds"] = null });

Test("Valid provisioned issuer issues RS256 and preserves keys across reload", () =>
{
    var before = fixture.Digest();
    string token;
    using (var issuer = fixture.Issuer())
    {
        var result = issuer.IssueToken("fixture-api", Fixture.Secret, "aura-profile", "profile:resolve");
        Require(result.Success && result.ExpiresIn == 300 && result.AccessToken is not null);
        token = result.AccessToken!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Require(jwt.Header.Alg == "RS256" && jwt.Header.Kid == fixture.KeyId && jwt.Subject == Fixture.Principal.ToString("D"));
        Require(jwt.ValidTo - jwt.ValidFrom == TimeSpan.FromSeconds(300));
    }
    using var reloaded = fixture.Issuer();
    Require(reloaded.ValidateServiceToken(token, "aura-profile", "profile:resolve").Valid);
    Require(fixture.Digest() == before);
});
Test("Profile validates the issued token with public material only", () =>
{
    using var issuer = fixture.Issuer(); using var validator = fixture.Validator();
    var token = issuer.IssueToken("fixture-api", Fixture.Secret, "aura-profile", "profile:resolve").AccessToken!;
    Require(validator.ValidateToken(token, "aura-profile", "profile:resolve").Valid);
});
Test("Wrong credentials and removed seed clients are denied", () =>
{
    using var issuer = fixture.Issuer();
    foreach (var pair in new[] { ("fixture-api", "wrong"), ("analysis-api", "secret_analysis_pass"), ("identity-api", "secret_identity_pass"), ("billing-api", "secret_billing_pass") })
        Require(!issuer.IssueToken(pair.Item1, pair.Item2, "aura-profile", "profile:resolve").Success);
});
Test("Issuance cannot escalate audience or scope", () =>
{
    using var issuer = fixture.Issuer();
    Require(issuer.IssueToken("fixture-api", Fixture.Secret, "aura-billing", "billing:capture").ErrorCode == "ERR_FORBIDDEN");
    Require(issuer.IssueToken("fixture-api", Fixture.Secret, "aura-profile", "profile:provision").ErrorCode == "ERR_FORBIDDEN");
    Require(issuer.IssueToken("fixture-api", Fixture.Secret, "aura-profile", " , ").ErrorCode == "ERR_VALIDATION");
});
Test("Malformed stored hashes return false without throwing", () =>
{
    foreach (var hash in new[] { "", "garbage", "pbkdf2-sha256$600000$!$!", new string('x', 200), null! })
        Require(!ServiceTokenIssuer.VerifySecret(Fixture.Secret, hash));
});
Test("Profile accepts explicit inline public PEM", () =>
{
    using var validator = fixture.Validator(new() { ["ServiceAuth:PublicKeyPath"] = null, ["ServiceAuth:PublicKeyPem"] = fixture.PublicPem });
    Require(validator.ValidateToken(fixture.Jwt(), "aura-profile", "profile:resolve").Valid);
});
foreach (var entry in new (string Name, Dictionary<string, string?> Config)[]
{
    ("missing source", new() { ["ServiceAuth:PublicKeyPath"] = null }),
    ("missing file", new() { ["ServiceAuth:PublicKeyPath"] = fixture.MissingKey }),
    ("malformed PEM", new() { ["ServiceAuth:PublicKeyPath"] = fixture.BadKeyPath }),
    ("private key file", new() { ["ServiceAuth:PublicKeyPath"] = fixture.PrivatePath }),
    ("private inline PEM", new() { ["ServiceAuth:PublicKeyPath"] = null, ["ServiceAuth:PublicKeyPem"] = fixture.PrivatePem }),
    ("ambiguous explicit sources", new() { ["ServiceAuth:PublicKeyPem"] = fixture.PublicPem }),
    ("invalid inline PEM despite valid file", new() { ["ServiceAuth:PublicKeyPem"] = "invalid" }),
    ("missing issuer", new() { ["ServiceAuth:Issuer"] = null }),
    ("missing audience", new() { ["ServiceAuth:Audience"] = null }),
    ("weak public key", new() { ["ServiceAuth:PublicKeyPath"] = null, ["ServiceAuth:PublicKeyPem"] = fixture.WeakPublicPem })
})
{
    var captured = entry;
    Test("Profile rejects " + captured.Name, () => ThrowsConfiguration(() => fixture.Validator(captured.Config)));
}
foreach (var entry in new (string Name, Func<string> Token)[]
{
    ("wrong signing key", () => fixture.Jwt(otherKey: true)),
    ("wrong issuer", () => fixture.Jwt(issuer: "wrong-issuer")),
    ("wrong audience", () => fixture.Jwt(audience: "aura-billing")),
    ("missing scope", () => fixture.Jwt(scope: null)),
    ("wrong scope", () => fixture.Jwt(scope: "profile:provision")),
    ("missing expiration", () => fixture.Jwt(expiration: false)),
    ("expired token", () => fixture.Jwt(expired: true)),
    ("future not-before", () => fixture.Jwt(future: true)),
    ("unsigned token", () => fixture.Jwt(unsigned: true)),
    ("RS384 algorithm", () => fixture.Jwt(algorithm: SecurityAlgorithms.RsaSha384)),
    ("missing kid", () => fixture.Jwt(kid: "")),
    ("wrong kid", () => fixture.Jwt(kid: "wrong-key")),
    ("missing subject", () => fixture.Jwt(subject: null)),
    ("empty account subject", () => fixture.Jwt(subject: Guid.Empty.ToString("D"))),
    ("missing client identity", () => fixture.Jwt(clientId: null)),
    ("multiple audiences", () => fixture.Jwt(multipleAudiences: true)),
    ("malformed token", () => "not-a-jwt"),
    ("oversized token", () => new string('a', 16 * 1024 + 1))
})
{
    var captured = entry;
    Test("JWT rejects " + captured.Name + " in issuer and Profile", () =>
    {
        using var issuer = fixture.Issuer(); using var validator = fixture.Validator();
        var token = captured.Token();
        Require(!issuer.ValidateServiceToken(token, "aura-profile", "profile:resolve").Valid);
        Require(!validator.ValidateToken(token, "aura-profile", "profile:resolve").Valid);
    });
}
Test("Issuer binds signed client identity and principal to registry", () =>
{
    using var issuer = fixture.Issuer();
    Require(!issuer.ValidateServiceToken(fixture.Jwt(clientId: "unregistered-api"), "aura-profile", "profile:resolve").Valid);
    Require(!issuer.ValidateServiceToken(fixture.Jwt(subject: Guid.NewGuid().ToString("D")), "aura-profile", "profile:resolve").Valid);
});
Test("Profile cannot override configured audience via method argument", () =>
{
    using var validator = fixture.Validator();
    Require(!validator.ValidateToken(fixture.Jwt(audience: "aura-billing"), "aura-billing", "profile:resolve").Valid);
});
Test("No credentials or fallback key files were created", () =>
{
    Require(!File.Exists(fixture.MissingKey) && !File.Exists(fixture.MissingRegistry));
    Require(fixture.InitialDigest == fixture.Digest());
});

var passed = 0;
foreach (var test in tests)
{
    try { test.Run(); passed++; Console.WriteLine("PASS: " + test.Name); }
    catch (Exception ex) { Console.Error.WriteLine("FAIL: " + test.Name + " (" + ex.GetType().Name + ")"); return 1; }
}
Console.WriteLine($"RESULT: {passed}/{tests.Count} service-auth checks passed. Synthetic keys/credentials only; no HTTP, Firebase or database runtime.");
return 0;

static JsonObject Client(JsonObject root) => root["clients"]![0]!.AsObject();
static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Assertion failed."); }
static void ThrowsConfiguration<T>(Func<T> action) where T : IDisposable
{
    try { using var value = action(); }
    catch (InvalidOperationException ex)
    {
        Require(!ex.Message.Contains("BEGIN", StringComparison.Ordinal) && !ex.Message.Contains(Fixture.Secret, StringComparison.Ordinal));
        return;
    }
    throw new Exception("Expected a configuration failure.");
}

static void CheckLocalMaterial()
{
    var root = Environment.GetEnvironmentVariable("AURA_SERVICE_AUTH_HOME");
    Require(!string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root));
    var home = Path.GetFullPath(root!);
    var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), home);
    Require(Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    var registryPath = Path.Combine(home, "issuer", "identity-clients.json");
    var privatePath = Path.Combine(home, "issuer", "signing-key.pem");
    var publicPath = Path.Combine(home, "public", "signing-public.pem");
    var before = new Dictionary<string, byte[]>();
    byte[] Read(string path)
    {
        using var file = File.OpenRead(path);
        Require(file.Length is > 0 and <= 256 * 1024);
        using var memory = new MemoryStream(); file.CopyTo(memory);
        var bytes = memory.ToArray(); before.TryAdd(path, SHA256.HashData(bytes)); return bytes;
    }
    var registry = JsonNode.Parse(Read(registryPath))!.AsObject();
    Read(privatePath); Read(publicPath);
    var settings = new Dictionary<string, string?>
    {
        ["ServiceAuth:Issuer"] = registry["issuer"]!.GetValue<string>(),
        ["ServiceAuth:TokenLifetimeSeconds"] = registry["tokenLifetimeSeconds"]!.GetValue<int>().ToString(),
        ["ServiceAuth:PrivateKeyPath"] = privatePath, ["ServiceAuth:RegistryPath"] = registryPath
    };
    Console.WriteLine("CHECK: Validating provisioned issuer configuration.");
    using var issuer = new ServiceTokenIssuer(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), NullLogger<ServiceTokenIssuer>.Instance);
    using var publicRsa = RSA.Create(); publicRsa.ImportFromPem(System.Text.Encoding.UTF8.GetString(Read(publicPath)));
    using var issuerPublicRsa = RSA.Create(); issuerPublicRsa.ImportFromPem(issuer.GetPublicKeyPem());
    Require(publicRsa.ExportSubjectPublicKeyInfo().SequenceEqual(issuerPublicRsa.ExportSubjectPublicKeyInfo()));
    using var validator = new ServiceTokenValidator(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ServiceAuth:Issuer"] = issuer.Issuer, ["ServiceAuth:Audience"] = "aura-profile", ["ServiceAuth:PublicKeyPath"] = publicPath
    }).Build(), NullLogger<ServiceTokenValidator>.Instance);
    Console.WriteLine("PASS: Provisioned issuer and public verification key match.");
    var count = 0;
    foreach (var node in registry["clients"]!.AsArray())
    {
        var id = node!["clientId"]!.GetValue<string>();
        // Issuer has already validated IDs before they can be used as filenames.
        Console.WriteLine("CHECK: Validating provisioned client credential: " + id);
        var caller = JsonNode.Parse(Read(Path.Combine(home, "clients", id + ".json")))!.AsObject();
        Require(caller["schemaVersion"]!.GetValue<int>() == 1 && caller["issuer"]!.GetValue<string>() == issuer.Issuer
            && caller["clientId"]!.GetValue<string>() == id
            && Guid.Parse(caller["principalId"]!.GetValue<string>()) == Guid.Parse(node["principalId"]!.GetValue<string>()));
        Require(JsonNode.DeepEquals(caller["allowedTargets"], node["allowedTargets"]));
        var target = node["allowedTargets"]!.AsObject().First();
        var scope = target.Value![0]!.GetValue<string>();
        var result = issuer.IssueToken(id, caller["clientSecret"]!.GetValue<string>(), target.Key, scope);
        Require(result.Success && issuer.ValidateServiceToken(result.AccessToken!, target.Key, scope).Valid);
        if (node["allowedTargets"]!["aura-profile"] is JsonArray profileScopes)
        {
            var profileScope = profileScopes[0]!.GetValue<string>();
            var token = issuer.IssueToken(id, caller["clientSecret"]!.GetValue<string>(), "aura-profile", profileScope);
            Require(token.Success && validator.ValidateToken(token.AccessToken!, "aura-profile", profileScope).Valid);
        }
        count++;
    }
    foreach (var pair in before) Require(SHA256.HashData(File.ReadAllBytes(pair.Key)).SequenceEqual(pair.Value));
    Require(count >= 1);
    Console.WriteLine($"PASS: Provisioned issuer, public key and {count} client credential files are consistent.");
    Console.WriteLine("PASS: All checked credential/key/registry files are byte-for-byte unchanged.");
    Console.WriteLine("CHECK ONLY: No HTTP requests, key rotation, registry changes or service restart.");
}

sealed class Fixture : IDisposable
{
    public const string Secret = "synthetic-fixture-secret-never-a-runtime-credential";
    public static readonly string SecretHash = ServiceTokenIssuer.HashSecret(Secret);
    public static readonly Guid Principal = Guid.Parse("a1000000-0000-4000-8000-000000000099");
    private readonly string _home = Path.Combine(Path.GetTempPath(), "aura-service-auth-fixture-" + Guid.NewGuid().ToString("N"));
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly RSA _other = RSA.Create(2048);
    public string PrivatePath => Path.Combine(_home, "private.pem");
    public string PublicPath => Path.Combine(_home, "public.pem");
    public string RegistryPath => Path.Combine(_home, "registry.json");
    public string BadKeyPath => Path.Combine(_home, "malformed.pem");
    public string WeakKeyPath => Path.Combine(_home, "weak.pem");
    public string MissingKey => Path.Combine(_home, "not-created.pem");
    public string MissingRegistry => Path.Combine(_home, "not-created.json");
    public string PrivatePem => _rsa.ExportPkcs8PrivateKeyPem();
    public string PublicPem => _rsa.ExportSubjectPublicKeyInfoPem();
    public string WeakPublicPem { get; }
    public string KeyId { get; }
    public JsonObject Registry { get; }
    public string InitialDigest { get; }
    public Fixture()
    {
        Directory.CreateDirectory(_home);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(_home, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        using var weak = RSA.Create(1024); WeakPublicPem = weak.ExportSubjectPublicKeyInfoPem();
        File.WriteAllText(PrivatePath, PrivatePem); File.WriteAllText(PublicPath, PublicPem);
        File.WriteAllText(BadKeyPath, "not-a-key"); File.WriteAllText(WeakKeyPath, weak.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(Path.Combine(_home, "fixture.marker"), "synthetic-only");
        KeyId = "aura-local-" + Convert.ToHexString(SHA256.HashData(_rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant()[..16];
        Registry = new JsonObject
        {
            ["schemaVersion"] = 1, ["issuer"] = "aura-identity-local", ["algorithm"] = "RS256",
            ["tokenLifetimeSeconds"] = 300, ["keyId"] = KeyId,
            ["clients"] = new JsonArray(new JsonObject
            {
                ["clientId"] = "fixture-api", ["principalId"] = Principal.ToString("D"), ["clientSecretHash"] = SecretHash,
                ["allowedTargets"] = new JsonObject { ["aura-profile"] = new JsonArray("profile:resolve") }
            })
        };
        File.WriteAllText(RegistryPath, Registry.ToJsonString()); InitialDigest = Digest();
    }
    public ServiceTokenIssuer Issuer(Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ServiceAuth:Issuer"] = "aura-identity-local", ["ServiceAuth:TokenLifetimeSeconds"] = "300",
            ["ServiceAuth:PrivateKeyPath"] = PrivatePath, ["ServiceAuth:RegistryPath"] = RegistryPath
        };
        if (overrides is not null) foreach (var entry in overrides) settings[entry.Key] = entry.Value;
        return new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), NullLogger<ServiceTokenIssuer>.Instance);
    }
    public ServiceTokenValidator Validator(Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ServiceAuth:Issuer"] = "aura-identity-local", ["ServiceAuth:Audience"] = "aura-profile", ["ServiceAuth:PublicKeyPath"] = PublicPath
        };
        if (overrides is not null) foreach (var entry in overrides) settings[entry.Key] = entry.Value;
        return new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), NullLogger<ServiceTokenValidator>.Instance);
    }
    public string Jwt(bool otherKey = false, string issuer = "aura-identity-local", string audience = "aura-profile",
        string? scope = "profile:resolve", bool expiration = true, bool expired = false, bool future = false,
        bool unsigned = false, string algorithm = SecurityAlgorithms.RsaSha256, string? kid = null,
        string? subject = "a1000000-0000-4000-8000-000000000099", string? clientId = "fixture-api", bool multipleAudiences = false)
    {
        var key = new RsaSecurityKey(otherKey ? _other : _rsa) { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } };
        if (kid != "") key.KeyId = kid ?? KeyId;
        var claims = new List<Claim>();
        if (scope is not null) claims.Add(new("scope", scope));
        if (subject is not null) claims.Add(new("sub", subject));
        if (clientId is not null) claims.Add(new("client_id", clientId));
        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(issuer, audience, claims,
            notBefore: expired ? now.AddMinutes(-10) : future ? now.AddMinutes(10) : now.AddSeconds(-5),
            expires: expiration ? expired ? now.AddMinutes(-5) : now.AddMinutes(15) : null,
            signingCredentials: unsigned ? null : new SigningCredentials(key, algorithm));
        if (multipleAudiences) jwt.Payload["aud"] = new[] { audience, "aura-billing" };
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
    public string Digest()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.GetFiles(_home).Order(StringComparer.Ordinal))
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(path)));
            hash.AppendData(File.ReadAllBytes(path));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public void Dispose()
    {
        _rsa.Dispose(); _other.Dispose();
        if (_home.StartsWith(Path.Combine(Path.GetTempPath(), "aura-service-auth-fixture-"), StringComparison.Ordinal)
            && File.ReadAllText(Path.Combine(_home, "fixture.marker")) == "synthetic-only") Directory.Delete(_home, true);
    }
}
