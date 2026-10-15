-- Kiem thu rang buoc cho 001_analysis_core.sql. Moi test: ket qua mong doi OK hoac LOI (ten constraint).
\set ON_ERROR_STOP off
\set VERBOSITY terse
\echo '--- chuan bi: tenant A, B'
\set ta '11111111-1111-1111-1111-111111111111'
\set tb '22222222-2222-2222-2222-222222222222'
\set u  '33333333-3333-3333-3333-333333333333'
\set h  'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
INSERT INTO analysis.analysis_requests (id,tenant_id,owner_id,requester_id,patient_id,idempotency_key,request_fingerprint,request_schema_version,accepted_response,correlation_id)
VALUES ('a0000000-0000-0000-0000-000000000001',:'ta',:'u',:'u',:'u','b0000000-0000-0000-0000-000000000001',:'h','1','{}','c1');
\echo 'T1 mong doi LOI uq_analysis_requests_idempotency (cung tenant/requester/key)'
INSERT INTO analysis.analysis_requests (id,tenant_id,owner_id,requester_id,patient_id,idempotency_key,request_fingerprint,request_schema_version,accepted_response,correlation_id)
VALUES ('a0000000-0000-0000-0000-000000000002',:'ta',:'u',:'u',:'u','b0000000-0000-0000-0000-000000000001',:'h','1','{}','c2');
\echo 'T2 mong doi OK (cung key, tenant khac)'
INSERT INTO analysis.analysis_requests (id,tenant_id,owner_id,requester_id,patient_id,idempotency_key,request_fingerprint,request_schema_version,accepted_response,correlation_id)
VALUES ('a0000000-0000-0000-0000-000000000003',:'tb',:'u',:'u',:'u','b0000000-0000-0000-0000-000000000001',:'h','1','{}','c3');
\echo 'T3 mong doi LOI ck_analysis_requests_fingerprint'
INSERT INTO analysis.analysis_requests (id,tenant_id,owner_id,requester_id,patient_id,idempotency_key,request_fingerprint,request_schema_version,accepted_response,correlation_id)
VALUES ('a0000000-0000-0000-0000-000000000004',:'ta',:'u',:'u',:'u','b0000000-0000-0000-0000-000000000004','xyz','1','{}','c4');
\echo 'T4 mong doi OK image hop le'
INSERT INTO analysis.images (id,tenant_id,analysis_request_id,file_id,modality,eye_side,object_key,mime_type,size_bytes,width,height,sha256,processing_status)
VALUES ('d0000000-0000-0000-0000-000000000001',:'ta','a0000000-0000-0000-0000-000000000001','e0000000-0000-0000-0000-000000000001','FUNDUS','left','k/1','image/jpeg',1000,100,100,:'h','QUEUED');
\echo 'T5 mong doi LOI fk_images_request (batch thuoc tenant khac)'
INSERT INTO analysis.images (id,tenant_id,analysis_request_id,file_id,modality,eye_side,object_key,mime_type,size_bytes,width,height,sha256,processing_status)
VALUES ('d0000000-0000-0000-0000-000000000002',:'tb','a0000000-0000-0000-0000-000000000001','e0000000-0000-0000-0000-000000000002','FUNDUS','left','k/2','image/jpeg',1000,100,100,:'h','QUEUED');
\echo 'T6 mong doi LOI ck_images_modality (OCT)'
INSERT INTO analysis.images (id,tenant_id,analysis_request_id,file_id,modality,eye_side,object_key,mime_type,size_bytes,width,height,sha256,processing_status)
VALUES ('d0000000-0000-0000-0000-000000000003',:'ta','a0000000-0000-0000-0000-000000000001','e0000000-0000-0000-0000-000000000003','OCT','left','k/3','image/jpeg',1000,100,100,:'h','QUEUED');
\echo 'T7 mong doi LOI ck_images_duplicate_ref (DUPLICATE thieu existing_analysis_id)'
INSERT INTO analysis.images (id,tenant_id,analysis_request_id,file_id,modality,eye_side,object_key,mime_type,size_bytes,width,height,sha256,processing_status)
VALUES ('d0000000-0000-0000-0000-000000000004',:'ta','a0000000-0000-0000-0000-000000000001','e0000000-0000-0000-0000-000000000004','FUNDUS','left','k/4','image/jpeg',1000,100,100,:'h','DUPLICATE');
\echo 'T8 mong doi OK DUPLICATE co existing_analysis_id'
INSERT INTO analysis.images (id,tenant_id,analysis_request_id,file_id,modality,eye_side,object_key,mime_type,size_bytes,width,height,sha256,processing_status,existing_analysis_id)
VALUES ('d0000000-0000-0000-0000-000000000005',:'ta','a0000000-0000-0000-0000-000000000001','e0000000-0000-0000-0000-000000000005','FUNDUS','left','k/5','image/jpeg',1000,100,100,:'h','DUPLICATE','d0000000-0000-0000-0000-000000000001');
\echo 'T9 mong doi LOI ck_images_mime (gif)'
INSERT INTO analysis.images (id,tenant_id,analysis_request_id,file_id,modality,eye_side,object_key,mime_type,size_bytes,width,height,sha256,processing_status)
VALUES ('d0000000-0000-0000-0000-000000000006',:'ta','a0000000-0000-0000-0000-000000000001','e0000000-0000-0000-0000-000000000006','FUNDUS','left','k/6','image/gif',1000,100,100,:'h','QUEUED');
\echo 'T10 mong doi LOI uq_images_object_key'
INSERT INTO analysis.images (id,tenant_id,analysis_request_id,file_id,modality,eye_side,object_key,mime_type,size_bytes,width,height,sha256,processing_status)
VALUES ('d0000000-0000-0000-0000-000000000007',:'ta','a0000000-0000-0000-0000-000000000001','e0000000-0000-0000-0000-000000000007','FUNDUS','left','k/1','image/png',1000,100,100,:'h','QUEUED');
\echo 'T11 mong doi OK job attempt 1'
INSERT INTO analysis.jobs (id,tenant_id,image_id,message_id,attempt_no,status,correlation_id)
VALUES ('f0000000-0000-0000-0000-000000000001',:'ta','d0000000-0000-0000-0000-000000000001','90000000-0000-0000-0000-000000000001',1,'QUEUED','c1');
\echo 'T12 mong doi LOI uq_jobs_image_attempt'
INSERT INTO analysis.jobs (id,tenant_id,image_id,message_id,attempt_no,status,correlation_id)
VALUES ('f0000000-0000-0000-0000-000000000002',:'ta','d0000000-0000-0000-0000-000000000001','90000000-0000-0000-0000-000000000002',1,'QUEUED','c1');
\echo 'T13 mong doi LOI uq_jobs_message_id'
INSERT INTO analysis.jobs (id,tenant_id,image_id,message_id,attempt_no,status,correlation_id)
VALUES ('f0000000-0000-0000-0000-000000000003',:'ta','d0000000-0000-0000-0000-000000000001','90000000-0000-0000-0000-000000000001',2,'QUEUED','c1');
\echo 'T14 mong doi OK retry attempt 2'
INSERT INTO analysis.jobs (id,tenant_id,image_id,message_id,attempt_no,status,correlation_id)
VALUES ('f0000000-0000-0000-0000-000000000004',:'ta','d0000000-0000-0000-0000-000000000001','90000000-0000-0000-0000-000000000004',2,'QUEUED','c1');
\echo 'T15 mong doi LOI fk_jobs_image (job tenant B tro anh tenant A)'
INSERT INTO analysis.jobs (id,tenant_id,image_id,message_id,attempt_no,status,correlation_id)
VALUES ('f0000000-0000-0000-0000-000000000005',:'tb','d0000000-0000-0000-0000-000000000001','90000000-0000-0000-0000-000000000005',3,'QUEUED','c1');
\echo 'T16 mong doi LOI ck_jobs_status'
INSERT INTO analysis.jobs (id,tenant_id,image_id,message_id,attempt_no,status,correlation_id)
VALUES ('f0000000-0000-0000-0000-000000000006',:'ta','d0000000-0000-0000-0000-000000000001','90000000-0000-0000-0000-000000000006',3,'SUCCESS','c1');
\echo 'T17 mong doi OK result v1'
INSERT INTO analysis.analysis_results (id,tenant_id,image_id,job_id,version,result_schema_version,risk_assessment,metrics,is_mock,model_version,config_version,threshold_version,processing_time_ms,limitations)
VALUES ('70000000-0000-0000-0000-000000000001',:'ta','d0000000-0000-0000-0000-000000000001','f0000000-0000-0000-0000-000000000001',1,'0.6','{}','{}',true,'m','c','t',10,'l');
\echo 'T18 mong doi LOI uq_results_job (1 attempt toi da 1 ket qua)'
INSERT INTO analysis.analysis_results (id,tenant_id,image_id,job_id,version,result_schema_version,risk_assessment,metrics,is_mock,model_version,config_version,threshold_version,processing_time_ms,limitations)
VALUES ('70000000-0000-0000-0000-000000000002',:'ta','d0000000-0000-0000-0000-000000000001','f0000000-0000-0000-0000-000000000001',2,'0.6','{}','{}',true,'m','c','t',10,'l');
\echo 'T19 mong doi LOI uq_results_image_version'
INSERT INTO analysis.analysis_results (id,tenant_id,image_id,job_id,version,result_schema_version,risk_assessment,metrics,is_mock,model_version,config_version,threshold_version,processing_time_ms,limitations)
VALUES ('70000000-0000-0000-0000-000000000003',:'ta','d0000000-0000-0000-0000-000000000001','f0000000-0000-0000-0000-000000000004',1,'0.6','{}','{}',true,'m','c','t',10,'l');
\echo 'T20 mong doi OK outbox'
INSERT INTO analysis.outbox_events (id,tenant_id,analysis_request_id,image_id,event_type,schema_version,payload,occurred_at,next_publish_at)
VALUES ('80000000-0000-0000-0000-000000000001',:'ta','a0000000-0000-0000-0000-000000000001','d0000000-0000-0000-0000-000000000001','analysis.requested','0.6','{"a":1}',now(),now());
\echo 'T21 mong doi LOI fk_outbox_image (anh tenant B khong ton tai)'
INSERT INTO analysis.outbox_events (id,tenant_id,analysis_request_id,image_id,event_type,schema_version,payload,occurred_at)
VALUES ('80000000-0000-0000-0000-000000000002',:'tb','a0000000-0000-0000-0000-000000000003','d0000000-0000-0000-0000-000000000001','analysis.requested','0.6','{}',now());
\echo 'T22 mong doi LOI ck_reviews_correction (hieu chinh thieu ly do)'
INSERT INTO analysis.doctor_reviews (id,tenant_id,image_id,analysis_result_id,doctor_id,version,base_version,is_approved,corrected_risk_level)
VALUES ('60000000-0000-0000-0000-000000000001',:'ta','d0000000-0000-0000-0000-000000000001','70000000-0000-0000-0000-000000000001',:'u',1,0,false,'HIGH');
\echo 'T23 mong doi OK review xac nhan v1'
INSERT INTO analysis.doctor_reviews (id,tenant_id,image_id,analysis_result_id,doctor_id,version,base_version,is_approved)
VALUES ('60000000-0000-0000-0000-000000000002',:'ta','d0000000-0000-0000-0000-000000000001','70000000-0000-0000-0000-000000000001',:'u',1,0,true);
\echo 'T24 mong doi LOI uq_reviews_image_version'
INSERT INTO analysis.doctor_reviews (id,tenant_id,image_id,analysis_result_id,doctor_id,version,base_version,is_approved)
VALUES ('60000000-0000-0000-0000-000000000003',:'ta','d0000000-0000-0000-0000-000000000001','70000000-0000-0000-0000-000000000001',:'u',1,0,true);
\echo 'T25 mong doi OK annotation MASK; T26 LOI uq_annotation_result_type (MASK lan 2)'
INSERT INTO analysis.annotation_refs (id,tenant_id,image_id,analysis_result_id,file_id,type,object_key,mime_type,size_bytes,width,height,sha256)
VALUES ('50000000-0000-0000-0000-000000000001',:'ta','d0000000-0000-0000-0000-000000000001','70000000-0000-0000-0000-000000000001','51000000-0000-0000-0000-000000000001','MASK','a/1','image/png',10,100,100,:'h');
INSERT INTO analysis.annotation_refs (id,tenant_id,image_id,analysis_result_id,file_id,type,object_key,mime_type,size_bytes,width,height,sha256)
VALUES ('50000000-0000-0000-0000-000000000002',:'ta','d0000000-0000-0000-0000-000000000001','70000000-0000-0000-0000-000000000001','51000000-0000-0000-0000-000000000002','MASK','a/2','image/png',10,100,100,:'h');
\echo 'T27 mong doi OK audit'
INSERT INTO analysis.audit_logs (id,tenant_id,actor_type,action,target_type,outcome,correlation_id)
VALUES ('40000000-0000-0000-0000-000000000001',:'ta','User','analysis.read','Analysis','DENIED','c1');
\echo 'T28 mong doi LOI ck_audit_outcome'
INSERT INTO analysis.audit_logs (id,tenant_id,actor_type,action,target_type,outcome,correlation_id)
VALUES ('40000000-0000-0000-0000-000000000002',:'ta','User','analysis.read','Analysis','MAYBE','c1');
\echo 'T29 mong doi OK: index partial outbox duoc dung / kiem tra ton tai'
SELECT indexname FROM pg_indexes WHERE schemaname='analysis' AND indexdef LIKE '%WHERE (published_at IS NULL)%';
\echo 'T30 so bang trong schema analysis (mong doi 8)'
SELECT count(*) FROM information_schema.tables WHERE table_schema='analysis';
