# INSPIRE-AVR — Dataset Bệnh Võng Mạc Tăng Huyết Áp & Nguy Cơ Biến Cố Tim Mạch / Đột Quỵ cho AURA

## 1. Tổng quan
- **Tên dataset**: INSPIRE-AVR (Iowa Normative Set for Processing Images of the REtina - Arteriovenous Ratio)
- **Mục tiêu chuyên môn trong AURA**:
  - Đo đạc tỷ lệ đường kính động mạch / tĩnh mạch võng mạc (**Arteriovenous Ratio - AVR**).
  - Đánh giá mức độ co hẹp tiểu động mạch do **Bệnh Võng Mạc Tăng Huyết Áp (Hypertensive Retinopathy)**.
  - Sàng lọc và cảnh báo sớm **Nguy cơ Biến cố Tim mạch (CVD) và Đột quỵ (Stroke Risk)** thông qua biến đổi hình thái vi mạch võng mạc (vessel tortuosity & focal narrowing).
- **Nguồn dữ liệu**: Đại học Iowa (University of Iowa) & Viện Mắt Quốc gia Hoa Kỳ.
- **Quy mô dữ liệu**:
  - 100 ca đáy mắt lâm sàng (`IM000001` đến `IM006106`).
  - Mỗi ca chứa ảnh đáy mắt gốc (`.JPG`), nhãn phân loại động mạch (`--artry.jpg`), tĩnh mạch (`--veins.jpg`), toàn bộ mạch máu (`--vessels.jpg`, `--both.jpg`) và tệp vector đo đạc (`.ai`).

## 2. Hướng dẫn giải nén
Dữ liệu được chia thành 4 gói nén đều nhau (< 50 MB) để tối ưu lưu trữ và không vượt giới hạn file của GitHub:
- `INSPIRE_AVR_part1.zip` (~42.8 MB, 25 ca đầu)
- `INSPIRE_AVR_part2.zip` (~46.8 MB, 25 ca tiếp theo)
- `INSPIRE_AVR_part3.zip` (~44.9 MB, 25 ca tiếp theo)
- `INSPIRE_AVR_part4.zip` (~48.4 MB, 25 ca cuối)

Tại thư mục gốc của repository, chạy lệnh PowerShell sau để giải nén toàn bộ vào `datasets/INSPIRE_AVR`:

```powershell
New-Item -ItemType Directory -Force -Path datasets/INSPIRE_AVR
Get-ChildItem data-assets/INSPIRE_AVR/*.zip | ForEach-Object {
    Expand-Archive -Path $_.FullName -DestinationPath datasets/INSPIRE_AVR -Force
}
```

*Lưu ý*: Thư mục giải nén `datasets/` được bỏ qua bởi Git (theo `.gitignore`) để giữ sạch kho lưu trữ.
