
import { useState } from 'react'
import FormField from '../components/common/FormField'
import './FormDemoPage.css'

export default function FormDemoPage() {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [nameError, setNameError] = useState('')
  const [emailError, setEmailError] = useState('')
  const [message, setMessage] = useState('')

  function handleSubmit(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault()

    setNameError('')
    setEmailError('')
    setMessage('')

    if (!name.trim()) {
      setNameError('Vui lòng nhập họ và tên')
      return
    }

    if (!email.includes('@')) {
      setEmailError('Email không hợp lệ')
      return
    }

    setMessage('Kiểm tra biểu mẫu thành công!')
  }

  return (
    <section>
      <h2>Demo Form dùng chung</h2>
      <p>Kiểm tra nhập liệu và thông báo lỗi.</p>

      <form onSubmit={handleSubmit} noValidate>
        <FormField
          id="fullName"
          label="Họ và tên"
          value={name}
          onChange={setName}
          required
          error={nameError}
        />

        <FormField
          id="email"
          label="Email"
          type="email"
          value={email}
          onChange={setEmail}
          required
          error={emailError}
        />

        <button type="submit" className="aura-demo-submit">
            Kiểm tra
        </button>
        {message && <p role="status">{message}</p>}
      </form>
    </section>
  )
}
