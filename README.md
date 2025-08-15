# VnSQL - Hệ thống SQL Server Phân tán

VnSQL là một hệ thống SQL Server phân tán được phát triển bằng .NET Core, hỗ trợ đa giao thức và có khả năng mở rộng cao.

## Tính năng chính

### 🔌 Đa giao thức
- **MySQL Protocol**: Tương thích với MySQL client
- **MariaDB Protocol**: Hỗ trợ MariaDB client
- **PostgreSQL Protocol**: Tương thích với PostgreSQL client
- **SQLite Protocol**: Hỗ trợ SQLite client

### 💾 Lưu trữ linh hoạt
- **File Storage**: Lưu trữ dữ liệu trên đĩa
- **Memory Storage**: Lưu trữ dữ liệu trong RAM
- **Hybrid Storage**: Kết hợp cả hai phương thức

### 🌐 Phân tán và cụm
- **Cluster Mode**: Chế độ cụm với nhiều node
- **Distributed Mode**: Phân tán dữ liệu tự động
- **Load Balancing**: Cân bằng tải thông minh
- **Failover**: Tự động chuyển đổi khi node lỗi

## Kiến trúc dự án

```
VnSQL/
├── src/
│   ├── VnSQL.Core/          # Core engine và interfaces
│   ├── VnSQL.Protocols/     # Protocol handlers (MySQL, PostgreSQL, etc.)
│   ├── VnSQL.Storage/       # Storage engines (File, Memory)
│   ├── VnSQL.Cluster/       # Cluster management và distributed logic
│   └── VnSQL.Server/        # Main server application
├── tests/
│   └── VnSQL.Tests/         # Unit tests và integration tests
└── docs/                    # Documentation
```

## Yêu cầu hệ thống

- .NET 8.0 hoặc cao hơn
- Windows/Linux/macOS
- Tối thiểu 2GB RAM (khuyến nghị 4GB+)
- 1GB disk space

## Cài đặt và chạy

### 1. Clone repository
```bash
git clone https://github.com/your-username/VnSQL.git
cd VnSQL
```

### 2. Build dự án
```bash
dotnet build
```

### 3. Chạy server
```bash
cd src/VnSQL.Server
dotnet run
```

### 4. Kết nối với client
```bash
# MySQL client
mysql -h localhost -P 3306 -u root -p

# PostgreSQL client
psql -h localhost -p 5432 -U postgres
```

## Cấu hình

Tạo file `appsettings.json` trong thư mục `VnSQL.Server`:

```json
{
  "VnSQL": {
    "Server": {
      "Port": 3306,
      "Host": "0.0.0.0",
      "MaxConnections": 1000
    },
    "Protocols": {
      "MySQL": {
        "Enabled": true,
        "Port": 3306
      },
      "PostgreSQL": {
        "Enabled": true,
        "Port": 5432
      },
      "SQLite": {
        "Enabled": true,
        "Port": 5433
      }
    },
    "Storage": {
      "Type": "Hybrid",
      "FileStorage": {
        "DataPath": "./data",
        "MaxFileSize": "1GB"
      },
      "MemoryStorage": {
        "MaxMemory": "2GB"
      }
    },
    "Cluster": {
      "Enabled": false,
      "Nodes": [
        "localhost:3306",
        "localhost:3307"
      ],
      "ReplicationFactor": 2
    }
  }
}
```

## Đóng góp

Chúng tôi hoan nghênh mọi đóng góp! Vui lòng:

1. Fork dự án
2. Tạo feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit thay đổi (`git commit -m 'Add some AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Mở Pull Request

## License

Dự án này được phân phối dưới MIT License. Xem file `LICENSE` để biết thêm chi tiết.

## Tác giả

Được phát triển bởi đội ngũ VnSQL Team.

---

**VnSQL** - SQL Server phân tán cho người Việt Nam 🇻🇳
