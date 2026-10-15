# BÁO CÁO ĐÁNH GIÁ KHẢ THI MÔ HÌNH NGUY CƠ (RISK MODEL) VÀ MODALITY OCT — AURA AI

> **Dự án**: AURA — AI Retinal Analysis Platform
> **Mã công việc Jira**: SCRUM-309 (W2-M4-02)
> **Người thực hiện**: Đào Duy Quân (quandd2937@ut.edu.vn — M4: AI Engineer & NiFi)
> **Người tiếp nhận / Reviewer**: Nguyễn Trương Hậu (M1 — Tech Lead), Võ Hữu Duy (M2 — Analysis Backend)
> **Phiên bản tài liệu**: v1.0.0
> **Ngày lập**: 16/10/2026
> **Trạng thái**: Đã nghiệm thu khảo sát kỹ thuật; chốt kết luận vận hành cho Sprint 2.

---

## 1. Mục tiêu và phạm vi thẩm định

Tài liệu này là báo cáo thẩm định kỹ thuật chính thức cho hai định hướng mở rộng của hệ thống AURA theo phân công Sprint 2:
1. **Mô hình đánh giá nguy cơ biến cố tim mạch và đột quỵ** (*Cardiovascular & Stroke Risk Assessment*) từ ảnh đáy mắt võng mạc.
2. **Phương thức chụp cắt lớp quang học võng mạc** (*Optical Coherence Tomography — OCT*).

Mục tiêu cốt lõi: Xác định căn cứ khoa học, yêu cầu dữ liệu lâm sàng, năng lực tính toán phần cứng, tác động kiến trúc đa dịch vụ (Gateway, Analysis, NiFi, Worker), và đưa ra **kết luận khả thi có căn cứ kỹ thuật** nhằm bảo đảm an toàn hệ thống, tuân thủ nghiêm ngặt **Contract Sprint 2 v1.0**.

---

## 2. Đánh giá khả thi Mô hình Nguy cơ (Retinal Cardiovascular & Stroke Risk Model)

### 2.1. Cơ sở y sinh học (Biological & Clinical Rationale)
- Hệ mạch máu võng mạc là mạng lưới tuần hoàn vi mạch duy nhất trong cơ thể người có thể quan sát trực tiếp, không xâm lấn thông qua kính chụp đáy mắt (*Fundus camera*).
- Nhiều nghiên cứu lâm sàng quy mô lớn trên thế giới (như *Atherosclerosis Risk in Communities — ARIC Study*, *Blue Mountains Eye Study*, và các nghiên cứu của *Google Health / UK Biobank*) đã chứng minh:
  - **Tỉ lệ đường kính động mạch / tĩnh mạch (Arteriolar-to-Venular Ratio — AVR)**: Người bình thường có tỉ lệ $\approx 2/3$ (0.67). Khi tỉ lệ này giảm dưới $0.60$, có tương quan rõ rệt với xơ vữa động mạch, bệnh võng mạc do tăng huyết áp, và làm tăng nguy cơ đột quỵ thiếu máu cục bộ (*ischemic stroke*).
  - **Độ uốn lượn mạch máu (Vessel Tortuosity)**: Mạch máu ngoằn ngoèo gia tăng là chỉ dấu của tình trạng áp lực thành mạch cao mạn tính và biến đổi huyết động học.
  - **Mật độ mạch và chiều phân nhánh (Fractal Dimension)**: Giảm mật độ mao mạch quanh hoàng điểm phản ánh tình trạng thiếu máu nuôi vi tuần hoàn.

### 2.2. Khảo sát nguồn dữ liệu và rào cản nhãn lâm sàng
Để huấn luyện hoặc kiểm chứng một mô hình AI đánh giá nguy cơ, yêu cầu bắt buộc là dữ liệu phải có **cặp đôi (paired data)** gồm: Ảnh võng mạc + Hồ sơ bệnh án theo dõi dọc (*longitudinal follow-up data* tối thiểu 5–10 năm) ghi nhận biến cố tim mạch/đột quỵ thực tế.

| Tập dữ liệu | Số lượng ảnh | Nhãn phân đoạn mạch (Segmentation) | Nhãn biến cố tim mạch / Đột quỵ (CVD/Stroke) | Khả năng tích hợp vào Sprint 2 |
| :--- | :--- | :--- | :--- | :--- |
| **CHASE_DB1** | 28 ảnh (14 bệnh nhân trẻ em) | ✅ Có Ground-truth chuyên gia | ❌ Không có | Dùng cho baseline phân đoạn mạch, **không dùng được cho risk** |
| **DRIVE** | 40 ảnh | ✅ Có Ground-truth chuyên gia | ❌ Không có | Chỉ phục vụ segmentation |
| **STARE** | 20 ảnh | ✅ Có Ground-truth | ❌ Không có biến cố lâm sàng | Chỉ phục vụ segmentation |
| **UK Biobank** | > 100.000 bệnh nhân | ❌ Không gắn nhãn pixel | ✅ Có nhãn lâm sàng toàn diện | ⚠️ Yêu cầu MTA, phê duyệt đạo đức, phí nghiên cứu, không thể tải vào repo |

> [!WARNING]
> Hiện tại trong phạm vi học phần và dữ liệu công khai sẵn có, **chúng ta không có tập dữ liệu mở nào vừa có ảnh võng mạc vừa có nhãn biến cố tim mạch/đột quỵ được kiểm định y khoa**.

### 2.3. Phân tích hai hướng tiếp cận kỹ thuật

#### Hướng tiếp cận A: Deep Learning End-to-End (Ảnh $\rightarrow$ Risk Score)
- **Phương pháp**: Huấn luyện ResNet-50 hoặc Vision Transformer (ViT) dự đoán trực tiếp điểm nguy cơ hoặc huyết áp tâm thu từ ảnh thô.
- **Đánh giá**:
  - Đòi hỏi tối thiểu $10.000 - 50.000$ ảnh có nhãn lâm sàng để tránh hiện tượng học vẹt (*overfitting*).
  - Mô hình mang tính chất "hộp đen" (*black box*), khó giải thích cho bác sĩ lâm sàng, tiềm ẩn rủi ro chẩn đoán sai lệch (*hallucination*).
  - **Kết luận**: **KHÔNG KHẢ THI** trong khuôn khổ Sprint 2 và tài nguyên hiện tại.

#### Hướng tiếp cận B: Trích xuất chỉ số hình thái mạch máu (Biomarker Morphometry Pipeline)
- **Phương pháp**:
  1. Sử dụng kết quả phân đoạn mạch máu (*Segmentation Mask*) đã qua tiền xử lý chuẩn hóa.
  2. Dùng thuật toán xử lý ảnh hình thái học (*morphological thinning, distance transform, skeleton graph*) để đo đạc khách quan: $AVR$, Mật độ mạch (*Vessel Density*), Chỉ số uốn lượn (*Tortuosity*).
  3. Đối chiếu các chỉ số này với các ngưỡng tham chiếu y văn (*normal clinical ranges*).
- **Đánh giá**:
  - Minh bạch, có thể giải thích được (*Explainable AI*), phản ánh đúng dữ liệu đo đạc thực tế từ mạch máu.
  - Không cần bộ dữ liệu huấn luyện biến cố tim mạch khổng lồ.
  - **Kết luận**: **KHẢ THI** để triển khai ở giai đoạn hoàn thiện thuật toán phân đoạn (Sprint 3–Sprint 4).

### 2.4. Kết luận và Cam kết kỹ thuật đối với Risk Model
1. **Tuân thủ tuyệt đối quy định Contract v1.0**:
   - Ở Sprint 2, API nội bộ `POST /api/v1/analyze` **tiếp tục giữ `is_mock: true`** cho trường `risk_assessment`.
   - Tuyệt đối **không tự ý chuyển `is_mock: false`** chỉ vì đã có bước tiền xử lý hoặc phân đoạn Frangi.
   - Bắt buộc trả về trường `disclaimer` nêu rõ: *"Kết quả hoàn toàn là dữ liệu giả lập phục vụ kiểm thử tích hợp. Không phải kết quả chẩn đoán và không thay thế đánh giá của bác sĩ."*

---

## 3. Đánh giá khả thi Phương thức ảnh cắt lớp quang học OCT (Modality OCT)

### 3.1. Đặc thù kỹ thuật của ảnh OCT so với ảnh đáy mắt (FUNDUS)
- **FUNDUS (Màu 2D)**: Là ảnh chụp bề mặt võng mạc, định dạng ảnh thông thường (JPEG/PNG), độ phân giải phổ biến từ $512\times512$ đến $3000\times3000$ pixels, dung lượng $1 - 10$ MiB.
- **OCT (Cắt lớp 2D/3D)**:
  - Là ảnh quét cắt lớp ngang các tầng tế bào võng mạc (B-scan) hoặc tập hợp hàng trăm lát cắt thể tích 3D (C-scan volume).
  - Định dạng chuẩn y tế: DICOM đa tầng (*multi-frame DICOM*), file trích xuất từ máy Zeiss/Topcon/Heidelberg (`.vol`, `.fda`, `.e2e`, `.oct`).
  - Dung lượng mỗi ca chụp thể tích: **100 MiB – 1 GiB**, vượt xa giới hạn upload hiện tại ($10$ MiB/ảnh).

### 3.2. Yêu cầu tính toán và hạ tầng Docker
- Hiện tại container `aura-ai-core` được thiết lập chạy trên môi trường CPU cục bộ, giới hạn thời gian xử lý AI là **30 giây** (`Worker__AiTimeoutSeconds=30`).
- Việc giải mã file thể tích OCT và chạy các mô hình 3D-CNN / Swin-UNETR để phát hiện phù hoàng điểm (DME) hoặc dịch dưới võng mạc trên CPU sẽ mất từ **$2 - 5$ phút/thể tích**, gây nghẽn hàng đợi (bottleneck), nguy cơ cạn kiệt RAM (OOM Crash container).
- Để xử lý OCT thời gian thực, hệ thống bắt buộc cần GPU chuyên dụng với tối thiểu $8 - 16$ GiB VRAM (CUDA runtime) và kiến trúc worker bất đồng bộ chuyên biệt.

### 3.3. Tác động kiến trúc hệ thống đa dịch vụ (Architecture Impact)
Nếu hỗ trợ OCT trong thời điểm này, toàn bộ hệ sinh thái AURA sẽ phải thay đổi lớn:
1. **API Gateway (M1)**: Phải nâng cấu hình giới hạn multipart request từ $1.05$ GiB lên nhiều GiB, kéo dài request timeout.
2. **Analysis API (M2)**: Schema hiện tại `modality: FUNDUS` phải nâng cấp lên Contract v0.7+, thay đổi bảng lưu trữ file private và cơ chế trích xuất thumbnail.
3. **Billing API (M3)**: Phải xây dựng biểu phí credit riêng cho OCT (không thể tính cào bằng 1 credit như ảnh FUNDUS).
4. **Frontend Web (M5)**: Cần thư viện hiển thị DICOM chuyên biệt (như Cornerstone.js / OHIF Viewer) với thanh trượt lát cắt (slice slider), phức tạp hơn rất nhiều so với thẻ `<img>` hiện tại.
5. **NiFi Ingestion (M4)**: Cần parser giải mã file nhị phân DICOM/FDA chuyên biệt.

### 3.4. Kết luận và Quyết định kỹ thuật đối với Modality OCT
1. **Tạm dừng triển khai runtime cho OCT trong Sprint 2 và Sprint 3**:
   - Nhóm kỹ thuật cần dồn toàn lực hoàn thiện luồng nghiệp vụ cốt lõi của ảnh FUNDUS từ Gateway $\rightarrow$ Analysis $\rightarrow$ Storage $\rightarrow$ Billing $\rightarrow$ Worker.
2. **Cơ chế từ chối nhất quán đã được thiết lập**:
   - **Public Gateway / Analysis API (M2)**: Trả về HTTP `400 Bad Request` với mã lỗi `ERR_VALIDATION` khi nhận modality không phải FUNDUS.
   - **AI Core nội bộ (M4)**: Khi nhận request nội bộ với `modality: OCT`, trả về HTTP `422 Unprocessable Content` với mã lỗi `UNSUPPORTED_MODALITY`, ghi rõ chi tiết:
     ```json
     {
       "error_code": "UNSUPPORTED_MODALITY",
       "message": "OCT chưa được hỗ trợ; chỉ chấp nhận FUNDUS.",
       "details": {
         "supported_modalities": ["FUNDUS"],
         "received_modality": "OCT"
       }
     }
     ```
   - Cơ chế này đã được hiện thực hóa và kiểm thử tự động đạt 100% qua test case `test_oct_modality_returns_422`.

---

## 4. Lộ trình kỹ thuật đề xuất (Roadmap)

| Giai đoạn | Mục tiêu chính cho AI Core (M4) | Trạng thái mô hình Risk | Trạng thái Modality OCT |
| :--- | :--- | :--- | :--- |
| **Sprint 2 (Hiện tại)** | • Chuẩn hóa pipeline tiền xử lý ảnh Fundus (crop FOV, cân bằng tương phản, kiểm tra chất lượng).<br>• Hoàn thành báo cáo khả thi Risk/OCT.<br>• Chốt mapping Worker–AI Contract. | Giữ Mock (`is_mock: true`), disclaimer bắt buộc | Từ chối có kiểm soát (Public 400, AI 422) |
| **Sprint 3** | • Triển khai Worker bất đồng bộ nhận job từ RabbitMQ.<br>• Tích hợp pipeline tiền xử lý vào luồng suy luận thật.<br>• Tối ưu hóa thuật toán phân đoạn mạch máu Frangi/U-Net trên tập CHASE_DB1. | Giữ Mock, bắt đầu viết module tính toán chỉ số hình thái ($AVR$, density) | Giữ nguyên từ chối (bảo toàn tài nguyên) |
| **Sprint 4** | • Hoàn thiện bộ chỉ số hình thái học vi mạch làm gợi ý khách quan.<br>• Kiểm thử hiệu năng end-to-end trên dữ liệu thực. | Tích hợp thuật toán trích xuất Biomarker khách quan | Xây dựng RFC tài liệu kiến trúc cho Phase 2 |

---

## 5. Tổng kết và Đề xuất

1. **Về Risk Model**: Không vội vã xây dựng mô hình Deep Learning phỏng đoán thiếu dữ liệu. Hướng đi khoa học và khả thi nhất cho AURA là trích xuất các chỉ số hình thái học mạch máu ($AVR$, uốn lượn, mật độ) để hỗ trợ quyết định lâm sàng cho bác sĩ.
2. **Về Modality OCT**: Việc hoãn tích hợp OCT vào runtime Sprint 2 là quyết định đúng đắn, giúp đảm bảo tính ổn định của hạ tầng Docker cục bộ và tiến độ bàn giao của toàn bộ 6 thành viên.
3. Báo cáo này hoàn thành 100% yêu cầu nghiệm thu của task **SCRUM-309 (W2-M4-02)** theo văn bản chỉ đạo của Tech Lead Trương Hậu.

---
*Biên soạn bởi: Đào Duy Quân — M4 (AI Engineer & NiFi)*
*Chữ ký kỹ thuật: Daoduyquan*
