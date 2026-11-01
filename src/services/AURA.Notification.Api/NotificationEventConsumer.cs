using System.Text;
using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AURA.Notification.Api;

public sealed class RabbitMqConsumerOptions
{
    public const string SectionName = "RabbitMQ";

    public string HostName { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/aura-s3";
    public string UserName { get; set; } = "s3-notification";
    public string Password { get; set; } = "aura_s3_notification_2026";
    public string QueueName { get; set; } = "aura.s3.notification.events";
    public ushort PrefetchCount { get; set; } = 10;
    public bool AutoStart { get; set; } = true;
}

public sealed class NotificationEventConsumer(
    IOptions<RabbitMqConsumerOptions> options,
    NotificationInboxRepository inboxRepository,
    DeviceRegistrationRepository deviceRepository,
    IAccountResolver accountResolver,
    FirebaseApp firebaseApp,
    IConfiguration configuration,
    NpgsqlDataSource dataSource,
    ILogger<NotificationEventConsumer> logger) : BackgroundService
{
    private readonly RabbitMqConsumerOptions _options = options.Value;
    private IConnection? _connection;
    private IModel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoStart)
        {
            logger.LogInformation("NotificationEventConsumer AutoStart is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                InitializeRabbitMq();
                var consumer = new AsyncEventingBasicConsumer(_channel);

                consumer.Received += async (_, ea) =>
                {
                    await HandleMessageAsync(ea, stoppingToken);
                };

                _channel.BasicConsume(
                    queue: _options.QueueName,
                    autoAck: false,
                    consumer: consumer);

                logger.LogInformation("NotificationEventConsumer listening on {Queue} in vhost {VHost}",
                    _options.QueueName, _options.VirtualHost);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "NotificationEventConsumer connection failed. Retrying in 5s...");
                Cleanup();
                await Task.Delay(5000, stoppingToken);
            }
        }

        Cleanup();
    }

    private void InitializeRabbitMq()
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.UserName,
            Password = _options.Password,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.BasicQos(0, _options.PrefetchCount, false);
    }

    public async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        ulong deliveryTag = ea.DeliveryTag;
        try
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("eventId", out var eventIdProp) || !eventIdProp.TryGetGuid(out var eventId) ||
                !root.TryGetProperty("analysisId", out var analysisIdProp) || !analysisIdProp.TryGetGuid(out var analysisId) ||
                !root.TryGetProperty("eventType", out var eventTypeProp) || !root.TryGetProperty("tenantId", out var tenantIdProp) ||
                !tenantIdProp.TryGetGuid(out var tenantId))
            {
                logger.LogWarning("Discarding malformed notification event envelope: {Json}", json);
                _channel?.BasicAck(deliveryTag, false);
                return;
            }

            var eventType = eventTypeProp.GetString();
            if (eventType is not ("analysis.completed" or "analysis.failed" or "billing.payment.success"))
            {
                logger.LogInformation("Ignoring non-terminal event type {EventType}", eventType);
                _channel?.BasicAck(deliveryTag, false);
                return;
            }

            var recipients = await ResolveRecipientsAsync(root, analysisId, cancellationToken);
            if (recipients.Count == 0)
            {
                logger.LogWarning("No recipient accounts found for analysis {AnalysisId}. Acknowledging.", analysisId);
                _channel?.BasicAck(deliveryTag, false);
                return;
            }

            var inboxType = eventType switch
            {
                "analysis.completed" => "ANALYSIS_COMPLETED",
                "billing.payment.success" => "PAYMENT_SUCCESS",
                _ => "ANALYSIS_FAILED"
            };
            var title = eventType switch
            {
                "analysis.completed" => "Phân tích ảnh đáy mắt hoàn tất",
                "billing.payment.success" => "Thanh toán thành công",
                _ => "Phân tích ảnh đáy mắt không thành công"
            };
            var bodyText = eventType switch
            {
                "analysis.completed" => "Kết quả phân tích mắt của bạn đã có! Bấm để xem chi tiết.",
                "billing.payment.success" => "Giao dịch thanh toán đã được xác nhận. Bấm để xem gói cước.",
                _ => "Không thể phân tích ảnh đáy mắt do chất lượng ảnh không đạt tiêu chuẩn."
            };
            var targetPath = eventType == "billing.payment.success"
                ? "/billing/packages"
                : $"/analyses/{analysisId:D}";

            foreach (var recipientId in recipients)
            {
                var command = new SaveInboxItemCommand(
                    AccountId: recipientId,
                    TenantId: tenantId,
                    EventId: eventId,
                    AnalysisId: analysisId,
                    Type: inboxType,
                    Title: title,
                    TargetPath: targetPath,
                    Metadata: json
                );

                var saveResult = await inboxRepository.SaveAsync(command, cancellationToken);
                logger.LogInformation(
                    "Notification inbox outcome for Account {AccountId}, Event {EventId}: {Outcome}",
                    recipientId, eventId, saveResult.Outcome);

                // Dispatch FCM Web Push Notification (AC1, AC2, AC3)
                await SendWebPushNotificationAsync(
                    recipientId,
                    analysisId,
                    title,
                    bodyText,
                    targetPath,
                    cancellationToken);
            }

            _channel?.BasicAck(deliveryTag, false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process notification message. Nacking without requeue to DLQ.");
            _channel?.BasicNack(deliveryTag, false, false);
        }
    }

    private async Task SendWebPushNotificationAsync(
        Guid recipientId,
        Guid analysisId,
        string title,
        string bodyText,
        string targetPath,
        CancellationToken ct)
    {
        try
        {
            var projectId = configuration["Firebase:ProjectId"] ?? "aura-dev-97716";
            var firebaseUid = await accountResolver.ResolveFirebaseUidAsync(recipientId, ct);
            if (string.IsNullOrWhiteSpace(firebaseUid))
            {
                logger.LogDebug("No Firebase UID found for recipient Account {AccountId}", recipientId);
                return;
            }

            var devices = await deviceRepository.GetTokensForUserAsync(projectId, firebaseUid, ct);
            if (devices.Count == 0)
            {
                logger.LogDebug("No active device registrations found for Firebase UID {Uid}", firebaseUid);
                return;
            }

            var messaging = FirebaseMessaging.GetMessaging(firebaseApp);

            foreach (var device in devices)
            {
                try
                {
                    var message = new Message
                    {
                        Token = device.Token,
                        Notification = new FirebaseAdmin.Messaging.Notification
                        {
                            Title = title,
                            Body = bodyText
                        },
                        Data = new Dictionary<string, string>
                        {
                            ["kind"] = "analysis-completed",
                            ["analysisId"] = analysisId.ToString("D"),
                            ["targetPath"] = targetPath,
                            ["url"] = targetPath
                        },
                        Webpush = new WebpushConfig
                        {
                            Notification = new WebpushNotification
                            {
                                Title = title,
                                Body = bodyText,
                                Icon = "/favicon.ico",
                                Tag = $"analysis-{analysisId:D}"
                            },
                            FcmOptions = new WebpushFcmOptions
                            {
                                Link = targetPath
                            }
                        }
                    };

                    var response = await messaging.SendAsync(message, ct);
                    logger.LogInformation(
                        "FCM Push delivered successfully to Device {DeviceId} (User {Uid}): MessageId={MessageId}",
                        device.DeviceId, firebaseUid, response);
                }
                catch (FirebaseMessagingException ex)
                {
                    logger.LogWarning(
                        "FCM Push failed for Device {DeviceId} (User {Uid}). Error: {Code}",
                        device.DeviceId, firebaseUid, ex.MessagingErrorCode);

                    if (ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                        ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                    {
                        // AC3: Auto cleanup unregistered/invalid token
                        logger.LogInformation("Cleaning up expired FCM token for Device {DeviceId}", device.DeviceId);
                        try
                        {
                            await deviceRepository.DeleteIfTokenMatchesAsync(
                                projectId,
                                firebaseUid,
                                device.DeviceId,
                                device.Token,
                                ct);
                        }
                        catch (Exception cleanupEx)
                        {
                            logger.LogWarning(cleanupEx, "Failed to delete expired token for Device {DeviceId}", device.DeviceId);
                        }
                    }
                    else
                    {
                        // Transient FCM error: rethrow to trigger broker retry / DLQ routing
                        logger.LogError(ex, "Transient FCM error for Device {DeviceId}, will trigger DLQ/retry", device.DeviceId);
                        throw;
                    }
                }
                catch (Exception pushEx) when (pushEx is not FirebaseMessagingException)
                {
                    logger.LogError(pushEx, "Unexpected delivery failure for Device {DeviceId}", device.DeviceId);
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to dispatch FCM push for recipient Account {AccountId}", recipientId);
        }
    }

    private async Task<List<Guid>> ResolveRecipientsAsync(
        JsonElement root,
        Guid analysisId,
        CancellationToken ct)
    {
        var list = new List<Guid>();

        if (root.TryGetProperty("data", out var data))
        {
            if (data.TryGetProperty("recipientAccountIds", out var recipientArray) && recipientArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in recipientArray.EnumerateArray())
                {
                    if (elem.TryGetGuid(out var accId) && accId != Guid.Empty && !list.Contains(accId))
                    {
                        list.Add(accId);
                    }
                }
            }

            if (data.TryGetProperty("actorAccountId", out var actorProp) && actorProp.TryGetGuid(out var actorId) && actorId != Guid.Empty)
            {
                if (!list.Contains(actorId))
                {
                    list.Add(actorId);
                }
            }
        }

        if (list.Count > 0)
        {
            return list;
        }

        // Query database analysis_requests to find the requester who created the analysis
        try
        {
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            const string sql = "SELECT requester_id, owner_id FROM analysis.analysis_requests WHERE id = @id LIMIT 1;";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", analysisId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var requesterId = reader.IsDBNull(0) ? Guid.Empty : reader.GetGuid(0);
                var ownerId = reader.IsDBNull(1) ? Guid.Empty : reader.GetGuid(1);
                var targetId = requesterId != Guid.Empty ? requesterId : ownerId;
                if (targetId != Guid.Empty && !list.Contains(targetId))
                {
                    list.Add(targetId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to query requester_id for analysis {AnalysisId}", analysisId);
        }

        return list;
    }

    private void Cleanup()
    {
        try { _channel?.Close(); _channel?.Dispose(); } catch { }
        try { _connection?.Close(); _connection?.Dispose(); } catch { }
        _channel = null;
        _connection = null;
    }
}
