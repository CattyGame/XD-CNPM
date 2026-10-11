# AURA — Kiểm chứng đồng bộ M2/M4 ngày 2026-10-11

## 1. Phạm vi

- Task liên quan: SCRUM-282, SCRUM-284, SCRUM-293.
- Nhánh: fix/sprint1-m2-m5-completion.
- HEAD: b2b3dde; working tree có thay đổi chưa commit.
- Không thay đổi Analysis/AI contract, fixtures, baseline hashes hoặc thuật toán.
- Nguồn kết quả: log PowerShell người thao tác cung cấp.

## 2. Tích hợp mã nguồn

git merge-base --is-ancestor xác nhận:
- Commit nguồn 131d640 nằm trong lịch sử HEAD.
- Merge PR #23 tại 3ea5ad9 nằm trong lịch sử HEAD.

[PR #23](https://github.com/CattyGame/XD-PMHDT/pull/23).

Không còn mô tả các sửa đổi này là chưa xác nhận merge.
Không dùng merge làm bằng chứng nghiệm thu toàn hệ thống.

## 3. Kết quả chạy lại

| Kiểm tra | Kết quả |
| --- | --- |
| AI OpenAPI YAML/JSON --check | Đồng bộ |
| AI mock pytest | 29 passed, 7 warnings |
| Analysis OpenAPI | 0.6.0 PASS |
| REST examples | 20 |
| Event examples | 4 |
| Job examples | 1 |
| Embedded example checks | 54 PASS |
| Negative checks | PASS |
| LF-normalized SHA256 | 31 PASS |

Các lệnh:
- src/ai-core/scripts/sync_openapi_contracts.py --check
- python -m pytest src/ai-core/tests -q
- scripts/validate_analysis_contract.py

Môi trường Python:
.venv-dataset cho AI; .venv-contract cho validator.

## 4. Cảnh báo và giới hạn

Còn Starlette/httpx, tên hằng HTTP 422 và jsonschema.RefResolver deprecated.
Chưa nâng dependency hoặc ghi rằng các cảnh báo đã được sửa.

Kết quả chỉ bao phủ contract/examples/schema và AI mock.
Không chứng minh backend authorization, Worker–AI, NiFi E2E,
camera thật, chất lượng AI lâm sàng hoặc NFR toàn sản phẩm.

M2 bàn giao contract/thiết kế không đồng nghĩa Analysis backend đã triển khai.
M4 bàn giao mock/PoC/offline không đồng nghĩa pipeline sản phẩm đã hoàn chỉnh.

Review và ký duyệt của thành viên vẫn cần bằng chứng thực tế;
tài liệu này không tự chuyển trạng thái Jira.

## 5. Tài liệu liên quan

- [Contract baseline](../../contracts/BASELINE-W1-v0.2.md).
- [AI contract](../api/ai_service_contract.md).
- [Bàn giao M4](m4_m3_verification_handover.md).
- [RTM](../requirements/requirements_traceability_matrix_v0.1.md).
