using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AURA.Analysis.Api.Upload;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.WebUtilities;

try { return await RunChecksAsync(); }
catch (Exception e)
{
    Console.Error.WriteLine("FAIL: Storage fixture preflight stopped (" + e.GetType().Name + "). No SDK exception details were logged.");
    return 1;
}

static async Task<int> RunChecksAsync()
{
// This executable accepts one local Azurite fixture only, never an Azure account.
var run = Environment.GetEnvironmentVariable("AURA_ANALYSIS_STORAGE_TEST_RUN");
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
if (!Guid.TryParseExact(run, "N", out var runId) || runId == Guid.Empty)
    throw new InvalidOperationException("Run through Test-AuraAnalysisStorage.py.");
var evidence = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aura", "checks",
    "m2-analysis-storage-" + run + ".json");
if (!File.Exists(evidence)) throw new InvalidOperationException("Saved fixture plan is required.");
using (var plan = JsonDocument.Parse(await File.ReadAllTextAsync(evidence)))
    if (plan.RootElement.GetProperty("runId").GetString() != run
        || plan.RootElement.GetProperty("endpoint").GetString() != "http://127.0.0.1:10000/devstoreaccount1")
        throw new InvalidOperationException("Fixture plan does not match this run.");
var envPath = Path.Combine(root, "infra/storage/storage.host.env.example");
var env = File.ReadAllLines(envPath).Where(l => l.StartsWith("Storage__", StringComparison.Ordinal))
    .Select(l => l.Split('=', 2)).ToDictionary(p => p[0][9..], p => p[1]);
var fields = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = env["ConnectionString"] };
const string publicKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
if ((string)fields["AccountName"] != "devstoreaccount1" || (string)fields["AccountKey"] != publicKey
    || (string)fields["BlobEndpoint"] != "http://127.0.0.1:10000/devstoreaccount1"
    || env["PublicBlobEndpoint"] != "http://127.0.0.1:10000/devstoreaccount1"
    || env["OriginalContainer"] != "aura-originals" || env["AnnotationContainer"] != "aura-annotations"
    || env["ReportContainer"] != "aura-reports")
    throw new InvalidOperationException("Only the public local emulator fixture is allowed.");
var options = new BlobStorageOptions
{
    Provider = env["Provider"], ConnectionString = env["ConnectionString"],
    PublicBlobEndpoint = env["PublicBlobEndpoint"], OriginalContainer = env["OriginalContainer"],
    AnnotationContainer = env["AnnotationContainer"], ReportContainer = env["ReportContainer"]
};
var storage = new AzureBlobPrivateFileStorage(options, development: true);
var sdkOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03);
sdkOptions.Retry.MaxRetries = 0;
sdkOptions.Diagnostics.IsLoggingEnabled = false;
sdkOptions.Diagnostics.IsDistributedTracingEnabled = false;
var service = new BlobServiceClient(new Uri("http://127.0.0.1:10000/devstoreaccount1"),
    new StorageSharedKeyCredential("devstoreaccount1", publicKey), sdkOptions);
using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
    { Timeout = TimeSpan.FromSeconds(10) };
using var all = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var ct = all.Token;
var containers = new[] { "aura-originals", "aura-annotations" };
// Keys are recorded before any mutation and deterministic for this run, including cleanup on failure.
var first = $"{runId:D}/{runId:D}/{runId:D}";
var secondId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(run + ":sibling"))[..16]);
var second = $"{runId:D}/{runId:D}/{secondId:D}";
var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j2ioAAAAASUVORK5CYII=");
var owned = new List<(string Container, string Key)>();
var urls = new Dictionary<string, Uri>();
var etags = new Dictionary<string, string>();
int groups = 0;
void Require(bool ok) { if (!ok) throw new InvalidOperationException("Storage fixture assertion failed."); }
void Pass(string label) { groups++; Console.WriteLine("PASS: " + label); }
async Task<byte[]> Read(IPrivateFileStorage adapter, string container, string key)
{
    await using var stream = await adapter.OpenReadAsync(container, key, ct);
    using var output = new MemoryStream();
    await stream.CopyToAsync(output, ct);
    return output.ToArray();
}
async Task Put(IPrivateFileStorage adapter, string container, string key, byte[] payload)
{
    using var stream = new MemoryStream(payload, writable: false);
    await adapter.PutAsync(container, key, stream, "image/png", ct);
}
async Task Denied(Uri url, HttpMethod method)
{
    using var request = new HttpRequestMessage(method, url);
    if (method == HttpMethod.Put)
    {
        request.Headers.Add("x-ms-blob-type", "BlockBlob");
        request.Content = new ByteArrayContent([0, 1, 2]);
    }
    using var response = await client.SendAsync(request, ct);
    Require(response.StatusCode == HttpStatusCode.Forbidden);
}
Exception? failure = null;
try
{
    foreach (var c in containers.Append("aura-reports"))
        Require((await service.GetBlobContainerClient(c).GetAccessPolicyAsync(cancellationToken: ct))
            .Value.BlobPublicAccess == PublicAccessType.None);
    Pass("All three existing containers are private; ACLs were not changed.");
    foreach (var c in containers)
    {
        foreach (var key in new[] { first, second })
        {
            Require(!(await service.GetBlobContainerClient(c).GetBlobClient(key).ExistsAsync(ct)).Value);
            owned.Add((c, key)); // Proven absent; cleanup also covers an upload acknowledgement lost in transit.
            await Put(storage, c, key, bytes);
        }
        Require((await Read(storage, c, first)).SequenceEqual(bytes));
        etags[c] = (await service.GetBlobContainerClient(c).GetBlobClient(first)
            .GetPropertiesAsync(cancellationToken: ct)).Value.ETag.ToString();
    }
    Pass("Original and annotation adapters upload and read the exact synthetic bytes.");
    foreach (var c in containers)
    {
        await Put(storage, c, first, bytes);
        Require((await service.GetBlobContainerClient(c).GetBlobClient(first)
            .GetPropertiesAsync(cancellationToken: ct)).Value.ETag.ToString() == etags[c]);
    }
    Pass("Same key and content replay preserves the original blob ETag.");
    foreach (var c in containers)
    {
        bool rejected = false;
        try { await Put(storage, c, first, [1, 2, 3]); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && (await Read(storage, c, first)).SequenceEqual(bytes));
    }
    Pass("Different content cannot overwrite either existing blob.");
    foreach (var c in containers)
        await Denied(service.GetBlobContainerClient(c).GetBlobClient(first).Uri, HttpMethod.Get);
    Pass("Anonymous reads are denied for both kinds of blob.");
    foreach (var c in containers)
    {
        var before = DateTimeOffset.UtcNow;
        var access = await storage.CreateReadAccessAsync(c, first, TimeSpan.FromSeconds(120), ct);
        var after = DateTimeOffset.UtcNow;
        var query = QueryHelpers.ParseQuery(access.Url.Query);
        Require(access.Url.Host == "127.0.0.1" && access.Url.Port == 10000
            && query["sp"] == "r" && query["sr"] == "b" && query["rscc"] == "no-store"
            && DateTimeOffset.Parse(query["se"].ToString()).ToUniversalTime() == access.ExpiresAt
            && access.ExpiresAt >= before.AddSeconds(119) && access.ExpiresAt <= after.AddSeconds(120));
        using var response = await client.GetAsync(access.Url, ct);
        Require(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsByteArrayAsync(ct)).SequenceEqual(bytes)
            && response.Headers.CacheControl?.NoStore == true);
        urls[c] = access.Url;
    }
    Pass("Browser URLs read exact bytes, grant only blob read and expire after 120 seconds; no-store is set.");
    foreach (var c in containers)
    {
        await Denied(urls[c], HttpMethod.Put);
        Require((await Read(storage, c, first)).SequenceEqual(bytes));
    }
    Pass("Read SAS cannot overwrite a blob; stored bytes remain unchanged.");
    foreach (var c in containers)
    {
        var sibling = new UriBuilder(urls[c]) { Path = $"/devstoreaccount1/{c}/{second}" }.Uri;
        await Denied(sibling, HttpMethod.Get);
    }
    Pass("A blob SAS cannot read a different existing blob.");
    var expired = new AzureBlobPrivateFileStorage(options, true, new PastClock());
    foreach (var c in containers)
        await Denied((await expired.CreateReadAccessAsync(c, first, TimeSpan.FromSeconds(120), ct)).Url, HttpMethod.Get);
    Pass("Expired SAS is denied without waiting for the normal TTL.");
    var reopened = new AzureBlobPrivateFileStorage(options, true);
    foreach (var c in containers) Require((await Read(reopened, c, first)).SequenceEqual(bytes));
    Pass("A fresh adapter reads the same persisted blobs.");
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    bool cancelledOk = false, ttlOk = false;
    try { await storage.CreateReadAccessAsync(containers[0], first, TimeSpan.FromSeconds(120), cancelled.Token); }
    catch (OperationCanceledException) { cancelledOk = true; }
    try { await storage.CreateReadAccessAsync(containers[0], first, TimeSpan.FromSeconds(121), ct); }
    catch (ArgumentOutOfRangeException) { ttlOk = true; }
    Require(cancelledOk && ttlOk);
    Pass("Caller cancellation and maximum TTL are enforced.");
}
catch (Exception e) { failure = e; }
finally
{
    // Bounded cleanup uses only the four predeclared UUID keys. Never list or delete a container.
    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    try
    {
        foreach (var (c, key) in owned)
        {
            await storage.DeleteAsync(c, key, cleanup.Token);
            Require(!(await service.GetBlobContainerClient(c).GetBlobClient(key).ExistsAsync(cleanup.Token)).Value);
        }
        Require(owned.Count == 4);
        foreach (var c in containers.Append("aura-reports"))
            Require((await service.GetBlobContainerClient(c).GetAccessPolicyAsync(cancellationToken: cleanup.Token))
                .Value.BlobPublicAccess == PublicAccessType.None);
        Pass("All four fixture blobs removed; containers and ACLs preserved.");
    }
    catch (Exception e) { failure ??= e; }
}
if (failure is not null)
{
    // Do not emit SDK exception messages, request URLs, SAS or response bodies.
    Console.Error.WriteLine("FAIL: Azurite storage checks stopped before group " + (groups + 1) + " (" + failure.GetType().Name
        + (failure is Azure.RequestFailedException azure ? "; HTTP " + azure.Status : "")
        + "). Review the saved UUID fixture plan.");
    return 1;
}
Require(groups == 12);
Console.WriteLine("RESULT: 12/12 Azurite adapter groups passed; four fixture blobs removed.");
return 0;

}

sealed class PastClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddMinutes(-10);
}
