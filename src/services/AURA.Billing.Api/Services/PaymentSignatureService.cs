using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AURA.Billing.Api.Services;

public sealed class PaymentSignatureService
{
    public string ComputeSignature(
        Guid orderId,
        string transactionId,
        decimal amount,
        string currency,
        string paymentStatus,
        long timestamp,
        string secretKey)
    {
        var rawData = BuildDataString(orderId, transactionId, amount, currency, paymentStatus, timestamp);
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var dataBytes = Encoding.UTF8.GetBytes(rawData);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifySignature(
        Guid orderId,
        string transactionId,
        decimal amount,
        string currency,
        string paymentStatus,
        long timestamp,
        string signature,
        string secretKey)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(secretKey))
        {
            return false;
        }

        var expectedSignature = ComputeSignature(orderId, transactionId, amount, currency, paymentStatus, timestamp, secretKey);
        var signatureBytes = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);

        return signatureBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(signatureBytes, expectedBytes);
    }

    public static string BuildDataString(
        Guid orderId,
        string transactionId,
        decimal amount,
        string currency,
        string paymentStatus,
        long timestamp)
    {
        return FormattableString.Invariant(
            $"{orderId:D}|{transactionId.Trim()}|{amount:0.00}|{currency.Trim().ToUpperInvariant()}|{paymentStatus.Trim().ToUpperInvariant()}|{timestamp}");
    }
}
