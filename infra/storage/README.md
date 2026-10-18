# Private storage local — Sprint 2 / SCRUM-300

## Phạm vi

Sử dụng Azurite để phát triển và kiểm thử Azure Blob Storage local.

Image storage:

`mcr.microsoft.com/azure-storage/azurite:3.33.0`

Image công cụ kiểm thử:

`mcr.microsoft.com/azure-cli:2.87.0-azurelinux3.0`

Các script dùng Python standard library; chạy bằng `python3`

trong container công cụ, không cần đăng nhập Azure.

Khóa devstoreaccount1 trong cấu hình là khóa mặc định công khai

của emulator, chỉ dùng local.

## Container dữ liệu

| Container | Mục đích |
| --- | --- |
| aura-originals | Ảnh đầu vào |
| aura-annotations | Kết quả đánh dấu/phân đoạn |
| aura-reports | Báo cáo xuất |

Cả ba container phải tắt anonymous public access.

Script khởi tạo:

- Tạo container còn thiếu.

- Giữ nguyên dữ liệu trong container đã có.

- Kiểm tra ACL.

- Báo lỗi nếu container đang bật public access.

## Endpoint

| Nơi chạy client | Blob endpoint |
| --- | --- |
| Windows | http://127.0.0.1:10000/devstoreaccount1 |
| Container cùng network | http://azurite:10000/devstoreaccount1 |

File cấu hình:

- `storage.host.env.example`: backend chạy trên Windows.

- `storage.docker.env.example`: backend chạy trong Docker.

`Storage__PublicBlobEndpoint` dùng endpoint Windows để tạo

URL mà trình duyệt trên máy phát triển truy cập được.

Các file env chỉ cung cấp cấu hình. Backend cần có adapter

đọc và sử dụng các khóa Storage tương ứng.

Biến `AURA_STORAGE_TEST_ENDPOINT` của script kiểm thử khác

Blob endpoint SDK: không thêm `/devstoreaccount1`, vì script

tự ghép account vào đường dẫn.

Ví dụ:

- Docker: `http://azurite:10000`

- Windows: `http://127.0.0.1:10000`

## Volume và network

Compose sử dụng external volume `aura_azurite_data`, mount tại `/data`.

Trên máy mới, tạo volume trước khi khởi động:

```powershell
docker volume create aura_azurite_data
```

Không xóa volume để xử lý lỗi kết nối.

Không dùng `--remove-orphans` để xử lý cảnh báo khi chưa xác định

vai trò các container còn lại.

Container công cụ phải tham gia cùng network với service Azurite.

Network được kiểm chứng trên máy hiện tại là `aura_aura_network`;

máy khác cần kiểm tra network thực tế.

Khởi động riêng Azurite:

```powershell
docker compose -p aura up -d --no-deps azurite
```

Compose hiện có biến bắt buộc `BILLING_DB_PASSWORD`.

Cần cung cấp biến này khi Compose đọc cấu hình, kể cả khi chỉ

khởi động service Azurite. Không ghi mật khẩu vào repository.

## Chạy khởi tạo và kiểm thử

Chạy tại thư mục gốc repository, sau khi Azurite đang hoạt động:

```powershell
& {
    $ErrorActionPreference = "Stop"
    $storageNetwork = "aura_aura_network"
    $toolImage = "mcr.microsoft.com/azure-cli:2.87.0-azurelinux3.0"
    $scriptDirectory = (Resolve-Path "scripts/storage").Path
    $scriptMount = "type=bind,source=$scriptDirectory,target=/checks,readonly"
    docker run --rm `
        --network $storageNetwork `
        --mount $scriptMount `
        $toolImage `
        python3 /checks/initialize_local_storage.py
    if ($LASTEXITCODE -ne 0) {
        throw "Khởi tạo private storage chưa đạt."
    }
    docker run --rm `
        --network $storageNetwork `
        --mount $scriptMount `
        $toolImage `
        python3 /checks/verify_local_storage.py
    if ($LASTEXITCODE -ne 0) {
        throw "Kiểm thử private storage chưa đạt."
    }
}
```

## Các kiểm tra tự động

Với mỗi container, script tạo một blob kiểm thử tên ngẫu nhiên:

1. Anonymous GET bị từ chối.

2. SAS có quyền đọc trả đúng nội dung.

3. Thời điểm hết hạn SAS được đặt 120 giây sau lúc tạo.

4. SAS chỉ đọc không cho phép ghi đè blob.

5. Nội dung blob ban đầu vẫn giữ nguyên.

6. SAS có thời điểm hết hạn trong quá khứ bị từ chối.

7. Blob kiểm thử được xóa trong bước cleanup.

Script dùng thời điểm bắt đầu lùi 5 phút để dung sai lệch đồng hồ.

Kiểm tra hết hạn dùng SAS đã hết hạn, không chờ 120 giây thực tế.

Không in hoặc đưa SAS URL vào log bàn giao.

## Kết quả local ngày 2026-10-15

- Ba container tồn tại và không bật anonymous public access.

- Khởi tạo lặp giữ nguyên container và dữ liệu.

- Kiểm thử quyền đọc, từ chối ghi, SAS hết hạn và cleanup đạt.

- DNS `azurite:10000` hoạt động trong Docker network.

- Windows đọc được blob qua SAS tạo trực tiếp cho endpoint

  `127.0.0.1:10000`; thiếu SAS bị từ chối; blob thử đã được xóa.

## Phần tích hợp tiếp theo

Backend cần:

- Kiểm tra quyền tài khoản/tenant/tài nguyên trước khi cấp SAS.

- Cấp SAS đọc theo từng blob với thời hạn ngắn.

- Trả Cache-Control: no-store cho response cấp URL truy cập.

- Không ghi SAS hoặc dữ liệu nhạy cảm vào log.

- Tích hợp upload, metadata và cleanup theo contract Sprint 2.

Kết quả emulator không chứng minh Azure cloud RBAC, managed identity,

TLS, mã hóa phía cloud hoặc phân quyền nghiệp vụ tại API đã hoàn tất.
