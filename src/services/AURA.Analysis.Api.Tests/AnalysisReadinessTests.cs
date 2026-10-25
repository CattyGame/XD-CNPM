using AURA.Analysis.Api.Http;
using AURA.Analysis.Api.Upload;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using Xunit;

namespace AURA.Analysis.Api.Tests;

public sealed class AnalysisReadinessTests
{
    private sealed class Checks(bool db,bool blob,bool failure):IAnalysisReadinessChecks
    {
        public int Calls;
        public Task<bool> DatabaseAsync(CancellationToken ct){Calls++;if(failure)throw new IOException("PRIVATE_CONNECTION_SECRET");return Task.FromResult(db);}
        public Task<bool> StorageAsync(CancellationToken ct){Calls++;return Task.FromResult(blob);}
    }
    [Theory]
    [InlineData(true,true,true,false,200,"ready")]
    [InlineData(true,false,true,false,503,"not_ready")]
    [InlineData(true,true,false,false,503,"not_ready")]
    [InlineData(true,true,true,true,503,"not_ready")]
    [InlineData(false,false,false,true,503,"disabled")]
    public async Task Readiness_reports_only_verified_dependencies(bool enabled,bool db,bool blob,bool failure,int code,string status)
    {
        var checks=new Checks(db,blob,failure);var b=WebApplication.CreateBuilder();b.Logging.ClearProviders();b.WebHost.UseTestServer();b.Services.AddSingleton<IAnalysisReadinessChecks>(checks);
        await using var app=b.Build();app.MapAnalysisReadiness(enabled);await app.StartAsync();using var client=app.GetTestClient();using var response=await client.GetAsync("/health/ready");
        Assert.Equal(code,(int)response.StatusCode);var body=await response.Content.ReadAsStringAsync();Assert.Contains(status,body);Assert.DoesNotContain("PRIVATE_CONNECTION_SECRET",body);Assert.Equal("no-store",response.Headers.CacheControl!.ToString());
        if(!enabled)Assert.Equal(0,checks.Calls);
    }
    [Fact]public async Task Storage_without_readiness_capability_fails_closed()
    {var checks=new AnalysisReadinessChecks(null!,new FakeStorage());Assert.False(await checks.StorageAsync(default));}
}
