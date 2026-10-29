#!/usr/bin/env python3
"""Kiem tra OutboxSql.cs tren PostgreSQL that trong container Docker (chay duoc tren Windows).

Dung (PowerShell, tai thu muc goc repo):
  python infra\\postgres\\migrations\\analysis\\tests\\outbox_sql_test.py --container aura-postgres-1 --db s3_outbox_test `
     --f001 infra\\postgres\\migrations\\analysis\\001_analysis_core.sql `
     --f002 infra\\postgres\\migrations\\analysis\\002_outbox_publisher.sql `
     --cs src\\services\\AURA.Analysis.Api\\Outbox\\OutboxSql.cs

AN TOAN:
  - Ten database BAT BUOC chua chu "test". Script TAO moi database do (xoa neu da ton tai) va XOA no khi xong.
  - Khong dung vao database khac (aura_dev...). Role aura_analysis_publisher neu do script tao ra cung bi xoa khi xong.
  - Dung user/mat khau cua container (bien POSTGRES_USER ben trong container), khong can nhap mat khau.
Exit code 1 neu co FAIL."""
import argparse, re, subprocess, sys, threading, time, uuid

ap = argparse.ArgumentParser()
ap.add_argument("--container", required=True); ap.add_argument("--db", required=True)
ap.add_argument("--f001", required=True); ap.add_argument("--f002", required=True); ap.add_argument("--cs", required=True)
ap.add_argument("--keep", action="store_true", help="khong xoa database/role sau khi chay (de xem lai)")
a = ap.parse_args()
if "test" not in a.db.lower() or not re.fullmatch(r"[A-Za-z0-9_]+", a.db):
    sys.exit("TU CHOI: --db phai chi gom chu/so/_ va chua chu 'test' (script se xoa database nay).")

PSQL = 'psql -U "$POSTGRES_USER" -d %s -v ON_ERROR_STOP=1 -X -q -t -A -F "|"'
def dexec(db, stdin_text, extra=""):
    cmd = ["docker", "exec", "-i", a.container, "sh", "-c", (PSQL % db) + extra]
    r = subprocess.run(cmd, input=stdin_text, capture_output=True, text=True, encoding="utf-8")
    if r.returncode: raise RuntimeError((r.stderr or r.stdout).strip())
    return [l for l in r.stdout.strip().split("\n") if l]
def psql(sql): return dexec(a.db, sql)
def admin(sql): return dexec("postgres", sql)
def run_file(p): dexec(a.db, open(p, encoding="utf-8").read())

src = open(a.cs, encoding="utf-8").read()
def sql(name): return re.search(r'const string %s = """\n(.*?)\n\s*""";' % name, src, re.S).group(1)
def sub(q, **kw):
    for k, v in kw.items(): q = re.sub(r'@%s\b' % k, v, q)
    assert "@" not in re.sub(r"'[^']*'", "", q), q
    return q
ts = lambda s: "'%s'::timestamptz" % s
res = []
def check(n, ok): res.append((n, ok)); print(("PASS " if ok else "FAIL ") + n, flush=True)

role_existed = admin("SELECT 1 FROM pg_roles WHERE rolname='aura_analysis_publisher'") != []
admin("DROP DATABASE IF EXISTS %s WITH (FORCE)" % a.db)
admin("CREATE DATABASE %s" % a.db)
try:
    run_file(a.f001); run_file(a.f002); run_file(a.f002)  # chay 2 lan: idempotent
    tenant, other = str(uuid.uuid4()), str(uuid.uuid4())
    def has_col(c): return psql("select 1 from information_schema.columns where table_schema='analysis' and table_name='outbox_events' and column_name='%s'" % c)
    check("002 them needs_attention_at, last_error_at", bool(has_col("needs_attention_at")) and bool(has_col("last_error_at")))
    def insert(ev, t, at, typ="analysis.requested"):
        return ("INSERT INTO analysis.outbox_events(id,tenant_id,analysis_request_id,image_id,event_type,schema_version,payload,occurred_at) "
                "VALUES('%s','%s','%s','%s','%s','0.7','{\"correlationId\":\"c-%s\"}','%s');" % (ev, t, uuid.uuid4(), uuid.uuid4(), typ, ev[:4], at))
    ids = {k: str(uuid.uuid4()) for k in "abcde"}
    psql("SET session_replication_role = replica;\n" + "\n".join([
        insert(ids["a"], tenant, "2026-10-28T02:00:00Z"), insert(ids["b"], tenant, "2026-10-28T02:00:01Z"),
        insert(ids["c"], other, "2026-10-28T02:00:02Z"), insert(ids["d"], tenant, "2026-09-01T00:00:00Z"),
        insert(ids["e"], tenant, "2026-10-28T02:00:03Z")]))
    P = dict(now=ts("2026-10-28T03:00:00Z"), min_occurred_at=ts("2026-10-27T00:00:00Z"),
             excluded_tenants="ARRAY['%s']::uuid[]" % other, batch_size="2", lease_seconds="60", max_attempts="10")
    claim = lambda **o: psql(sub(sql("Claim"), **{**P, **o}))
    r1 = claim(); got = [x.split("|")[0] for x in r1]
    check("claim lay dung 2 record cu nhat, bo tenant bi loai va record truoc moc", got and set(got) <= {ids["a"], ids["b"], ids["e"]} and len(got) == 2 and ids["c"] not in got and ids["d"] not in got)
    check("claim tang attempts=1", all(x.split("|")[-1] == "1" for x in r1))
    r2 = claim()
    check("claim lan 2 (lease con han) khong lay lai record da claim", not (set(x.split("|")[0] for x in r2) & set(got)) and len(r2) == 1)
    check("claim lan 3 khong con gi", claim() == [])
    r4 = claim(now=ts("2026-10-28T03:02:00Z"))
    check("het lease: claim lai, attempts tang len 2, cung id", any(x.split("|")[-1] == "2" for x in r4) and set(got) <= set(x.split("|")[0] for x in r4))
    psql("UPDATE analysis.outbox_events SET locked_until=NULL, delivery_attempts=0, next_publish_at=NULL")
    def holder():
        dexec(a.db, "BEGIN; SELECT id FROM analysis.outbox_events WHERE id='%s' FOR UPDATE; SELECT pg_sleep(4); ROLLBACK;" % ids["a"])
    th = threading.Thread(target=holder); th.start(); time.sleep(2.5)
    r5 = [x.split("|")[0] for x in claim(batch_size="10")]; th.join()
    check("SKIP LOCKED: record dang bi khoa boi phien khac khong bi claim/cho", ids["a"] not in r5 and len(r5) >= 1)
    psql("UPDATE analysis.outbox_events SET locked_until=NULL, delivery_attempts=0")
    mp = lambda i: psql(sub(sql("MarkPublished"), at=ts("2026-10-28T03:05:00Z"), id="'%s'" % i) + " RETURNING 1")
    check("MarkPublished lan dau cap nhat 1 dong", len(mp(ids["a"])) == 1)
    check("MarkPublished lan hai khong cap nhat (idempotent)", len(mp(ids["a"])) == 0)
    check("record da published khong duoc claim", ids["a"] not in [x.split("|")[0] for x in claim(now=ts("2026-10-29T00:00:00Z"), batch_size="10")])
    psql(sub(sql("MarkRetry"), code="'BROKER_NACK'", at=ts("2026-10-28T03:06:00Z"), next=ts("2026-10-28T03:10:00Z"), id="'%s'" % ids["b"]))
    rb = [x.split("|")[0] for x in claim(now=ts("2026-10-28T03:07:00Z"), batch_size="10")]
    check("MarkRetry: chua den next_publish_at thi khong claim", ids["b"] not in rb)
    rb2 = [x.split("|")[0] for x in claim(now=ts("2026-10-28T03:11:00Z"), batch_size="10")]
    check("MarkRetry: den han thi claim lai", ids["b"] in rb2)
    psql(sub(sql("MarkNeedsAttention"), code="'X'", at=ts("2026-10-28T03:12:00Z"), id="'%s'" % ids["b"]))
    check("NEEDS_ATTENTION khong bi claim va record van ton tai",
          ids["b"] not in [x.split("|")[0] for x in claim(now=ts("2026-10-30T00:00:00Z"), batch_size="10")]
          and psql("select count(*) from analysis.outbox_events where id='%s'" % ids["b"]) == ["1"])
    try:
        psql("UPDATE analysis.outbox_events SET published_at=now(), needs_attention_at=now() WHERE id='%s'" % ids["e"]); ok = False
    except RuntimeError as e: ok = "ck_outbox_final_state" in str(e)
    check("constraint ck_outbox_final_state chan vua published vua NEEDS_ATTENTION", ok)
    psql("UPDATE analysis.outbox_events SET delivery_attempts=10, locked_until='2026-10-28T03:00:00Z', needs_attention_at=NULL WHERE id='%s'" % ids["e"])
    psql(sub(sql("SweepExhausted"), now=ts("2026-10-28T04:00:00Z"), max_attempts="10"))
    check("Sweep: attempts>=10 va het lease -> NEEDS_ATTENTION", psql("select needs_attention_at is not null from analysis.outbox_events where id='%s'" % ids["e"]) == ["t"])
    psql("UPDATE analysis.outbox_events SET delivery_attempts=3, locked_until='2099-01-01' WHERE id='%s'" % ids["c"])
    psql(sub(sql("ReleaseUnattempted"), ids="ARRAY['%s']::uuid[]" % ids["c"]))
    check("ReleaseUnattempted giam attempts 3->2 va xoa lease", psql("select delivery_attempts||'|'||(locked_until is null) from analysis.outbox_events where id='%s'" % ids["c"]) == ["2|true"])
    bl = psql(sub(sql("Backlog"), now=ts("2026-10-28T05:00:00Z"), min_occurred_at=ts("2026-10-27T00:00:00Z"), excluded_tenants="ARRAY[]::uuid[]"))
    check("Backlog chay duoc va dem NEEDS_ATTENTION >= 2", bool(bl) and int(bl[0].split("|")[3]) >= 2)
    check("view v_outbox_status tra cuu theo correlationId, state PUBLISHED", psql("select state from analysis.v_outbox_status where correlation_id='c-%s'" % ids["a"][:4]) == ["PUBLISHED"])
    admin("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='aura_analysis_publisher') THEN CREATE ROLE aura_analysis_publisher NOLOGIN; END IF; END $$;")
    run_file(a.f002)
    def as_pub(q):
        try: psql("SET ROLE aura_analysis_publisher; " + q); return True
        except RuntimeError: return False
    check("publisher: UPDATE cot last_error_code duoc", as_pub("UPDATE analysis.outbox_events SET last_error_code='X' WHERE false"))
    check("publisher: KHONG sua duoc payload", not as_pub("UPDATE analysis.outbox_events SET payload='{}' WHERE false"))
    check("publisher: KHONG INSERT/DELETE", not as_pub("DELETE FROM analysis.outbox_events WHERE false") and not as_pub("INSERT INTO analysis.outbox_events(id) VALUES (gen_random_uuid())"))
    check("publisher: KHONG doc bang khac", not as_pub("SELECT 1 FROM analysis.images LIMIT 1"))
finally:
    if not a.keep:
        try:
            admin("DROP DATABASE IF EXISTS %s WITH (FORCE)" % a.db)
            if not role_existed: admin("DROP ROLE IF EXISTS aura_analysis_publisher")
            print("Da don dep: xoa database %s%s" % (a.db, "" if role_existed else " va role aura_analysis_publisher"))
        except RuntimeError as e: print("CANH BAO don dep that bai:", e)
bad = [n for n, o in res if not o]
print("TOTAL pass=%d fail=%d" % (len(res) - len(bad), len(bad))); sys.exit(1 if bad or not res else 0)
