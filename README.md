# \# AURA — Hệ thống sàng lọc sức khỏe mạch máu võng mạc

# Mã đề tài: SP26SE025. Repository: XD-PMHDT.

# \## Công nghệ

# \- ASP.NET Core/.NET 10, YARP Gateway.

# \- React/TypeScript/Vite, Firebase Authentication và FCM.

# \- PostgreSQL 17, Redis 7.4, RabbitMQ 4.

# \- Azure Blob Storage; Azurite 3.33.0 cho local.

# \- Python/FastAPI cho AI Core.

# \## Bắt đầu

# Xem \[Hướng dẫn môi trường Sprint 2](docs/development/sprint2\_local\_setup.md)

# để chuẩn bị credentials, database, migration và private storage.

# Sau khi hoàn tất bootstrap, tại thư mục gốc repository:

# ```powershell

# \& ".\\scripts\\dev\\Set-AuraDevEnvironment.ps1"

# docker compose -p aura --env-file .env `

# &#x20;   -f compose.yaml -f compose.dev.yaml `

# &#x20;   up -d --build

# ```

# Máy đang sử dụng network `aura\_aura\_network` cần truyền rõ:

# ```powershell

# \& ".\\scripts\\dev\\Set-AuraDevEnvironment.ps1" `

# &#x20;   -DockerNetwork "aura\_aura\_network"

# ```

# Giữ cùng cửa sổ PowerShell để chạy Compose.

# Không ghi đè `.env` hoặc thay khóa mã hóa trên môi trường có dữ liệu cũ.

# \## Địa chỉ local

# | Thành phần | Địa chỉ |

# | --- | --- |

# | Web/Vite | http://localhost:5173 |

# | Gateway | http://127.0.0.1:5000 |

# | Analysis | http://127.0.0.1:5001 |

# | Billing | http://127.0.0.1:5002 |

# | Notification | http://127.0.0.1:5003 |

# | AI Core | http://127.0.0.1:8000 |

# | RabbitMQ UI | http://127.0.0.1:15672 |

# Identity/Profile dành cổng 5004/5005; cần tích hợp bản nguồn M6.

# Không coi container còn lại từ checkout cũ là bản nguồn hiện tại.

# \## Tài liệu

# \- \[Môi trường và sơ đồ triển khai](docs/development/sprint2\_local\_setup.md).

# \- \[ADR-004: private storage và quyền service](docs/adr/ADR-004-private-storage.md).

# \- \[Gateway Sprint 2](docs/api/gateway\_sprint2.md).

# \- \[Database và quyền runtime](infra/postgres/README.md).

# \- \[Private storage local](infra/storage/README.md).

# \- \[Analysis OpenAPI](contracts/analysis-openapi.yaml).

# \- \[AI OpenAPI](contracts/ai\_service\_openapi.yaml).

# \- \[Notification contract](docs/api/notification\_service\_contract\_v0.1.md).

# \## Phạm vi hiện tại

# Repository có nền tảng hạ tầng, Gateway, Billing catalog PoC,

# Notification và AI Core cùng các phần triển khai Sprint 2.

# Health thành công không chứng minh toàn bộ nghiệp vụ đã hoàn tất.

# Xem giới hạn và kết quả kiểm chứng trong tài liệu từng thành phần.
