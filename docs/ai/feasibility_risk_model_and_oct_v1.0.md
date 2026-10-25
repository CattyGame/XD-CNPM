# AURA — Khả thi risk model và OCT

> SCRUM-309 (W2-M4-02) · Biên soạn ban đầu: Đào Duy Quân (M4).
> Bản rà soát 1.0.1 · 2026-10-24 · baseline 87af21d cùng closeout patches.
> Trạng thái: khảo sát kỹ thuật và giới hạn phạm vi; không phải nghiệm thu mô hình lâm sàng.

## 1. Đích dự đoán và giới hạn Sprint 2

Phải tách ba bài toán trước khi chọn model, nhãn hoặc metric:

| Bài toán | Đầu ra cần định nghĩa | Nhãn / phép đối chiếu cần có | Trạng thái AURA |
|---|---|---|---|
| Phân vùng mạch | Mask pixel mạch / nền | Mask người đánh dấu, quy tắc FOV và split theo đối tượng | Có baseline Frangi offline trên CHASE_DB1. |
| Đo hình thái | Density, tortuosity; AVR cần phân biệt động/tĩnh mạch và định nghĩa phép đo | Phép đo tham chiếu, đơn vị, vùng đo và đánh giá sai số | API hiện phát giá trị mock; mask nhị phân đơn thuần chưa chứng minh đo được AVR. |
| Dự đoán nguy cơ | Phải chọn endpoint cụ thể, thời hạn dự đoán và quần thể; không trộn risk factor với biến cố stroke/CVD | Dữ liệu ghép ảnh–nhãn phù hợp, thời gian theo dõi/censoring nếu dự đoán biến cố | Chưa có cohort/nhãn phù hợp được xác minh trong dự án; giữ mock. |

Không coi LOW/MODERATE/HIGH hoặc score trong JSON là một mô hình nguy cơ đã
được huấn luyện. `is_mock: true`, `limitations` và disclaimer phải được giữ
xuyên kết quả. Tiền xử lý, phân vùng mạch và heuristic quality không chứng
minh khả năng chẩn đoán hay dự đoán biến cố.

## 2. Nguồn dữ liệu và căn cứ nghiên cứu

- CHASE_DB1: nguồn Kingston University mô tả 28 ảnh của hai mắt từ 14 trẻ em,
  mỗi ảnh có hai mask mạch do hai người đánh dấu. Các artifact dự án đã kiểm
  là ảnh/mask segmentation, chưa có nhãn biến cố để huấn luyện risk.
- DRIVE/STARE là nguồn tham khảo trong dataset card; không coi dữ liệu chưa
  được xác minh tại dự án là cohort risk sẵn có. Không suy rộng kết luận thành
  khẳng định trên thế giới không có dataset phù hợp.
- Poplin và cộng sự (2018) là ví dụ nghiên cứu dự đoán yếu tố nguy cơ/biến cố
  từ fundus bằng deep learning. Kết quả của nghiên cứu đó không phải metric
  hoặc bằng chứng hiệu quả của AURA. Việc tiếp cận cohort có điều kiện và
  kiểm tra quyền sử dụng dữ liệu vẫn là công việc riêng của dự án.

Nguồn gốc, truy cập khi rà soát 2026-10-24:

1. Kingston University, [CHASE_DB1 retinal vessel reference dataset](https://researchinnovation.kingston.ac.uk/en/datasets/chasedb1-retinal-vessel-reference-dataset-4/).
2. Poplin et al., *Prediction of cardiovascular risk factors from retinal fundus
   photographs via deep learning*, Nature Biomedical Engineering (2018),
   [DOI 10.1038/s41551-018-0195-0](https://doi.org/10.1038/s41551-018-0195-0).
   [Trang công bố của nhóm nghiên cứu](https://research.google/pubs/predicting-cardiovascular-risk-factors-in-retinal-fundus-photographs-using-deep-learning/).
3. Artifact dự án: `docs/data_cards/dataset_card.md`, manifest CHASE_DB1 và
   `docs/benchmarks/frangi_baseline_v0.2.md` / `frangi_test_v0.2.json`.

Không giữ các khẳng định cũ về ngưỡng AVR 0.60, số lượng ảnh huấn luyện tối
thiểu 10.000–50.000 hoặc thời hạn theo dõi bắt buộc 5–10 năm như chuẩn chung.
Các con số này chưa được xác lập cho một endpoint/cohort cụ thể của AURA.

## 3. Model và metric cần đánh giá

Frangi offline là baseline segmentation. Các lựa chọn học máy như U-Net hoặc
encoder tiền huấn luyện là phương án thử nghiệm sau này, cần so sánh trên cùng
split/nhãn, không khẳng định đã tốt hơn baseline hay đã được nối vào endpoint.

Đối với risk, chưa chọn model triển khai. Trước hết cần định nghĩa target,
cohort, quyền dữ liệu, số biến cố và baseline thống kê. Nếu đủ dữ liệu, so sánh
model ảnh với baseline thích hợp và đánh giá trên tập độc lập. Không biến một
ngưỡng morphology tự đặt thành xác suất risk.

| Phạm vi | Metric / điều kiện báo cáo |
|---|---|
| Segmentation | Dice, IoU, sensitivity, specificity; ghi rõ toàn ảnh/FOV, nhãn tham chiếu, threshold chọn ở validation, số ảnh/số đối tượng. |
| Đo morphology | Sai số với phép đo tham chiếu, độ lặp lại và định nghĩa đơn vị/vùng đo; không dùng Dice thay metric AVR. |
| Risk classification / probability | Discrimination và calibration theo endpoint; confidence interval và đánh giá theo nhóm khi số mẫu cho phép. Không suy ra risk từ pixel metric. |
| Outcome có thời gian | Chọn metric/phương pháp xử lý follow-up và censoring phù hợp sau khi chốt thiết kế nghiên cứu. |
| Hệ thống | Thời gian component và end-to-end, CPU/RAM/peak memory, timeout/failure; ghi phần có và không có trong phép đo. |

Đây là kế hoạch đánh giá, chưa có số liệu risk của AURA. Kết quả Frangi đã lưu
là baseline trên split nhỏ, không đạt bằng chứng clinical validation hoặc NFR
toàn luồng chỉ nhờ thời gian component thấp.

## 4. Compute và OCT

Sprint 2 chưa có decoder, dữ liệu OCT được xác minh, model OCT, nhãn, metric
hoặc benchmark tài nguyên phù hợp. Giữ OCT ngoài runtime được hỗ trợ.

Không suy ra loại file hay tài nguyên bắt buộc từ tên modality. Khi khảo sát
OCT tiếp phải xác định dữ liệu cụ thể: B-scan hay volume, định dạng/export và
metadata, dimensions/voxel spacing, decoder/license, nhãn, đích dự đoán và
quy tắc de-identification. Xác minh từng nguồn trước khi thêm parser.

Chưa có phép đo AURA để chứng minh các con số cũ 2–5 phút/volume, 100 MiB–1 GiB
hoặc yêu cầu GPU tối thiểu 8–16 GiB VRAM. Compute phải đo trên model, decoder,
độ phân giải và phần cứng cụ thể. CPU-only demo hiện tại không chứng minh
khả thi hay không khả thi cho mọi pipeline OCT.

Nếu mở phạm vi sau này, cần RFC riêng cho upload/storage limits, bảo vệ dữ
liệu, queue/Worker/timeouts, schema/UI và quyết định giá Billing. Không tự
nâng giới hạn Gateway, đổi contract version hoặc đặt số credit cho OCT trong
bản khảo sát này. Cấu hình timeout trong tài liệu là budget thiết kế; chưa
coi là Worker đang thực thi.

## 5. Hành vi từ chối và quyết định hiện tại

- Public Analysis: FUNDUS-only; modality khác trả 400 / ERR_VALIDATION theo
  contract. Nhóm 5M đã báo PASS trường hợp UnsupportedModality ở candidate
  kiểm thử; primary Analysis HTTP vẫn disabled, chưa chứng nhận Gateway path
  bằng chính phép thử đó.
- AI mock: OCT trả 422 / UNSUPPORTED_MODALITY; modality lạ khác trả
  422 / INVALID_MODALITY. Có kiểm thử TestClient; 7B được người chạy xác nhận
  PASS toàn suite. Chưa chạy lại image AI hiện hữu trong bước 7A–7C.
- Giữ `is_mock: true`; không huấn luyện model risk, bật OCT hoặc chuyển kết
  quả Frangi offline thành inference thật chỉ bằng sửa tài liệu.

Phạm vi khảo sát SCRUM-309 đã được bổ sung rõ target, data, model, compute,
format và metric/giới hạn. Jira hiện ghi Done; bản rà soát không tự xác nhận
người duyệt đã nghiệm thu, không sửa Jira hoặc đổi phạm vi Sprint sau.
Phần tích hợp Worker/event/Billing và đánh giá lâm sàng vẫn cần bằng chứng riêng.

Kế hoạch tiếp: [M4 evaluation plan](../testing/m4_evaluation_plan.md).
