# HRF (High-Resolution Fundus) — Dataset Bệnh Võng Mạc Đái Tháo Đường & Mạch Máu cho AURA

## 1. Tổng quan
- **Tên dataset**: High-Resolution Fundus (HRF) Image Database
- **Mục tiêu chuyên môn trong AURA**:
  - Phát hiện và phân loại **Bệnh Võng Mạc Đái Tháo Đường (Diabetic Retinopathy - DR)**.
  - Phân vùng cấu trúc vi mạch võng mạc độ phân giải cao (3504 x 2336 pixels) phục vụ mô hình phân tích tổn thương vi phình mạch (microaneurysms), xuất huyết và xuất tiết.
- **Nguồn dữ liệu**: Pattern Recognition and Image Processing Group (PRIP), Đại học Erlangen-Nuremberg, Đức.
- **Nhóm bệnh học**:
  - `dr`: Diabetic Retinopathy (Bệnh võng mạc đái tháo đường - 15 ca).
  - `g`: Glaucoma (Glôcôm / Cườm nước - 15 ca).
  - `h`: Healthy (Người khỏe mạnh bình thường - 15 ca).
- **Cấu trúc dữ liệu**:
  - `images/`: Ảnh đáy mắt màu độ phân giải cao (`.JPG`).
  - `manual1/`: Ground truth nhãn phân vùng mạch máu thủ công của chuyên gia nhãn khoa (`.tif`).
  - `mask/`: Mặt nạ trường quan sát võng mạc (Field of View - FOV) (`.tif`).

## 2. Hướng dẫn giải nén
Các file nén được chia nhỏ để tối ưu lưu trữ trên Git:
- `HRF_images_part1.zip` (~37.8 MB)
- `HRF_images_part2.zip` (~29.2 MB)
- `HRF_annotations.zip` (~5.8 MB)

Tại thư mục gốc của repository, chạy lệnh PowerShell sau để giải nén vào thư mục làm việc cục bộ `datasets/HRF`:

```powershell
New-Item -ItemType Directory -Force -Path datasets/HRF
Expand-Archive -Path data-assets/HRF/HRF_images_part1.zip -DestinationPath datasets/HRF -Force
Expand-Archive -Path data-assets/HRF/HRF_images_part2.zip -DestinationPath datasets/HRF -Force
Expand-Archive -Path data-assets/HRF/HRF_annotations.zip -DestinationPath datasets/HRF -Force
```

*Lưu ý*: Thư mục giải nén `datasets/` được bỏ qua bởi Git (theo `.gitignore`) để giữ sạch kho lưu trữ.
