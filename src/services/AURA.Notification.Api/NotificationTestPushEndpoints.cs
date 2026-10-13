using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Npgsql;

namespace AURA.Notification.Api;

public static class NotificationTestPushEndpoints
{
    public static IServiceCollection AddNotificationTestPush(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;

            options.AddPolicy("notification-test-push", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue(
                        ClaimTypes.NameIdentifier) ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.OnRejected = async (rejected, cancellationToken) =>
            {
                rejected.HttpContext.Response.Headers["Retry-After"] = "60";

                await Error(
                    rejected.HttpContext,
                    429,
                    "ERR_RATE_LIMITED",
                    "Đã đạt giới hạn kiểm thử. Hãy chờ một phút.",
                    true).ExecuteAsync(rejected.HttpContext);
            };
        });

        return services;
    }

    public static void MapNotificationTestPush(
        this WebApplication app)
    {
        if (
            !app.Environment.IsDevelopment() ||
            !app.Configuration.GetValue<bool>(
                "Notification:EnableTestPush"))
        {
            return;
        }

        var projectId = app.Configuration["Firebase:ProjectId"]
            ?? throw new InvalidOperationException(
                "Missing Firebase:ProjectId.");

        app.MapPost(
            "/api/v1/notifications/devices/{deviceId:guid}/test-push",
            async Task<IResult> (
                Guid deviceId,
                DeviceRegistrationRepository repository,
                FirebaseApp firebaseApp,
                ILoggerFactory loggerFactory,
                HttpContext context) =>
            {
                if (deviceId == Guid.Empty)
                {
                    return Error(
                        context,
                        400,
                        "ERR_VALIDATION",
                        "DeviceId không hợp lệ.");
                }

                var uid = context.User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(uid))
                {
                    return Error(
                        context,
                        401,
                        "ERR_UNAUTHORIZED",
                        "Cần đăng nhập.");
                }

                var logger = loggerFactory.CreateLogger(
                    "AURA.Notification.TestPush");

                using var timeout =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        context.RequestAborted);

                timeout.CancelAfter(TimeSpan.FromSeconds(15));

                string? token = null;

                try
                {
                    token = await repository.GetTokenAsync(
                        projectId,
                        uid,
                        deviceId,
                        timeout.Token);

                    if (token is null)
                    {
                        return Error(
                            context,
                            404,
                            "ERR_DEVICE_NOT_REGISTERED",
                            "Không tìm thấy thiết bị đã đăng ký cho tài khoản.");
                    }

                    var testId = Guid.NewGuid().ToString("N");

                    var message = new Message
                    {
                        Token = token,
                        Notification = new FirebaseAdmin.Messaging.Notification
                        {
                            Title = "AURA - Thông báo kiểm thử",
                            Body = "Bạn có thông báo mới từ AURA."
                        },
                        Data = new Dictionary<string, string>
                        {
                            ["kind"] = "aura-test",
                            ["testId"] = testId
                        },
                        Webpush = new WebpushConfig
                        {
                            Headers = new Dictionary<string, string>
                            {
                                ["TTL"] = "120"
                            }
                        }
                    };

                    await FirebaseMessaging
                        .GetMessaging(firebaseApp)
                        .SendAsync(message, timeout.Token);

                    return Results.Ok(new
                    {
                        accepted = true,
                        testId,
                        message =
                            "FCM đã chấp nhận gửi. Cần xác nhận nhận trên trình duyệt."
                    });
                }
                catch (FirebaseMessagingException exception)
                {
                    logger.LogWarning(
                        "FCM test push failed. Code: {ErrorCode}",
                        exception.MessagingErrorCode);

                    if (
                        exception.MessagingErrorCode ==
                        MessagingErrorCode.Unregistered)
                    {
                        try
                        {
                            if (token is not null)
                            {
                                await repository.DeleteIfTokenMatchesAsync(
                                    projectId,
                                    uid,
                                    deviceId,
                                    token,
                                    timeout.Token);
                            }
                        }
                        catch (NpgsqlException)
                        {
                            return Error(
                                context,
                                503,
                                "ERR_STORAGE_UNAVAILABLE",
                                "Chưa hoàn tất xóa đăng ký token hết hiệu lực.",
                                true);
                        }
                        catch (TimeoutException)
                        {
                            return Error(
                                context,
                                503,
                                "ERR_STORAGE_UNAVAILABLE",
                                "Chưa xác nhận xóa đăng ký token hết hiệu lực.",
                                true);
                        }
                        catch (OperationCanceledException)
                            when (!context.RequestAborted.IsCancellationRequested)
                        {
                            return Error(
                                context,
                                503,
                                "ERR_STORAGE_UNAVAILABLE",
                                "Chưa xác nhận xóa đăng ký token hết hiệu lực.",
                                true);
                        }

                        return Error(
                            context,
                            410,
                            "ERR_FCM_TOKEN_EXPIRED",
                            "FCM token không còn hợp lệ. Hãy bật thông báo lại.");
                    }

                    return Error(
                        context,
                        503,
                        "ERR_PUSH_UNAVAILABLE",
                        "Chưa gửi được thông báo kiểm thử.",
                        true);
                }
                catch (CryptographicException)
                {
                    return Error(
                        context,
                        500,
                        "ERR_TOKEN_DECRYPTION",
                        "Không đọc được token đã lưu.");
                }
                catch (NpgsqlException)
                {
                    return Error(
                        context,
                        503,
                        "ERR_STORAGE_UNAVAILABLE",
                        "Không đọc được đăng ký thiết bị.",
                        true);
                }
                catch (TimeoutException)
                {
                    return Error(
                        context,
                        503,
                        "ERR_PUSH_TIMEOUT",
                        "Chưa xác nhận kết quả gửi thông báo.",
                        true);
                }
                catch (OperationCanceledException)
                    when (!context.RequestAborted.IsCancellationRequested)
                {
                    return Error(
                        context,
                        503,
                        "ERR_PUSH_TIMEOUT",
                        "Chưa xác nhận kết quả gửi thông báo.",
                        true);
                }
            })
            .RequireRateLimiting("notification-test-push");
    }

    private static IResult Error(
        HttpContext context,
        int status,
        string code,
        string message,
        bool retryable = false)
    {
        return Results.Json(new
        {
            error = new
            {
                code,
                message,
                correlationId = context.TraceIdentifier,
                retryable
            }
        }, statusCode: status);
    }
}