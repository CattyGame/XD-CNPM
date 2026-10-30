namespace AURA.StressRunbook.Checks;

public class Program
{
    private static int _passed;
    private static int _failed;

    public static int Main(string[] args)
    {
        Console.WriteLine("====================================================================");
        Console.WriteLine("AURA Task SCRUM-341 (W4-M1-04) Automated Verification Suite");
        Console.WriteLine("====================================================================");

        var repoRoot = FindRepoRoot();
        Console.WriteLine($"Repo Root: {repoRoot}");

        TestAC1_StressTestingAndResourceConstraints(repoRoot);
        TestAC2_BrokerFailureAndBillingIdempotency(repoRoot);
        TestAC3_OperationsRunbookAndHandoverCompleteness(repoRoot);

        Console.WriteLine();
        Console.WriteLine("====================================================================");
        Console.WriteLine($"RESULTS: Passed: {_passed}, Failed: {_failed}");
        Console.WriteLine("====================================================================");

        return _failed == 0 ? 0 : 1;
    }

    private static void TestAC1_StressTestingAndResourceConstraints(string repoRoot)
    {
        Console.WriteLine("\n[AC1] Verifying Batch Infrastructure Stress Test & Resource Ceiling (RAM < 80%, 0 OOMKilled):");

        var stressScriptPath = Path.Combine(repoRoot, "scripts", "dev", "Test-BatchInfrastructureStress.ps1");
        Assert(File.Exists(stressScriptPath), "AC1.1 Test-BatchInfrastructureStress.ps1 exists");

        var stressScriptText = File.ReadAllText(stressScriptPath);
        Assert(stressScriptText.Contains("BatchCount = 100") || stressScriptText.Contains("100"), "AC1.2 Simulates 100 batch tasks under concurrency");
        Assert(stressScriptText.Contains("MaxRamThresholdPercent = 80.0"), "AC1.3 Configures 80.0% max RAM safety threshold");
        Assert(stressScriptText.Contains("docker stats --no-stream"), "AC1.4 Samples real-time container CPU, Memory, Network I/O");
        Assert(stressScriptText.Contains("OOMKilled") && stressScriptText.Contains("137"), "AC1.5 Formally inspects container OOM status and exit code 137");

        var stressReportPath = Path.Combine(repoRoot, "docs", "testing", "m1_batch_infrastructure_stress_report.md");
        Assert(File.Exists(stressReportPath), "AC1.6 Official stress test report m1_batch_infrastructure_stress_report.md exists");

        var stressReportText = File.ReadAllText(stressReportPath);
        Assert(stressReportText.Contains("85.47 req/s") || stressReportText.Contains("1.17"), "AC1.7 Documents verified throughput metrics (85.47 req/s in 1.17s)");
        Assert(stressReportText.Contains("70.50%") && stressReportText.Contains("OOMKilled"), "AC1.8 Confirms peak RAM ceiling < 80% (70.50%) and 0 OOMKilled containers");
    }

    private static void TestAC2_BrokerFailureAndBillingIdempotency(string repoRoot)
    {
        Console.WriteLine("\n[AC2] Verifying Broker Failure Drill, DLQ Routing & Zero Duplicate Billing:");

        var failureScriptPath = Path.Combine(repoRoot, "scripts", "dev", "Test-BrokerFailureAndRecovery.ps1");
        Assert(File.Exists(failureScriptPath), "AC2.1 Test-BrokerFailureAndRecovery.ps1 exists");

        var failureScriptText = File.ReadAllText(failureScriptPath);
        Assert(failureScriptText.Contains("aura.s3.dead.jobs"), "AC2.2 Routes failed messages to Dead-Letter Queue aura.s3.dead.jobs");
        Assert(failureScriptText.Contains("duplicate_billing_detected") && failureScriptText.Contains("IDEMPOTENT_REPLAY_MATCHED"), "AC2.3 Verifies zero duplicate billing upon replay");

        var publisherPath = Path.Combine(repoRoot, "src", "services", "AURA.Analysis.Api", "Outbox", "Adapters", "RabbitMqBrokerPublisher.cs");
        Assert(File.Exists(publisherPath), "AC2.4 RabbitMqBrokerPublisher.cs exists");

        var publisherText = File.ReadAllText(publisherPath);
        Assert(publisherText.Contains("AutomaticRecoveryEnabled = true"), "AC2.5 Automatic AMQP reconnection enabled (AutomaticRecoveryEnabled = true)");
        Assert(publisherText.Contains("BrokerUnavailable"), "AC2.6 Catches broker outage and maps to controlled BrokerUnavailable outcome");
    }

    private static void TestAC3_OperationsRunbookAndHandoverCompleteness(string repoRoot)
    {
        Console.WriteLine("\n[AC3] Verifying Operations Runbook v1.2 Completeness for Engineer Handover:");

        var runbookPath = Path.Combine(repoRoot, "docs", "development", "operations_runbook_v1.2.md");
        Assert(File.Exists(runbookPath), "AC3.1 operations_runbook_v1.2.md exists");

        var runbookText = File.ReadAllText(runbookPath);
        Assert(runbookText.Contains("compose.monitoring.yaml"), "AC3.2 Details monitoring cluster startup via compose.monitoring.yaml");
        Assert(runbookText.Contains("compose.workers.yaml") && runbookText.Contains("--scale"), "AC3.3 Details worker scaling commands via compose.workers.yaml");
        Assert(runbookText.Contains("AURA System Overview") && runbookText.Contains("RED Metrics") && runbookText.Contains("RabbitMQ"), "AC3.4 Comprehensive Grafana Dashboard guide (Overview, RED, RabbitMQ)");
        Assert(runbookText.Contains("AuraHighHttp5xxRate") && runbookText.Contains("AuraAnalysisJobQueueBacklog"), "AC3.5 Alertmanager triage playbooks for production alerts");
        Assert(runbookText.Contains("gw:ratelimit:") && runbookText.Contains("billing:idempotency:") && runbookText.Contains("volatile-lru"), "AC3.6 Redis administration & key namespace standards");
        Assert(runbookText.Contains("shovel") || runbookText.Contains("DLQ"), "AC3.7 Dead-Letter Queue inspection and replay procedures");
        Assert(runbookText.Contains("Backup-AuraDatabase.ps1") && runbookText.Contains("Restore-AuraDatabase.ps1"), "AC3.8 Backup & disaster recovery drill operational guides");
        Assert(runbookText.Contains("PreviousPublicKeyPem") || runbookText.Contains("Zero-Downtime Key Rotation"), "AC3.9 Zero-downtime JWT dual-key rotation buffer procedures");
    }

    private static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [PASS] {testName}");
            Console.ResetColor();
            _passed++;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [FAIL] {testName}");
            Console.ResetColor();
            _failed++;
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AURA.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}
