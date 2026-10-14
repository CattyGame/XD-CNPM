import './DataTable.css'
type TableColumn = {
  key: string
  label: string
}

type TableRow = {
  id: string
  [key: string]: string
}

type DataTableProps = {
  columns: TableColumn[]
  data: TableRow[]
}

export default function DataTable({
  columns,
  data,
}: DataTableProps) {
  return (
    <table className="aura-data-table">
      <thead>
        <tr>
          {columns.map((column) => (
            <th key={column.key} scope="col">
              {column.label}
            </th>
          ))}
        </tr>
      </thead>

      <tbody>
        {data.length === 0 ? (
          <tr>
            <td colSpan={columns.length}>
              Chưa có dữ liệu
            </td>
          </tr>
        ) : (
          data.map((row) => (
            <tr key={row.id}>
              {columns.map((column) => (
                <td key={column.key}>
                  {row[column.key] ?? '—'}
                </td>
              ))}
            </tr>
          ))
        )}
      </tbody>
    </table>
  )
}
