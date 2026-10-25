using System.Net;
using System.Text.Json;
using AURA.Analysis.Api.Upload;
using Xunit;

namespace AURA.Analysis.Api.Tests;

public class BillingReservationsClientTests
{
    private static ReserveCredit Request() => new(Guid.NewGuid(), "ACCOUNT:" + Owner, Guid.NewGuid(), Guid.NewGuid())
    {
        ActorAccountId = Owner, PatientId = Guid.NewGuid(), CorrelationId = "corr-billing"
    };
    private static readonly Guid Owner = Guid.Parse("11111111-1111-4111-8111-111111111111");

    private static string Reply(ReserveCredit request, Guid id, string state = "RESERVED") => JsonSerializer.Serialize(new
    {
        reservationId = id, analysisId = request.AnalysisId, generation = request.Generation,
        units = 1, state, createdAt = DateTimeOffset.UtcNow
    });

    private static (BillingReservationsClient Client, ServiceReplyHandler Handler, FakeServiceTokens Tokens) Make()
    {
        var handler = new ServiceReplyHandler();
        var tokens = new FakeServiceTokens();
        return (new BillingReservationsClient(new HttpClient(handler) { BaseAddress = new Uri("http://billing.test") }, tokens), handler, tokens);
    }

    [Theory]
    [InlineData(201)]
    [InlineData(200)]
    public async Task Reserve_new_or_replayed_sends_exact_Billing_contract(int status)
    {
        var (client, handler, tokens) = Make();
        var request = Request(); var id = Guid.NewGuid();
        handler.Reply = (_, _, _) => Task.FromResult(ServiceReplyHandler.Json(Reply(request, id), (HttpStatusCode)status));
        var result = await client.ReserveAsync(request, default);
        Assert.Equal(ReservationStatus.Reserved, result.Status);
        Assert.Equal(id, result.ReservationId);
        Assert.Equal("/internal/v1/billing/reservations", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal(tokens.Token, handler.Request.Headers.Authorization.Parameter);
        Assert.Equal(request.IdempotencyKey.ToString("D"), Assert.Single(handler.Request.Headers.GetValues("Idempotency-Key")));
        Assert.Equal("corr-billing", Assert.Single(handler.Request.Headers.GetValues("X-Correlation-Id")));
        Assert.Equal(("aura-billing", "billing:reserve", "corr-billing"), Assert.Single(tokens.Calls));
        using var json = JsonDocument.Parse(handler.Body!);
        var body = json.RootElement;
        Assert.Equal(request.ActorAccountId, body.GetProperty("actorAccountId").GetGuid());
        Assert.Equal(request.PatientId, body.GetProperty("patientId").GetGuid());
        Assert.Equal(request.TenantId, body.GetProperty("tenantId").GetGuid());
        Assert.Equal(request.AnalysisId, body.GetProperty("analysisId").GetGuid());
        Assert.Equal(1, body.GetProperty("units").GetInt32());
        Assert.Equal(1, body.GetProperty("generation").GetInt32());
        Assert.Equal("ACCOUNT", body.GetProperty("billingOwner").GetProperty("type").GetString());
        Assert.Equal(Owner, body.GetProperty("billingOwner").GetProperty("id").GetGuid());
        Assert.False(body.TryGetProperty("idempotencyKey", out _));
    }

    [Fact]
    public async Task Missing_authoritative_actor_or_patient_never_sends_reserve()
    {
        foreach (var request in new[] { Request() with { ActorAccountId = Guid.Empty }, Request() with { PatientId = Guid.Empty } })
        {
            var (client, handler, tokens) = Make();
            Assert.Equal(503, (await Assert.ThrowsAsync<UploadFailure>(() => client.ReserveAsync(request, default))).Status);
            Assert.Equal(0, handler.Calls);
            Assert.Empty(tokens.Calls);
        }
    }

    [Theory]
    [InlineData("ACCOUNT:not-a-guid")]
    [InlineData("SYSTEM_ADMIN:11111111-1111-4111-8111-111111111111")]
    [InlineData("CLINIC:00000000-0000-0000-0000-000000000000")]
    public async Task Invalid_billing_owner_never_sends_reserve(string owner)
    {
        var (client, handler, _) = Make();
        await Assert.ThrowsAsync<UploadFailure>(() => client.ReserveAsync(Request() with { BillingOwner = owner }, default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Insufficient_credit_is_returned_only_for_expected_error_code()
    {
        var (client, handler, _) = Make();
        handler.Reply = (_, _, _) => Task.FromResult(ServiceReplyHandler.Json(
            """{"error":{"code":"ERR_INSUFFICIENT_CREDIT"}}""", HttpStatusCode.PaymentRequired));
        Assert.Equal(ReservationStatus.InsufficientCredit, (await client.ReserveAsync(Request(), default)).Status);
        handler.Reply = (_, _, _) => Task.FromResult(ServiceReplyHandler.Json("{}", HttpStatusCode.PaymentRequired));
        Assert.Equal(503, (await Assert.ThrowsAsync<UploadFailure>(() => client.ReserveAsync(Request(), default))).Status);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task Service_auth_and_dependency_failures_do_not_become_insufficient_credit(int status)
    {
        var (client, handler, _) = Make();
        handler.Reply = (_, _, _) => Task.FromResult(ServiceReplyHandler.Json("{}", (HttpStatusCode)status));
        var failure = await Assert.ThrowsAsync<UploadFailure>(() => client.ReserveAsync(Request(), default));
        Assert.Equal(503, failure.Status); Assert.True(failure.Retryable); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("RELEASED")]
    [InlineData("CAPTURED")]
    public async Task Terminal_reservation_is_not_treated_as_a_successful_reserve(string state)
    {
        var (client, handler, _) = Make(); var request = Request();
        handler.Reply = (_, _, _) => Task.FromResult(ServiceReplyHandler.Json(Reply(request, Guid.NewGuid(), state)));
        var failure = await Assert.ThrowsAsync<UploadFailure>(() => client.ReserveAsync(request, default));
        Assert.Equal(409, failure.Status); Assert.Equal("ERR_REQUEST_IN_PROGRESS", failure.Code);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    public async Task Malformed_reservation_is_a_dependency_error(string body)
    {
        var (client, handler, _) = Make();
        handler.Reply = (_, _, _) => Task.FromResult(ServiceReplyHandler.Json(body));
        Assert.Equal(503, (await Assert.ThrowsAsync<UploadFailure>(() => client.ReserveAsync(Request(), default))).Status);
    }

    [Fact]
    public async Task Reservation_for_another_analysis_is_rejected()
    {
        var (client, handler, _) = Make(); var request = Request();
        handler.Reply = (_, _, _) => Task.FromResult(ServiceReplyHandler.Json(Reply(Request(), Guid.NewGuid())));
        Assert.Equal(503, (await Assert.ThrowsAsync<UploadFailure>(() => client.ReserveAsync(request, default))).Status);
    }

    [Theory]
    [InlineData("ERR_IDEMPOTENCY_CONFLICT", "ERR_IDEMPOTENCY_KEY_REUSED")]
    [InlineData("ERR_RESERVATION_STATE_CONFLICT", "ERR_REQUEST_IN_PROGRESS")]
    public async Task Conflicts_are_mapped_to_public_Analysis_errors(string billingCode, string publicCode)
    {
        var (client, handler, _) = Make();
        handler.Reply = (_, _, _) => Task.FromResult(ServiceReplyHandler.Json(
            JsonSerializer.Serialize(new { error = new { code = billingCode } }), HttpStatusCode.Conflict));
        var failure = await Assert.ThrowsAsync<UploadFailure>(() => client.ReserveAsync(Request(), default));
        Assert.Equal(409, failure.Status); Assert.Equal(publicCode, failure.Code);
    }

    [Fact]
    public async Task Release_uses_release_scope_reason_and_its_own_idempotency_key()
    {
        var (client, handler, tokens) = Make(); var reserve = Request(); var id = Guid.NewGuid();
        var request = new ReleaseCredit(reserve.TenantId, id, Guid.NewGuid()) { Reason = "STORAGE_ABORT", CorrelationId = "corr-release" };
        handler.Reply = (_, _, _) => Task.FromResult(ServiceReplyHandler.Json(Reply(reserve, id, "RELEASED")));
        await client.ReleaseAsync(request, default);
        Assert.Equal($"/internal/v1/billing/reservations/{id:D}/release", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(("aura-billing", "billing:release", "corr-release"), Assert.Single(tokens.Calls));
        Assert.Equal(request.IdempotencyKey.ToString("D"), Assert.Single(handler.Request.Headers.GetValues("Idempotency-Key")));
        using var json = JsonDocument.Parse(handler.Body!);
        Assert.Equal("STORAGE_ABORT", json.RootElement.GetProperty("reason").GetString());
        Assert.Single(json.RootElement.EnumerateObject());
    }

    [Fact]
    public async Task Release_does_not_accept_a_different_reservation_or_terminal_state()
    {
        foreach (var state in new[] { "RESERVED", "CAPTURED", "RELEASED" })
        {
            var (client, handler, _) = Make(); var reserve = Request(); var id = Guid.NewGuid();
            handler.Reply = (_, _, _) => Task.FromResult(ServiceReplyHandler.Json(Reply(reserve,
                state == "RELEASED" ? Guid.NewGuid() : id, state)));
            await Assert.ThrowsAsync<UploadFailure>(() => client.ReleaseAsync(new ReleaseCredit(reserve.TenantId, id, Guid.NewGuid()), default));
        }
    }

    [Fact]
    public async Task Network_failure_after_send_is_not_automatically_retried()
    {
        var (client, handler, _) = Make();
        handler.Reply = (_, _, _) => throw new HttpRequestException("simulated lost reply");
        Assert.True((await Assert.ThrowsAsync<UploadFailure>(() => client.ReserveAsync(Request(), default))).Retryable);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_wrapped_as_dependency_error()
    {
        var (client, handler, _) = Make(); using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReserveAsync(Request(), cts.Token));
        Assert.Equal(0, handler.Calls);
    }
}
