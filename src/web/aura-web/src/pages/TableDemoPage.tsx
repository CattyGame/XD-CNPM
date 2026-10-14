
import DataTable from '../components/common/DataTable'

// Khai báo các cột của bảng
const columns = [
  { key: 'id', label: 'Mã tài khoản' },
  { key: 'name', label: 'Họ và tên' },
  { key: 'role', label: 'Vai trò' },
  { key: 'status', label: 'Trạng thái' },
]

// Dữ liệu giả lập để kiểm tra
const accounts = [
  {
    id: 'ACC001',
    name: 'Nguyễn Văn A',
    role: 'System Admin',
    status: 'Hoạt động',
  },
  {
    id: 'ACC002',
    name: 'Trần Văn B',
    role: 'Doctor',
    status: 'Hoạt động',
  },
  {
    id: 'ACC003',
    name: 'Lê Thị C',
    role: 'Clinic',
    status: 'Tạm khóa',
  },
]

export default function TableDemoPage() {
  return (
    <section>
      <h2>Demo Table dùng chung</h2>

      <p>Danh sách tài khoản giả lập trong hệ thống AURA.</p>

      <DataTable columns={columns} data={accounts} />
    </section>
  )
}
