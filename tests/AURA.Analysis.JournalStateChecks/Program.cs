using AURA.Analysis.Api.Upload;
using System.Text.Json;

var cases = new List<(string,Action)>();
void Test(string name, Action test) => cases.Add((name,test));
void Reject(Action test) { try { test(); } catch (Exception ex) when (ex is UploadFailure or ArgumentException) { return; } throw new Exception("Expected rejection"); }
void Require(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
var plan = Fixture(); var image = plan.Images[0].AnalysisId; var owner = "ACCOUNT:"+plan.OwnerId;
UploadRecoveryRecord Prepared() => UploadRecovery.Prepare(plan,owner,"aura-originals");
UploadRecoveryRecord Intent() => UploadRecovery.Apply(Prepared(),UploadRecoveryEvent.ReserveIntent,image);
UploadRecoveryRecord Reserved() => UploadRecovery.Apply(Intent(),UploadRecoveryEvent.Reserved,image,Guid.NewGuid());
UploadRecoveryRecord BlobIntent() => UploadRecovery.Apply(Reserved(),UploadRecoveryEvent.BlobIntent,image);
UploadRecoveryRecord Stored() => UploadRecovery.Apply(BlobIntent(),UploadRecoveryEvent.BlobStored,image);
UploadRecoveryRecord Abort(UploadRecoveryRecord r) => UploadRecovery.Apply(r,UploadRecoveryEvent.BeginAbort,reason:"ANALYSIS_ABORT");

Test("Prepare freezes request metadata, generation and an attempt-specific UUID object key",()=>{
 var r=Prepared();UploadRecovery.Validate(r);Require(r.Revision==1&&r.AttemptNo==1&&r.Effects[0].Generation==1);
 Require(r.Plan.Images[0].ObjectKey.EndsWith(r.AttemptId.ToString("D")) && plan.RecoveryAttemptId is null);
 Require(r.Plan.Response==plan.Response && r.Plan.Images[0].AnalysisId==image);
});
Test("Intent is durable state before a reserve acknowledgement",()=>{var r=Intent();Require(r.Effects[0].ReserveStarted&&r.Effects[0].ReservationId is null&&!UploadRecovery.ReadyToAccept(r));});
Test("Reserve acknowledgement without prior intent is rejected",()=>Reject(()=>UploadRecovery.Apply(Prepared(),UploadRecoveryEvent.Reserved,image,Guid.NewGuid())));
Test("Reservation ID cannot change after acknowledgement",()=>Reject(()=>UploadRecovery.Apply(Reserved(),UploadRecoveryEvent.Reserved,image,Guid.NewGuid())));
Test("Reserve acknowledgement cannot use empty UUID",()=>Reject(()=>UploadRecovery.Apply(Intent(),UploadRecoveryEvent.Reserved,image,Guid.Empty)));
Test("Reserve acknowledgement replay does not advance revision",()=>{var r=Reserved();Require(UploadRecovery.Apply(r,UploadRecoveryEvent.Reserved,image,r.Effects[0].ReservationId)==r);});
Test("Blob intent requires credit for a chargeable image",()=>Reject(()=>UploadRecovery.Apply(Prepared(),UploadRecoveryEvent.BlobIntent,image)));
Test("Blob acknowledgement requires a preceding intent",()=>Reject(()=>UploadRecovery.Apply(Reserved(),UploadRecoveryEvent.BlobStored,image)));
Test("All required effects must be acknowledged before acceptance",()=>{Require(!UploadRecovery.ReadyToAccept(BlobIntent()));Require(UploadRecovery.ReadyToAccept(Stored()));});
Test("Credit denial cannot coexist with a reservation",()=>Reject(()=>UploadRecovery.Apply(Reserved(),UploadRecoveryEvent.CreditDenied,image)));
Test("Credit denial is retained rather than accepting work",()=>{var r=UploadRecovery.Apply(Intent(),UploadRecoveryEvent.CreditDenied,image);Require(r.Effects[0].CreditDenied&&!UploadRecovery.ReadyToAccept(r));});
Test("No compensation while an upload is still pending",()=>Reject(()=>UploadRecovery.Apply(Reserved(),UploadRecoveryEvent.Released,image)));
Test("No object deletion acknowledgement while pending",()=>Reject(()=>UploadRecovery.Apply(BlobIntent(),UploadRecoveryEvent.BlobDeleted,image)));
Test("Unknown reserve outcome prevents abort completion",()=>Reject(()=>UploadRecovery.Apply(Abort(Intent()),UploadRecoveryEvent.FinishAbort)));
Test("Unknown put outcome prevents abort completion",()=>{var r=Abort(BlobIntent());r=UploadRecovery.Apply(r,UploadRecoveryEvent.Released,image);Reject(()=>UploadRecovery.Apply(r,UploadRecoveryEvent.FinishAbort));});
Test("Abort permits resolving a previously unknown reservation",()=>{var r=Abort(Intent());r=UploadRecovery.Apply(r,UploadRecoveryEvent.Reserved,image,Guid.NewGuid());Require(r.Effects[0].ReservationId is not null);});
Test("Abort prevents starting another external reservation",()=>Reject(()=>UploadRecovery.Apply(Abort(Prepared()),UploadRecoveryEvent.ReserveIntent,image)));
Test("Abort prevents starting another blob write",()=>Reject(()=>UploadRecovery.Apply(Abort(Reserved()),UploadRecoveryEvent.BlobIntent,image)));
Test("Abort completes only after release and deletion confirmations",()=>{
 var r=Abort(Stored());r=UploadRecovery.Apply(r,UploadRecoveryEvent.Released,image);r=UploadRecovery.Apply(r,UploadRecoveryEvent.BlobDeleted,image);
 r=UploadRecovery.Apply(r,UploadRecoveryEvent.FinishAbort);Require(r.Phase==UploadRecoveryPhase.Aborted);UploadRecovery.Validate(r);
});
Test("Retry after released credit advances generation and changes reservation/blob keys",()=>{
 var r=Abort(Stored());r=UploadRecovery.Apply(r,UploadRecoveryEvent.Released,image);r=UploadRecovery.Apply(r,UploadRecoveryEvent.BlobDeleted,image);r=UploadRecovery.Apply(r,UploadRecoveryEvent.FinishAbort);
 var next=UploadRecovery.Prepare(plan,owner,"aura-originals",r);
 Require(next.AttemptNo==2&&next.Effects[0].Generation==2&&next.Effects[0].ReserveKey!=r.Effects[0].ReserveKey);
 Require(next.Plan.Images[0].ObjectKey!=r.Plan.Images[0].ObjectKey&&next.Plan.Images[0].AnalysisId==image);
});
Test("Retry after confirmed no credit keeps generation one",()=>{
 var r=UploadRecovery.Apply(Intent(),UploadRecoveryEvent.CreditDenied,image);r=UploadRecovery.Apply(Abort(r),UploadRecoveryEvent.FinishAbort);
 var next=UploadRecovery.Prepare(plan,owner,"aura-originals",r);Require(next.Effects[0].Generation==1&&next.Effects[0].ReserveKey==r.Effects[0].ReserveKey);
});
Test("Retry cannot replace a pending attempt",()=>Reject(()=>UploadRecovery.Prepare(plan,owner,"aura-originals",Prepared())));
Test("Accepted requests can never authorize abort",()=>{var r=Stored() with{Phase=UploadRecoveryPhase.Accepted};UploadRecovery.Validate(r);Require(Abort(r)==r);Reject(()=>UploadRecovery.Apply(r,UploadRecoveryEvent.Released,image));});
Test("Already aborted request is terminal",()=>{var r=UploadRecovery.Apply(Abort(Prepared()),UploadRecoveryEvent.FinishAbort);Require(UploadRecovery.Apply(r,UploadRecoveryEvent.FinishAbort)==r);Reject(()=>UploadRecovery.Apply(r,UploadRecoveryEvent.BlobStored,image));});
Test("Unknown analysis and unknown event fail closed",()=>{Reject(()=>UploadRecovery.Apply(Prepared(),UploadRecoveryEvent.ReserveIntent,Guid.NewGuid()));Reject(()=>UploadRecovery.Apply(Prepared(),(UploadRecoveryEvent)999,image));});
Test("Unknown abort reason is rejected",()=>Reject(()=>UploadRecovery.Apply(Prepared(),UploadRecoveryEvent.BeginAbort,reason:"TIMEOUT_ASSUMED_ABORT")));
Test("Malformed accepted checkpoint fails closed",()=>Reject(()=>UploadRecovery.Validate(Prepared() with{Phase=UploadRecoveryPhase.Accepted})));
Test("Malformed aborted checkpoint fails closed",()=>Reject(()=>UploadRecovery.Validate(Abort(Intent()) with{Phase=UploadRecoveryPhase.Aborted})));
Test("Recovery JSON round-trip preserves keys, intents and original response",()=>{var r=Stored();var copy=JsonSerializer.Deserialize<UploadRecoveryRecord>(JsonSerializer.Serialize(r))!;UploadRecovery.Validate(copy);Require(copy.Effects[0]==r.Effects[0]&&copy.Plan.Response.BatchId==plan.BatchId);});
foreach(var mutation in new (string,Func<UploadRecoveryRecord,UploadRecoveryRecord>)[]{
 ("abort reason",r=>r with{AbortReason="invalid"}), ("attempt identity",r=>r with{AttemptId=Guid.NewGuid()}), ("empty owner",r=>r with{BillingOwner="ACCOUNT:"+Guid.Empty}),
 ("wrong billing owner",r=>r with{BillingOwner="ACCOUNT:"+Guid.NewGuid()}), ("wrong container",r=>r with{Container="public"}),
 ("fingerprint",r=>r with{Plan=r.Plan with{Fingerprint="bad"}}), ("tenant",r=>r with{Plan=r.Plan with{TenantId=Guid.NewGuid()}}),
 ("image object key",r=>r with{Plan=r.Plan with{Images=new[]{r.Plan.Images[0] with{ObjectKey="a/b/c"}}}}),
 ("reservation key",r=>r with{Effects=new[]{r.Effects[0] with{ReserveKey=Guid.NewGuid()}}}),
 ("generation",r=>r with{Effects=new[]{r.Effects[0] with{Generation=2}}}),
 ("reservation without intent",r=>r with{Effects=new[]{r.Effects[0] with{ReservationId=Guid.NewGuid()}}}),
 ("blob without intent",r=>r with{Effects=new[]{r.Effects[0] with{BlobConfirmed=true}}})
}){var captured=mutation;Test("Corrupt "+captured.Item1+" is rejected",()=>Reject(()=>UploadRecovery.Validate(captured.Item2(Prepared()))));}
var passed=0;foreach(var (name,run) in cases){try{run();passed++;Console.WriteLine("PASS: "+name);}catch(Exception ex){Console.Error.WriteLine("FAIL: "+name+" ("+ex.GetType().Name+")");return 1;}}
Console.WriteLine($"RESULT: {passed}/{cases.Count} upload recovery state checks passed; no PostgreSQL/Billing/storage runtime.");return 0;

static BatchPlan Fixture(){
 var tenant=Guid.NewGuid();var requester=Guid.NewGuid();var batch=Guid.NewGuid();var image=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
 var job=new JobPlan(Guid.NewGuid(),Guid.NewGuid(),"journal-check");
 return new(batch,tenant,requester,requester,Guid.NewGuid(),Guid.NewGuid(),new string('a',64),"1","journal-check",now,
 new(batch,new[]{new AcceptedItem(image,"synthetic.png","QUEUED",null)},now.UtcDateTime,$"/api/v1/batches/{batch:D}"),
 new[]{new ImagePlan(image,Guid.NewGuid(),$"{tenant:D}/{batch:D}/{image:D}","synthetic.png","image/png",100,10,10,new string('b',64),"FUNDUS","left","QUEUED",null,job)},
 new[]{new OutboxPlan(Guid.NewGuid(),image,"analysis.requested","0.6","{}",now),new OutboxPlan(job.MessageId,image,"analysis.job.run","0.6","{}",now)});
}
