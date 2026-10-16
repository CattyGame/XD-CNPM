using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

string? stagingDirectory = null;

try
{
    if (
        args.Length != 4 ||
        args[0] != "--registry" ||
        args[2] != "--output")
    {
        throw new InvalidOperationException(
            "Usage: --registry <registry.json> --output <external-directory>");
    }

    var registryPath = Path.GetFullPath(args[1]);
    var outputDirectory = Path.GetFullPath(args[3]);

    var repositoryRoot = Directory.GetParent(registryPath)?
        .Parent?.Parent?.FullName
        ?? throw new InvalidOperationException(
            "Cannot determine repository root.");

    if (!File.Exists(Path.Combine(repositoryRoot, "AURA.sln")))
    {
        throw new InvalidOperationException(
            "Registry must be inside repository/infra/service-auth.");
    }

    var relativeOutput = Path.GetRelativePath(
        repositoryRoot,
        outputDirectory);

    var outsideRepository =
        Path.IsPathRooted(relativeOutput) ||
        relativeOutput.StartsWith(
            ".." + Path.DirectorySeparatorChar,
            StringComparison.Ordinal) ||
        relativeOutput == "..";

    if (!outsideRepository)
    {
        throw new InvalidOperationException(
            "Output must be outside the repository.");
    }

    if (
        Directory.Exists(outputDirectory) ||
        File.Exists(outputDirectory))
    {
        throw new InvalidOperationException(
            "Output already exists. Existing keys were not changed.");
    }

    var registry = JsonNode.Parse(
        File.ReadAllText(registryPath))?.AsObject()
        ?? throw new InvalidOperationException("Invalid registry.");

    if (
        registry["schemaVersion"]?.GetValue<int>() != 1 ||
        registry["issuer"]?.GetValue<string>() != "aura-identity-local" ||
        registry["algorithm"]?.GetValue<string>() != "RS256" ||
        registry["tokenLifetimeSeconds"]?.GetValue<int>() != 300)
    {
        throw new InvalidOperationException(
            "Registry does not match the local service-token contract.");
    }

    var clients = registry["clients"]?.AsArray()
        ?? throw new InvalidOperationException("Missing clients.");

    if (clients.Count == 0)
    {
        throw new InvalidOperationException("Client list is empty.");
    }

    var clientIds = new HashSet<string>(StringComparer.Ordinal);
    var principalIds = new HashSet<Guid>();

    foreach (var item in clients)
    {
        var client = item?.AsObject()
            ?? throw new InvalidOperationException("Invalid client.");

        var clientId = client["clientId"]?.GetValue<string>() ?? "";

        if (
            !Regex.IsMatch(clientId, "^[a-z][a-z0-9-]{0,63}$") ||
            !clientIds.Add(clientId))
        {
            throw new InvalidOperationException(
                "Invalid or duplicate clientId.");
        }

        if (
            !Guid.TryParse(
                client["principalId"]?.GetValue<string>(),
                out var principalId) ||
            principalId == Guid.Empty ||
            !principalIds.Add(principalId))
        {
            throw new InvalidOperationException(
                $"Invalid or duplicate principalId: {clientId}");
        }

        var targets = client["allowedTargets"]?.AsObject();

        if (targets is null || targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Missing allowedTargets: {clientId}");
        }

        foreach (var target in targets)
        {
            var scopes = target.Value?.AsArray();

            if (
                string.IsNullOrWhiteSpace(target.Key) ||
                scopes is null ||
                scopes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Invalid target/scopes: {clientId}");
            }

            var scopeSet = new HashSet<string>(StringComparer.Ordinal);

            foreach (var scopeNode in scopes)
            {
                var scope = scopeNode?.GetValue<string>() ?? "";

                if (
                    string.IsNullOrWhiteSpace(scope) ||
                    scope.Any(char.IsWhiteSpace) ||
                    !scopeSet.Add(scope))
                {
                    throw new InvalidOperationException(
                        $"Invalid or duplicate scope: {clientId}");
                }
            }
        }
    }

    using var rsa = RSA.Create(3072);

    var privatePem = rsa.ExportPkcs8PrivateKeyPem();
    var publicPem = rsa.ExportSubjectPublicKeyInfoPem();

    var keyId = "aura-local-" +
        Convert.ToHexString(
            SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))
        .ToLowerInvariant()[..16];

    // Check that the exported public key verifies signatures.
    using var publicRsa = RSA.Create();
    publicRsa.ImportFromPem(publicPem);

    var probe = RandomNumberGenerator.GetBytes(32);
    var signature = rsa.SignData(
        probe,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    if (!publicRsa.VerifyData(
        probe,
        signature,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1))
    {
        throw new CryptographicException(
            "RSA key-pair verification failed.");
    }

    var parentDirectory = Path.GetDirectoryName(outputDirectory)
        ?? throw new InvalidOperationException("Invalid output directory.");

    Directory.CreateDirectory(parentDirectory);

    stagingDirectory = Path.Combine(
        parentDirectory,
        ".aura-setup-" + Guid.NewGuid().ToString("N"));

    CreatePrivateDirectory(stagingDirectory);
    CreatePrivateDirectory(Path.Combine(stagingDirectory, "issuer"));
    CreatePrivateDirectory(Path.Combine(stagingDirectory, "public"));
    CreatePrivateDirectory(Path.Combine(stagingDirectory, "clients"));

    var options = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    var issuerConfig = registry.DeepClone().AsObject();
    issuerConfig["keyId"] = keyId;
    issuerConfig["privateKeyPath"] = "signing-key.pem";
    issuerConfig["publicKeyPath"] = "../public/signing-public.pem";

    var issuerClients = issuerConfig["clients"]!.AsArray();

    foreach (var item in issuerClients)
    {
        var client = item!.AsObject();
        var clientId = client["clientId"]!.GetValue<string>();

        var secret = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var secretHash = HashSecret(secret);

        if (
            !VerifySecret(secret, secretHash) ||
            VerifySecret("invalid-" + secret, secretHash))
        {
            throw new CryptographicException(
                $"Secret hash verification failed: {clientId}");
        }

        client["clientSecretHash"] = secretHash;

        var callerConfig = new
        {
            schemaVersion = 1,
            clientId,
            principalId = client["principalId"]!.GetValue<string>(),
            clientSecret = secret,
            issuer = registry["issuer"]!.GetValue<string>(),
            allowedTargets = client["allowedTargets"]!.DeepClone()
        };

        WritePrivateFile(
            Path.Combine(stagingDirectory, "clients", clientId + ".json"),
            JsonSerializer.Serialize(callerConfig, options));

        Console.WriteLine(
            $"PASS: Generated credentials and verified secret hash: {clientId}");
    }

    WritePrivateFile(
        Path.Combine(stagingDirectory, "issuer", "signing-key.pem"),
        privatePem);

    WritePrivateFile(
        Path.Combine(stagingDirectory, "public", "signing-public.pem"),
        publicPem);

    WritePrivateFile(
        Path.Combine(stagingDirectory, "issuer", "identity-clients.json"),
        issuerConfig.ToJsonString(options));

    WritePrivateFile(
        Path.Combine(stagingDirectory, "manifest.json"),
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            createdAt = DateTimeOffset.UtcNow,
            keyId,
            issuer = "aura-identity-local",
            algorithm = "RS256",
            tokenLifetimeSeconds = 300,
            clientCount = clients.Count
        }, options));

    // Publish only after all files and self-checks succeed.
    Directory.Move(stagingDirectory, outputDirectory);
    stagingDirectory = null;

    Console.WriteLine("PASS: RSA key-pair signature verification.");
    Console.WriteLine("PASS: Credentials created outside repository.");
    Console.WriteLine($"Output directory: {outputDirectory}");
    Console.WriteLine("No private keys, secrets or tokens were printed.");

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine("ERROR: " + exception.Message);
    return 1;
}
finally
{
    if (
        stagingDirectory is not null &&
        Directory.Exists(stagingDirectory))
    {
        Directory.Delete(stagingDirectory, recursive: true);
    }
}

static string HashSecret(string secret)
{
    var salt = RandomNumberGenerator.GetBytes(16);

    var hash = Rfc2898DeriveBytes.Pbkdf2(
        secret,
        salt,
        600_000,
        HashAlgorithmName.SHA256,
        32);

    return "pbkdf2-sha256$600000$" +
        Convert.ToBase64String(salt) + "$" +
        Convert.ToBase64String(hash);
}

static bool VerifySecret(string secret, string storedHash)
{
    var parts = storedHash.Split('$');

    if (
        parts.Length != 4 ||
        parts[0] != "pbkdf2-sha256" ||
        parts[1] != "600000")
    {
        return false;
    }

    var salt = Convert.FromBase64String(parts[2]);
    var expected = Convert.FromBase64String(parts[3]);

    var actual = Rfc2898DeriveBytes.Pbkdf2(
        secret,
        salt,
        600_000,
        HashAlgorithmName.SHA256,
        32);

    return CryptographicOperations.FixedTimeEquals(expected, actual);
}

static void CreatePrivateDirectory(string path)
{
    Directory.CreateDirectory(path);

    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute);
    }
}

static void WritePrivateFile(string path, string content)
{
    using (var stream = new FileStream(
        path,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None))
    using (var writer = new StreamWriter(
        stream,
        new UTF8Encoding(false)))
    {
        writer.Write(content);
        writer.Write('\n');
    }

    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
