
import { useState } from 'react'
import AppModal from '../components/common/AppModal'

export default function ModalDemoPage() {
  const [isOpen, setIsOpen] = useState(false)

  return (
    <section>
      <h2>Demo Modal dùng chung</h2>

      <p>Kiểm tra chức năng mở và đóng Modal trong AURA.</p>

      <button
        type="button"
        onClick={() => setIsOpen(true)}
      >
        Mở Modal
      </button>

      <AppModal
        isOpen={isOpen}
        title="Xác nhận thao tác"
        onClose={() => setIsOpen(false)}
      >
        <p>
          Đây là Modal dùng chung cho các vai trò
          trong hệ thống AURA.
        </p>
      </AppModal>
    </section>
  )
}
