# Báo cáo Kiểm thử Tải AI Core (Stress Test), Tối ưu Hiệu năng Frangi v1.3 & Bàn giao Contract 1.3

> **Dự án**: AURA (`SP26SE025`) · **Sprint**: 4–5 Kết hợp
> **Thành viên thực hiện**: Đào Duy Quân (`M4 — AI Core Lead`)
> **Mã công việc Jira**:
> - `SCRUM-351` (`W45-M4-02` — P0): Tối ưu hóa Phân vùng Frangi v0.2 (`< 1.8s`) & Triển khai Heuristic Risk Model (`tortuosity.py`)
> - `SCRUM-352` (`W45-M4-03` — P0): Đóng gói AI Contract `1.3.0`, Middleware `X-Correlation-Id` & Khảo sát khả thi ảnh cắt lớp `OCT`
> - `SCRUM-353` (`W45-M4-04` — P1): Kiểm thử Tải AI Core (Stress Test `5–10` concurrent requests, `100` lượt suy luận), Đo đạc Benchmark `v1.3` & Hoàn thiện Model Card
> **Ngày thực hiện**: `2026-10-30` · **Nhánh Git**: `feature/W45-M4-02-frangi-optimization`
> **Phạm vi**: Thực hiện trọn vẹn Task 2 -> Task 4 của M4 theo phân công của M1 (chừa lại Task 1 `SCRUM-350` / `W45-M4-01` về Apache NiFi để phối hợp hạ tầng riêng).

---

## 1. Bảng Tổng hợp Kết quả Nghiệm thu (Acceptance Criteria Matrix)

| Mã Task / AC | Yêu cầu nghiệm thu (DoD) | Kết quả đo thực tế | Trạng thái |
|---|---|---|---|
| **W45-M4-02 · AC1** | Benchmark trên 6 ảnh test `CHASE_DB1` (`999x960`) giảm độ trễ trung bình xuống `< 1.8s` trên CPU; Mask & Overlay khớp `100%` so với baseline | `mean = 1095.20 ms`, `median = 1097.25 ms`, `max = 1130.50 ms` (giảm `56.40%` so với `2516.41 ms`); `Dice vs Sprint 3 = 1.000000` (`0` pixel lệch) | **PASS** |
| **W45-M4-02 · AC2** | Trả về đầy đủ `vessel_density`, `tortuosity_index` và `risk_assessment` sơ bộ có nhãn `disclaimer` rõ ràng; ảnh xấu ngắt sớm trả `422 LOW_QUALITY_IMAGE` | Đã tích hợp `tortuosity.py` + `evaluate_heuristic_risk` kèm `HEURISTIC_DISCLAIMER`; quality gate ngắt trước khi chạy Frangi/Risk | **PASS** |
| **W45-M4-02 · AC3** | Toàn bộ unit tests của AI Core pass `100%`, RAM giải phóng sạch sẽ sau mỗi lượt | `110 passed` trong `pytest src/ai-core/tests -q`; `tracemalloc` delta `+0.0067 MiB` sau 20 lượt | **PASS** |
| **W45-M4-03 · AC1** | Header `X-Correlation-Id` được echo chính xác trong mọi response kể cả lỗi `400`, `401`, `403`, `422`, `500`, `503` | `correlation_id_middleware` bọc `try/except Exception` đảm bảo luôn gắn `X-Correlation-Id` kể cả khi xảy ra lỗi `500` nội bộ | **PASS** |
| **W45-M4-03 · AC2** | Schema response khớp `100%` với JSON Schema `1.3`; tương thích ngược an toàn cho Worker `M2` | Toàn bộ 6 ảnh test + unit tests được validate trực tiếp bằng `Draft202012Validator` với `contracts/ai-response-1.3.schema.json` | **PASS** |
| **W45-M4-03 · AC3** | Tài liệu khảo sát kỹ thuật `OCT` chỉ rõ phương án tiếp nhận dữ liệu, ngân sách GPU/VRAM và lộ trình Sprint 6 | Bàn giao `docs/ai/oct_modality_feasibility_study_v1.0.md` (phân tích DICOM/TIFF/PNG, 2.5D ResNet vs 3D U-Net, Claim-Check PHI-free) | **PASS** |
| **W45-M4-04 · AC1** | Endpoint chịu tải `10` request đồng thời không crash Uvicorn, tỷ lệ lỗi `0%` | Bắn `100` request ảnh `999x960` thật ở mức đồng thời `5` và `10` vào tiến trình Uvicorn độc lập: **`100/100` HTTP 200 (`0.0%` lỗi)**, `/ready` sau tải đạt `200 OK` | **PASS** |
| **W45-M4-04 · AC2** | Dung lượng RAM duy trì ổn định, không rò rỉ bộ nhớ (Memory Leak) sau `100` lượt suy luận | RSS Uvicorn từ `95.30 MiB` (nền) -> đỉnh `1038.38 MiB` (10 luồng song song) -> **hạ ngay về `100.72 MiB`** sau 100 lượt (`+5.42 MiB`), `memory_leak_detected = false` | **PASS** |
| **W45-M4-04 · AC3** | Báo cáo Benchmark JSON `v1.3` và Model Card `v1.3` phản ánh trung thực toàn bộ thông số đo thực tế | Bàn giao `docs/benchmarks/frangi_runtime_api_v1.3.json` và `docs/data_cards/model_card_v1.3.md` | **PASS** |

---

## 2. Phương pháp Kiểm thử & Cấu hình Môi trường Đo

### 2.1. Cấu hình Phần cứng & Phần mềm
* **Hệ điều hành**: `Windows-11-10.0.26300-SP0` (`8` logical CPU cores)
* **Runtime**: `Python 3.12.14`, `Uvicorn ASGI Server` (chạy dưới dạng tiến trình con độc lập `subprocess.Popen` tách biệt hoàn toàn khỏi tiến trình bắn tải HTTP client để không tranh chấp GIL giữa client và server)
* **Thư viện tính toán**: `NumPy 2.5.3`, `SciPy 1.18.1`, `scikit-image 0.26.0`, `Pillow 12.3.0`
* **Cấu hình AI Core**: `AURA_AI_ENGINE=frangi`, `AURA_ALLOW_MOCK=false`, `AURA_FRANGI_CONFIG_PATH=src/ai-core/config/ai-frangi-v1.3.yaml` (`riskAvailable: true`), xác thực đầy đủ **RS256 Service JWT** (`TTL = 240s <= 300s`).

### 2.2. Kịch bản Kiểm thử 3 Giai đoạn (`src/ai-core/scripts/stress_test_ai_core.py`)
1. **Giai đoạn 1 — Benchmark Đơn luồng Chuẩn xác (`6` ảnh tập `test` của `CHASE_DB1`, `999 x 960`)**:
   * Sau 2 request warm-up khởi tạo cache FFT/SciPy, gửi tuần tự 6 ảnh `Image_01L/R`, `Image_02L/R`, `Image_11L/R` qua HTTP `POST /api/v1/analyze` kèm `X-Correlation-Id`.
   * Kiểm chứng schema request/response theo `ai-request-1.3.schema.json` và `ai-response-1.3.schema.json`, giải mã PNG mask trả về và so sánh từng pixel với mask chuẩn Sprint 3 (`compute_reference_s3_mask`) cùng nhãn chuyên gia `_1stHO.png`.
2. **Giai đoạn 2 — Stress Test Đa luồng Thực tế trên Tiến trình Uvicorn (`100` lượt suy luận ảnh lớn `999 x 960`)**:
   * **Đợt 1 (`Wave 1`)**: Bắn `20` request suy luận song song với **`5` luồng đồng thời (`concurrency = 5`)**.
   * **Đợt 2 (`Wave 2`)**: Bắn `80` request suy luận song song với **`10` luồng đồng thời (`concurrency = 10`)**.
   * Một luồng giám sát độc lập lấy mẫu định kỳ mỗi `50 ms` trên toàn bộ cây tiến trình Uvicorn thông qua Win32 API (`CreateToolhelp32Snapshot`, `GetProcessMemoryInfo`, `GetProcessTimes`) / Linux `/proc/<pid>` để thu thập chính xác **Working Set RSS**, **OS Peak Working Set (High-Water Mark)** và **Kernel + User CPU Seconds** của riêng tiến trình server.
3. **Giai đoạn 3 — Kiểm toán Cấp phát Bộ nhớ Python & Garbage Collector (`20` lượt liên tiếp dưới `tracemalloc`)**:
   * Chạy `20` lượt `process_frangi_analysis` liên tiếp trên ảnh `999 x 960`, gọi `gc.collect()` và đo lượng heap giữ lại ròng (`tracemalloc_retained_delta_mb`) cùng thống kê các thế hệ GC (`gen0`, `gen1`, `gen2`).

---

## 3. Phân tích Chi tiết Kết quả Hiệu năng & Tài nguyên

### 3.1. Phân tích Tối ưu Độ trễ (`2516.41 ms` -> `1097.25 ms`, giảm `56.40%`)
Ba cải tiến kỹ thuật đóng góp trực tiếp vào việc kéo độ trễ tổng thể xuống dưới `1.10 giây/ảnh` (ngay cả khi đã cộng thêm bước trích xuất khung xương `tortuosity` + đánh giá `risk_assessment`):
1. **Rút gọn tích chập Hessian 2D tách chiều từ 10 pass xuống 6 pass/tỷ lệ** và tái sử dụng bộ đệm `float64` tại chỗ (`~2500 ms` -> `~930 ms`).
2. **Tối ưu trích xuất độ cong queo khung xương sang $O(N)$ vector hóa** (`np.nonzero` + `np.argsort` + `np.bincount` cạnh trực giao/chéo), giảm thời gian tính `tortuosity_index` trên ảnh `999 x 960` xuống chỉ còn `~58 ms`.
3. **Tối ưu nén PNG Lossless (`compress_level=1`) & co vùng nội tiếp tách chiều (`minimum_filter` 1D)**, giảm bước hậu xử lý mã hóa ảnh mask + overlay từ `~578 ms` xuống `~59 ms` mà không làm thay đổi bất kỳ giá trị pixel nào sau giải mã.

### 3.2. Phân tích Hồ sơ CPU, RAM & Garbage Collection dưới Tải Đồng thời

| Thông số Hệ thống (Tiến trình Uvicorn) | Số liệu đo được | Giải thích kỹ thuật |
|---|---:|---|
| **Wave 1 (`5` luồng đồng thời) — Median HTTP Roundtrip** | `1782.01 ms` | `5` luồng chia sẻ `8` logical cores mượt mà, độ trễ trung vị vẫn `< 1.8s` |
| **Wave 2 (`10` luồng đồng thời) — Median / P95 HTTP Roundtrip** | `3423.80 ms` / `6272.61 ms` | `10` luồng bão hòa CPU (`4.19` nhân thực hoạt động liên tục), xếp hàng điều độ qua Starlette threadpool không nghẽn event loop |
| **Thông lượng xử lý tổng thể (`Throughput`)** | `2.511 ảnh/giây` (`100` ảnh trong `39.82 s`) | Đáp ứng dư dả bài toán Batch Processing `>= 100` ảnh của Sprint 4–5 (chỉ mất `< 40 giây` tính toán AI cho cả lô `100` ảnh!) |
| **RSS RAM nền -> Đỉnh (`10` luồng) -> Sau tải (`100` ảnh)** | `95.30 MiB` -> `1038.38 MiB` -> **`100.72 MiB`** | Mỗi luồng suy luận `999 x 960` cần `~94–96 MiB` bộ đệm `float64` cục bộ; ngay khi `10` luồng kết thúc, bộ cấp phát trả vùng nhớ về hệ điều hành, RSS quay lại `100.72 MiB` |
| **Heap Python giữ lại ròng (`tracemalloc` 20 lượt)** | **`+0.0067 MiB` (`~6.86 KiB`)** | Không có tham chiếu vòng hoặc mảng NumPy/Pillow nào bị rò rỉ (`final_gc_unreachable_objects = 0`) |

---

## 4. Lệnh Tái lập Kiểm thử (Reproducibility Commands)

Từ thư mục gốc repository trên Windows PowerShell:

```powershell
# 1. Chạy toàn bộ 110 unit & contract tests của AI Core (Sprint 1 -> Sprint 4-5)
.\src\ai-core\.venv\Scripts\python.exe -m pytest src/ai-core/tests -q

# 2. Chạy Benchmark 6 ảnh CHASE_DB1 + Stress Test 100 lượt (5 & 10 concurrent requests)
.\src\ai-core\.venv\Scripts\python.exe src/ai-core/scripts/stress_test_ai_core.py --total-requests 100 --concurrency 10

# 3. (Tùy chọn Soak Test 15 phút liên tục) Chạy kiểm thử ngâm tải 900 giây ở mức 10 luồng đồng thời
.\src\ai-core\.venv\Scripts\python.exe src/ai-core/scripts/stress_test_ai_core.py --total-requests 100 --concurrency 10 --duration-seconds 900
```

---

## 5. Lưu ý Phối hợp Tích hợp gửi `M1` (DevOps) & `M2` (Analysis Worker)

1. **Cách bật Contract `1.3.0` trên môi trường Docker Sprint 4–5 (Bảo toàn tương thích ngược Sprint 3)**:
   * Mặc định `config/ai-frangi.yaml` vẫn giữ `riskAvailable: false` để không làm gãy container nghiệm thu Sprint 3 hiện hữu.
   * Khi chuyển sang luồng Sprint 4–5, đặt biến môi trường `AURA_FRANGI_CONFIG_PATH=/app/config/ai-frangi-v1.3.yaml` (hoặc `AURA_RISK_AVAILABLE=true`): endpoint `/api/v1/analyze` lập tức trả về `status="SUCCESS"`, `risk_available=true`, `metrics.tortuosity_index`, `metrics.branching_points`, và `risk_assessment` khớp 100% với `contracts/ai-response-1.3.schema.json`.
2. **Truy vết Phân tán (`X-Correlation-Id`)**: Worker `M2` chỉ cần đính kèm header `X-Correlation-Id` của job RabbitMQ vào HTTP request gọi AI Core; AI Core sẽ echo nguyên vẹn giá trị này ở mọi response (kể cả `4xx/5xx`).
3. **Khuyến nghị hạn mức RAM Container cho `M1`**: Đặt `mem_limit: 1536m` (hoặc `2g`) cho container `ai-core` nếu cho phép `<= 10` request ảnh lớn gọi song song vào cùng 1 instance Uvicorn.
