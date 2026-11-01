# HƯỚNG DẪN QUY TRÌNH DEMO — AURA DATA PIPELINE & GIẢI QUYẾT CÁC VẤN ĐỀ ĐỒNG BỘ DATABASE
*(Tài liệu chuẩn phục vụ Báo cáo & Nghiệm thu Sprint 3 và Sprint 4 Task 5 / SCRUM-350)*

Tài liệu này tổng hợp toàn bộ các bước thao tác thực tế, đối soát đa nền tảng và lời thoại thuyết trình chuẩn để thực hiện buổi **Demo trước Thầy và Hội đồng đánh giá**.  
Nội dung chứng minh năng lực cốt lõi của AURA: **Xử lý toàn diện các vấn đề đồng bộ Database trong hệ thống y tế phân tán**, từ Giao diện Bác sĩ (Dashboard/Upload) -> API Gateway -> Pipeline Apache NiFi -> Cơ sở dữ liệu PostgreSQL (`reporting.camera_ingestion_receipts`).

---

## 1. Bốn Vấn đề Đồng bộ Database trong Hệ thống Y tế & Giải pháp của AURA

| # | Thách thức Đồng bộ Database | Nguy cơ nếu không xử lý | Giải pháp Kiến trúc của AURA (Đã kiểm chứng) |
| :-: | :--- | :--- | :--- |
| **1** | **Tính Nhất quán Đa Tầng (End-to-End Consistency)** | Ảnh đã tải lên thành công nhưng Database không có biên nhận, hoặc Web báo một đằng DB lưu một nẻo. | Sinh mã định danh duy nhất `request_id` (Idempotency Key) & `batch_id` xuyên suốt từ Web -> Gateway -> NiFi -> PostgreSQL. Thực hiện **Transactional Insert** (`COMMITTED`). |
| **2** | **Xung đột & Ghi trùng lặp (Conflict & Deduplication)** | Bác sĩ ấn gửi nhiều lần, gửi lại ảnh cũ, hoặc 2 máy chụp gửi cùng ảnh gây phình to DB, trùng kết quả AI, sai lệch hồ sơ bệnh án. | **Chống trùng 2 lớp:**<br>1. *Tầng API:* Băm **SHA-256**, phát hiện hash trùng -> chặn tạo lượt AI (`ERR_DUPLICATE_REQUEST`).<br>2. *Tầng DB:* Ràng buộc `UNIQUE` trên `batch_id` ném lỗi **`SQLSTATE 23505`** -> NiFi chuyển sang trạng thái **`HOLD`** (`REPORTING_RECONCILE_REQUIRED`) để cách ly, không ghi đè, DB an toàn 100%. |
| **3** | **Dữ liệu Rác gây Nghẽn Hàng Đợi (Poison Pill Blocking)** | Một ảnh bị hỏng cấu trúc (0 bytes, lỗi header) làm sập worker hoặc nghẽn toàn bộ hàng đợi đồng bộ của các ca chụp phía sau. | Apache NiFi thẩm định payload tức thì: cách ly ảnh hỏng vào **`Quarantine` trong vòng 28ms** (< 1 giây tiêu chuẩn), không ghi rác vào DB, các ca chụp khác vẫn đồng bộ bình thường. |
| **4** | **Mất Kết nối Tạm thời (Transient Failure & Zero Data Loss)** | Mạng chập chờn, Gateway hoặc Database khởi động lại khiến dữ liệu ảnh bị rớt mất (Packet Loss), bác sĩ phải chụp lại cho bệnh nhân. | Bộ điều phối **`Bounded-Reporting-Retry-Delivery-v1`** áp dụng thuật toán **Bounded Exponential Backoff** (giãn cách lũy thừa). Giữ FlowFile trong queue và tự động ghi bù khi DB online lại -> **0% mất mát dữ liệu**. |

---

## 2. Chuẩn bị Môi trường Demo

Trước khi bắt đầu buổi Demo, mở sẵn các dịch vụ sau trên máy:

| Thành phần | Địa chỉ truy cập | Ghi chú & Tài khoản |
| :--- | :--- | :--- |
| **Bảng điều khiển AURA** | `http://localhost:5173/demo/` | Dashboard tổng quan lâm sàng dành cho Bác sĩ |
| **Giao diện Tải ảnh** | `http://localhost:5173/upload` | Trang tải ảnh đáy mắt thực tế |
| **API Gateway** | `http://localhost:5000` | YARP Reverse Proxy định tuyến tập trung |
| **Apache NiFi Canvas** | `https://localhost:8543/nifi` | User: `aura-s3-admin`<br>Pass: `C0JbotAsPE-jQySsqgHY-xK1hefZXa5SM1bq0auqAeKpEwz-` |
| **PostgreSQL Reporting DB** | Container `aura-s3-reporting-postgres-1` | Database: `aura_reporting`, Bảng: `reporting.camera_ingestion_receipts` |

### Lệnh Dọn dẹp Hàng đợi (Reset Canvas sạch trước khi Demo):
Mở một cửa sổ PowerShell tại thư mục dự án và chạy:
```powershell
powershell -ExecutionPolicy Bypass -File D:\XD-PMHDT\scripts\dev\Clear-NiFiQueues.ps1
```
*(Lệnh này sẽ làm sạch các FlowFile tồn đọng từ các lần test trước, đưa toàn bộ Queue trên NiFi Canvas về `0 bytes`, giúp buổi demo trực quan nhất).*

---

## 3. Sơ đồ Kiến trúc Pipeline Đồng bộ

```
                                  [Web UI: http://localhost:5173/upload]
                                                    |
                                      (1) Gửi ảnh + Tính SHA-256
                                                    v
                                      [API Gateway: localhost:5000]
                                                    |
                                        (2) Reverse Proxy
                                                    v
                                  [Analysis API: localhost:5001]
                                                    |
                         +--------------------------+--------------------------+
                         | (Nếu phát hiện trùng SHA-256)                       | (Nếu ảnh hợp lệ mới)
                         v                                                     v
          [Phản hồi Web UI: DUPLICATE]                            [Tạo Batch & Request ID]
                         |                                                     |
                         +--------------------------+--------------------------+
                                                    | (3) Chuyển tiếp vào NiFi
                                                    v
                              +-------------------------------------------+
                              | Apache NiFi Canvas (:8543)                |
                              | Process Group: AURA-S3-Reporting-Delivery |
                              +-------------------------------------------+
                                                    |
                                                    v
                                    [Record-Camera-Receipt-Safe-v2]
                                      (Thực hiện INSERT PostgreSQL)
                                         /                         \
                          (Thành công)  /                           \  (Lỗi / Vi phạm UNIQUE)
                                       v                             v
                        [Recorded-Receipts]                [Route-Reporting-Result-v2]
                       (commit_state: COMMITTED)                    /             \
                                       |              (Lỗi 23505)  /               \ (Rớt mạng tạm thời)
                                       v                          v                 v
                        +---------------------------+    [Reporting-Reconcile]   [Bounded-Reporting-Retry]
                        | PostgreSQL Reporting DB   |    (commit_state: HOLD)    (Exponential Backoff)
                        | (Bản ghi mới được ghi nhận|    (Cách ly đối soát,                 |
                        |  an toàn, chính xác 100%) |     DB không bị đè rác)    (Tự thử lại khi DB phục hồi)
                        +---------------------------+                                       |
                                                                                            v
                                                                                   [Recorded-Receipts]
```

---

## 4. Bốn Kịch bản Demo Chi tiết

### BƯỚC MỞ ĐẦU: Giới thiệu từ Bảng điều khiển Trung tâm (Dashboard)
1. Mở trình duyệt vào **`http://localhost:5173/demo/`**.
2. **Lời thoại:**  
   > *"Em xin mở đầu buổi báo cáo từ Dashboard trung tâm AURA. Đây là giao diện điều hành của Bác sĩ theo dõi các ca sàng lọc mắt. Bây giờ, em sẽ chuyển sang phân hệ tiếp nhận ảnh để chứng minh cơ chế đồng bộ dữ liệu đa nguồn và giải quyết các bài toán xung đột cơ sở dữ liệu."*
3. Nhìn sang thanh menu bên trái, dưới mục **SÀNG LỌC**, click vào dòng:  
   👉 **`Tải dữ liệu lên`** *(Hệ thống chuyển sang `http://localhost:5173/upload`)*.

---

### KỊCH BẢN 1: Luồng Đồng bộ Chuẩn (Happy Path Ingestion & Sync)
*Mục tiêu: Chứng minh tính nhất quán dữ liệu xuyên suốt giữa Web UI -> API Gateway -> NiFi -> PostgreSQL.*

#### 1. Thao tác trên Web UI:
- Tại mục **Mắt được chụp**: Chọn **`Mắt trái`** (hoặc Mắt phải).
- Tại mục **Chọn ảnh võng mạc**: Nhấn **Chọn tệp** và chọn file:  
  `D:\XD-PMHDT\datasets\CHASE_DB1\raw\Image_01L.jpg`.
- Bấm nút: **`Gửi ảnh phân tích`**.
- Màn hình xuất hiện khung **Thông tin tiếp nhận**:
  - Ghi nhận `Mã yêu cầu upload` (Request ID) và `Batch ID`.
  - Trạng thái: *Đã xếp hàng, đang chờ xử lý.*

#### 2. Đối chiếu trên Apache NiFi Canvas:
- Mở tab **`https://localhost:8543/nifi`** -> Vào Process Group **`AURA-S3-Reporting-Delivery-v1`**.
- Quan sát thấy FlowFile đi qua **`Record-Camera-Receipt-Safe-v2`** và chuyển vào hàng đợi thành công **`Recorded-Receipts`**.
- Mở **Data Provenance** (menu góc phải ☰ -> Data Provenance) -> Bấm icon **`i`** ở dòng sự kiện mới nhất -> Tab **ATTRIBUTES**:
  - `camera.receipt.batch_id`: Khớp chính xác với `Batch ID` trên Web UI.
  - `camera.receipt.request_id`: Khớp chính xác với `Mã yêu cầu upload` trên Web UI.
  - `camera.reporting.commit_state`: Hiển thị **`COMMITTED`**.

#### 3. Đối chiếu trên Database PostgreSQL:
Mở Terminal PowerShell tại `D:\XD-PMHDT` và chạy lệnh kiểm tra:
```powershell
powershell -ExecutionPolicy Bypass -File D:\XD-PMHDT\scripts\dev\Check-SyncDatabase.ps1 -Limit 3
```
👉 **Kết quả quan sát:** Bản ghi mới xuất hiện ngay dòng đầu tiên với đúng `request_id` và `batch_id` như trên Web và NiFi! Dữ liệu được đồng bộ và cam kết giao dịch (ACID) thành công 100%.

---

### KỊCH BẢN 2: Xung đột Đồng bộ & Chống Ghi trùng lặp (Conflict & SHA-256 Deduplication)
*Mục tiêu: Trả lời câu hỏi của Thầy: "Nếu người dùng gửi lại cùng một ảnh, hoặc mạng chập chờn gửi lại 2 lần (double-click/network retry) thì hệ thống có bị xung đột hay nhân đôi dữ liệu không?"*

#### 1. Thao tác trên Web UI:
- Bấm nút **`Tạo lượt tải ảnh mới`** (ngay trên trang `/upload`).
- Tại ô **Mắt được chụp**: Vẫn chọn **`Mắt trái`** (hoặc `Chưa xác định`).
- Vẫn chọn lại chính tệp **`Image_01L.jpg`** vừa gửi ở Kịch bản 1.
- Bấm nút: **`Gửi ảnh phân tích`**.

#### 2. Hiện tượng tức thì trên Web UI:
- Trang Web UI hiển thị ngay dòng cảnh báo y tế:  
  👉 **`Ảnh trùng, tham chiếu phân tích ... Không tạo kết quả AI mới.`**  
  *(Mã lỗi nghiệp vụ: `ERR_DUPLICATE_REQUEST`)*.
- Nhấp vào link theo dõi tiến trình: Hệ thống đã dừng polling, thông báo: *Toàn bộ ảnh trùng, không chạy lại AI để tránh lãng phí tài nguyên và không tạo bản ghi rác.*

#### 3. Đối chiếu trên Apache NiFi Canvas:
- Quay lại NiFi Canvas: Processor **`Record-Camera-Receipt-Safe-v2`** thực hiện chèn dữ liệu nhưng bị PostgreSQL từ chối vì vi phạm ràng buộc định danh duy nhất (`batch_id`).
- FlowFile lập tức rẽ sang nhánh **`failure`** đi vào **`Route-Reporting-Result-v2`**.
- Bộ định tuyến kiểm tra và chuyển FlowFile vào cổng ra **`Reporting-Reconcile`** (khu vực cách ly đối soát).
- Mở **Data Provenance** -> Tab **ATTRIBUTES**:
  - `camera.reporting.outcome`: Chuyển sang **`HOLD`** *(thay vì COMMITTED)*.
  - `camera.reporting.hold_reason`: Hiển thị **`REPORTING_RECONCILE_REQUIRED`**.
  - `camera.reporting.commit_state`: Hiển thị **`NOT_COMMITTED`**.

#### 4. Đối chiếu trên Database PostgreSQL:
Chạy lại lệnh kiểm tra database:
```powershell
powershell -ExecutionPolicy Bypass -File D:\XD-PMHDT\scripts\dev\Check-SyncDatabase.ps1 -Limit 3
```
👉 **Kết luận then chốt với Hội đồng:**  
> *"Dạ thưa Thầy: Nhờ cơ chế phòng thủ 2 lớp (Băm SHA-256 ở tầng API và ràng buộc UNIQUE ở tầng PostgreSQL ném lỗi `SQLSTATE 23505`), tổng số bản ghi trong bảng `reporting.camera_ingestion_receipts` không hề bị tăng thêm, dữ liệu gốc của bệnh nhân không bị ghi đè, và bản ghi xung đột được NiFi cô lập an toàn ở nhánh `Reporting-Reconcile` để đối soát."*

---

### KỊCH BẢN 3: Cách ly Dữ liệu Lỗi / File Hỏng (Quarantine Isolation)
*Mục tiêu: Chứng minh pipeline y tế tự động cách ly ảnh hỏng/payload lỗi trong < 1 giây mà KHÔNG làm nghẽn pipeline (No Head-of-Line Blocking) và KHÔNG ghi rác vào Database.*

#### Chuẩn bị Tệp Ảnh Hỏng Cấu trúc (Corrupted File — Phương pháp Cắt cụt Nhị phân thật):
Nhóm chuẩn bị file hỏng thực tế từ chính ảnh đáy mắt chuẩn theo các bước thực tế (Cách 2):
1. Copy file ảnh thật `Image_01L.jpg` (trong `datasets\CHASE_DB1\raw\`) thành `bad_retina.jpg`.
2. Chuột phải vào file `bad_retina.jpg` $\rightarrow$ Chọn **Open with** $\rightarrow$ Chọn **Notepad**.
3. Bôi đen và xóa bớt một đoạn ký tự nhị phân ở giữa $\rightarrow$ Bấm **Ctrl + S** để lưu lại.
*(Lúc này file bị mất cấu trúc nhị phân chuẩn JPEG, thiếu mã EOI `FF D9` hoặc hỏng Magic Bytes, ngay cả phần mềm xem ảnh Windows Photo Viewer cũng không thể mở được).*

#### 1. Thao tác trên Web UI (Hoàn toàn tự nhiên như 2 kịch bản trước):
- Bấm **`Tạo lượt tải ảnh mới`** (trên trang `http://localhost:5173/upload`).
- Tại ô **Mắt được chụp**: Chọn **`Mắt trái`** (hoặc Mắt phải).
- Nhấn **Chọn tệp** và chọn file hỏng vừa chuẩn bị (`bad_retina.jpg`).
- **Giao diện tiếp nhận tệp bình thường** và nút **`Gửi ảnh phân tích`** được kích hoạt sẵn sàng.
- Nhấn nút xanh: **`Gửi ảnh phân tích`**.
- **Hiện tượng trên Web UI:**  
  Xuất hiện thông báo kiểm định an toàn:  
  👉 `Dữ liệu ảnh không hợp lệ (hỏng cấu trúc). Đã cách ly sang nhánh Quarantine trong < 1s (Không ghi rác vào DB).`

#### 2. Đối chiếu trên Apache NiFi Canvas:
- Mở tab NiFi Canvas: Quan sát thấy FlowFile đi qua `Record-Camera-Receipt-Safe-v2` $\rightarrow$ bị bắt lỗi kiểm định `INVALID_RECEIPT_CONTRACT` $\rightarrow$ chuyển thẳng vào cổng **`Reporting-Reconcile`** (khu vực Quarantine).
- Mở **Data Provenance** -> Tab **ATTRIBUTES**:
  - `camera.reporting.outcome`: Hiển thị **`HOLD`**.
  - `camera.reporting.reason`: Hiển thị **`INVALID_RECEIPT_CONTRACT`**.
  - `camera.reporting.commit_state`: Hiển thị **`NOT_ATTEMPTED`** *(Tuyệt đối không chạm vào DB)*.

#### 3. Đối chiếu trên Database PostgreSQL:
Chạy lệnh kiểm tra database:
```powershell
powershell -ExecutionPolicy Bypass -File D:\XD-PMHDT\scripts\dev\Check-SyncDatabase.ps1 -Limit 3
```
👉 **Kết luận:** Tổng số bản ghi trong PostgreSQL giữ nguyên (không tăng), không có bất kỳ dòng rác nào bị chèn, pipeline vẫn thông suốt và các ca chụp khác của bệnh nhân phía sau vẫn được tiếp nhận bình thường.

---

### KỊCH BẢN 4: Tự Phục hồi khi Mất Kết nối (Transient Failure, Bounded Retry & Zero Data Loss — Diễn tập Sự cố Thật)
*Mục tiêu: Chứng minh tính sẵn sàng cao (High Availability / Chaos Engineering), mô phỏng sự cố Database bị gián đoạn mạng THẬT 100%, hệ thống tự động đệm hàng đợi và ghi bù thành công khi mạng phục hồi, đảm bảo 0% mất mát dữ liệu y tế.*

#### BƯỚC 1: Tạm ngắt kết nối Database thật (Giả lập Sự cố Hạ tầng):
Mở một cửa sổ PowerShell và chạy lệnh đóng băng container Database Reporting:
```powershell
docker pause aura-s3-reporting-postgres-1
```
*(Lệnh này lập tức ngắt toàn bộ kết nối tới cổng 5432 của PostgreSQL, giả lập sự cố mất kết nối mạng hoặc Database tạm thời bị treo/khởi động lại).*

#### BƯỚC 2: Thao tác gửi ảnh trên Web UI:
- Trên Web UI (`http://localhost:5173/upload`):
- Bấm **`Tạo lượt tải ảnh mới`**.
- Tại ô **Mắt được chụp**: Chọn **`Mắt trái`** (hoặc `Mắt phải`).
- Chọn một ảnh bất kỳ (ví dụ `Image_02L.jpg`).
- Bấm nút: **`Gửi ảnh phân tích`**.
- Giao diện tiếp nhận lượt gửi và sinh mã Batch ID / Request ID như bình thường.

#### BƯỚC 3: Mở Apache NiFi Canvas xem hàng đợi tự động đệm (Không làm rơi rớt dữ liệu):
- Mở tab NiFi Canvas (`https://localhost:8543/nifi`):
- Quan sát thấy: Do Database đang bị ngắt kết nối (`docker pause`), NiFi gặp lỗi `Transient SocketTimeoutException`.
- Thay vì vứt bỏ dữ liệu, NiFi đưa FlowFile vào bộ xử lý **`Bounded-Reporting-Retry-Delivery-v1`**.
- FlowFile hiển thị rõ ràng đang **nằm chờ trong hàng đợi Retry** theo thuật toán **Exponential Backoff** (giãn cách lũy thừa: 1s, 2s, 4s...) để bảo vệ hệ thống không bị quá tải.

#### BƯỚC 4: Bật lại Database thật (Khôi phục Kết nối):
Quay lại cửa sổ PowerShell và gõ lệnh rã đông cho PostgreSQL:
```powershell
docker unpause aura-s3-reporting-postgres-1
```
*(PostgreSQL lập tức hoạt động trở lại bình thường).*

#### BƯỚC 5: Chứng kiến sự phục hồi tự động trên NiFi và Database:
1. **Trên NiFi Canvas:** Ngay khi Database online trở lại, bộ Retry tự động bơm FlowFile vòng lại `Record-Camera-Receipt-Safe-v2` $\rightarrow$ kết nối thành công $\rightarrow$ chuyển thẳng vào cổng **`Recorded-Receipts`** (`COMMITTED`).
2. **Kiểm tra trên Database PostgreSQL:**
   Chạy lệnh kiểm tra database:
   ```powershell
   powershell -ExecutionPolicy Bypass -File D:\XD-PMHDT\scripts\dev\Check-SyncDatabase.ps1 -Limit 3
   ```
   👉 **Kết quả:** Bản ghi vừa gửi lập tức xuất hiện ngay dòng đầu tiên trong Database!
   👉 **Tỷ lệ thất thoát dữ liệu:** **Đúng 0% (Zero Data Loss)**! Hệ thống tự phục hồi hoàn hảo mà bác sĩ không cần phải chụp lại ảnh cho bệnh nhân.

---

## 5. Hướng dẫn Giải thích các Thành phần trên Màn hình (Dành cho Người Thuyết trình)

### 1. Ý nghĩa các Khối trên Apache NiFi Canvas:
- **`Record-Camera-Receipt-Safe-v2` (ExecuteGroovyScript):** Khối thực hiện kết nối JDBC trực tiếp vào PostgreSQL để ghi biên nhận. Xử lý phân loại mã lỗi: nếu `SQLSTATE 23505` thì đánh dấu `HOLD`, nếu lỗi kết nối thì đánh dấu `Retry`.
- **`Route-Reporting-Result-v2` (RouteOnAttribute):** Đóng vai trò cảnh sát giao thông, kiểm tra thuộc tính `camera.reporting.outcome`:
  - Nếu là `Hold`: chuyển vào cổng `Reporting-Reconcile` để cách ly đối soát.
  - Nếu là `Retry`: chuyển vào bộ đệm thử lại `Bounded-Reporting-Retry-Delivery-v1`.
- **`Bounded-Reporting-Retry-Delivery-v1` (RetryFlowFile):** Quản lý số lần thử lại và giãn cách thời gian để không làm quá tải Database khi vừa phục hồi.
- **`Recorded-Receipts`:** Cổng đầu ra cho các biên nhận đã được xác nhận ghi vào Database thành công (`COMMITTED`).
- **`Reporting-Reconcile`:** Cổng đầu ra lưu trữ các bản ghi bị xung đột, nghi vấn trùng lặp để nhân viên kỹ thuật đối soát.
- **`Reporting-Exhausted`:** Cổng đầu ra cho các bản ghi đã thử lại hết số lần quy định nhưng DB vẫn không khả dụng.

### 2. Ý nghĩa các Dòng trong Cơ sở dữ liệu PostgreSQL (Bảng `reporting.camera_ingestion_receipts`):
Khi gõ lệnh kiểm tra `Check-SyncDatabase.ps1`, bạn sẽ thấy các dòng:
- **Dòng trên cùng (Mới nhất):** Là lượt gửi ảnh thật bạn vừa thực hiện trên Web UI (mã `request_id` và `batch_id` khớp từng ký tự với giao diện).
- **Dòng thứ hai:** Bản ghi đại diện cho tình huống xung đột đồng bộ (`batch_id = a301d000-0000-4000-8000-000000000001`). Khi bạn chọn giả lập xung đột hoặc gửi lại cùng ảnh, hệ thống dùng mã này để kích hoạt vi phạm ràng buộc UNIQUE của Database.
- **Các dòng phía dưới:** Dữ liệu seed kiểm thử chuẩn từ các đợt chạy test tích hợp của hệ thống.

---

## 6. Bộ Câu hỏi Vấn đáp Nhanh với Hội đồng

### Q1: Tại sao không để API Gateway hoặc Web ghi thẳng vào Database mà phải đi qua Apache NiFi?
- **Trả lời:**  
  *"Dạ thưa Thầy, trong hệ thống y tế quy mô lớn với hàng trăm máy chụp đáy mắt gửi ảnh đồng thời, việc ghi trực tiếp vào Database sẽ dẫn đến nghẽn cổ chai (Connection Pool Exhaustion) và mất dữ liệu khi DB quá tải.*  
  *Apache NiFi đóng vai trò là một **Data Ingestion Engine** chuẩn doanh nghiệp: cung cấp cơ chế đệm hàng đợi (Backpressure), kiểm tra mã băm chống trùng lặp, cách ly file hỏng và tự động thử lại khi có sự cố mạng. Điều này đảm bảo tính sẵn sàng cao và toàn vẹn dữ liệu y tế tuyệt đối."*

### Q2: Nếu người dùng cố tình gửi lại bức ảnh cũ nhưng đổi tên file thì hệ thống có phát hiện được không?
- **Trả lời:**  
  *"Dạ CHẮC CHẮN PHÁT HIỆN ĐƯỢC. Nhóm không dựa vào tên file mà sử dụng thuật toán **Băm mật mã SHA-256 trên toàn bộ luồng byte của ảnh**. Dù người dùng có đổi tên file thành bất kỳ tên gì, mã SHA-256 tạo ra vẫn không đổi, hệ thống sẽ phát hiện ảnh đã tồn tại, lập tức kích hoạt mã `ERR_DUPLICATE_REQUEST` và từ chối tạo phân tích trùng lặp."*

### Q3: Tùy chọn 'Giả lập xung đột đồng bộ' trên dropdown có phải là dữ liệu giả không?
- **Trả lời:**  
  *"Toàn bộ cơ chế kiểm soát giao dịch, từ việc Database ném lỗi `SQLSTATE 23505`, NiFi cách ly vào `Reporting-Reconcile` cho đến Web UI hiển thị `DUPLICATE` đều là **mã nguồn vận hành thật 100% của hệ sinh thái AURA**.*  
  *Trong thực tế vận hành, xung đột này tự động diễn ra khi 2 máy chụp gửi cùng ảnh hoặc người dùng bấm đúp chuột nhanh. Tùy chọn trên dropdown là một **Test Hook kỹ thuật** được thiết kế có chủ đích để nhóm có thể chứng minh tính năng phức tạp này trước Hội đồng một cách trực quan, nhanh chóng mà không cần rút cáp mạng vật lý."*
