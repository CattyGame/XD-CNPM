
import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react'
import { getFirebaseAuth } from '../firebase/config'

type EyeSide = 'left' | 'right' | 'unknown'

type UploadResponse = {
  batchId: string
  items: unknown[]
  createdAt: string
  statusUrl: string
}
function ImagePreview({ file }: { file: File }) {
  const [previewUrl, setPreviewUrl] = useState('')

  useEffect(() => {
    const url = URL.createObjectURL(file)
    setPreviewUrl(url)

    return () => URL.revokeObjectURL(url)
  }, [file])

  return (
    <div style={{ marginBottom: 16 }}>
      <p>{file.name}</p>

      {previewUrl && (
        <img
          src={previewUrl}
          alt={`Xem trước ${file.name}`}
          style={{
            width: 200,
            height: 200,
            objectFit: 'contain',
            border: '1px solid #ddd',
            borderRadius: 8,
          }}
        />
      )}
    </div>
  )
}
export default function UploadPage() {
  const [patientId, setPatientId] = useState('')
  const [eyeSide, setEyeSide] = useState<EyeSide>('unknown')
  const [images, setImages] = useState<File[]>([])
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState('')
  const [result, setResult] = useState<UploadResponse | null>(null)

  // Xử lý khi người dùng chọn ảnh
  async function handleImageChange(event: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? [])
    // Chỉ chấp nhận định dạng JPEG và PNG
const allowedTypes = ['image/jpeg', 'image/png']

const invalidFile = files.find(
  (file) => !allowedTypes.includes(file.type)
)

if (invalidFile) {
  setMessage(
    `File "${invalidFile.name}" không hợp lệ. Chỉ hỗ trợ JPG và PNG.`
  )
  setImages([])
  setResult(null)
  event.target.value = ''
  return
}
    // Backend mặc định cho phép tối đa 100 ảnh
    const maxImages = 100
    if (files.length > maxImages) {
        setMessage('Mỗi lần chỉ được chọn tối đa 100 ảnh.')
        setImages([])
        setResult(null)
        event.target.value = ''
        return
    }
    // Giới hạn tạm thời: 10 MB cho mỗi ảnh
const maxFileSize = 10 * 1024 * 1024

const hasLargeFile = files.some(
  (file) => file.size > maxFileSize,
)
// Giới hạn độ phân giải theo Backend AURA
const maxImagePixels = 16_000_000

for (const file of files) {
  try {
    const bitmap = await createImageBitmap(file)
    const pixels = bitmap.width * bitmap.height
    bitmap.close()

    if (pixels > maxImagePixels) {
      setMessage(
        `Ảnh "${file.name}" vượt quá giới hạn 16 triệu pixel.`
      )
      setImages([])
      setResult(null)
      event.target.value = ''
      return
    }
  } catch {
    setMessage(`Không thể đọc ảnh "${file.name}".`)
    setImages([])
    setResult(null)
    event.target.value = ''
    return
  }
}
if (hasLargeFile) {
  setMessage('Mỗi ảnh không được vượt quá 10 MB.')
  setImages([])
  setResult(null)
  event.target.value = ''
  return
}

    const validImages = files.every(
      (file) => file.type === 'image/jpeg' || file.type === 'image/png',
    )

    if (!validImages) {
      setMessage('Chỉ chấp nhận ảnh JPG hoặc PNG.')
      setImages([])
      event.target.value = ''
      return
    }

    setImages(files)
    setMessage('')
    setResult(null)
  }

  // Gửi ảnh lên Backend thật
  async function handleUpload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (loading) return

    setMessage('')
    setResult(null)

    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(patientId.trim())) {
      setMessage('Patient ID phải là UUID hợp lệ.')
      return
    }

    if (images.length === 0) {
      setMessage('Vui lòng chọn ít nhất một ảnh.')
      return
    }

    const auth = getFirebaseAuth()
    const user = auth?.currentUser

    if (!user) {
      setMessage('Bạn cần đăng nhập trước khi tải ảnh.')
      return
    }

    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim()

    setLoading(true)

    try {
      // Lấy Firebase ID Token
      const token = await user.getIdToken()

      // Tạo multipart/form-data theo OpenAPI
      const formData = new FormData()

      formData.append('patientId', patientId.trim())
      formData.append('modality', 'FUNDUS')
      formData.append('eyeSide', eyeSide)

      images.forEach((image) => {
        formData.append('images', image)
      })

      const response = await fetch(
        `${(apiBaseUrl ?? '').replace(/\/$/, '')}/api/v1/analyses`,
        {
          method: 'POST',
          headers: {
            Authorization: `Bearer ${token}`,
            'Idempotency-Key': crypto.randomUUID(),
          },
          body: formData,
        },
      )

      if (!response.ok) {
  let errorMessage = ''

  switch (response.status) {
    case 400:
      errorMessage = 'Dữ liệu gửi lên không hợp lệ.'
      break

    case 401:
      errorMessage = 'Phiên đăng nhập không hợp lệ hoặc đã hết hạn.'
      break

    case 403:
      errorMessage = 'Bạn không có quyền tải ảnh cho bệnh nhân này.'
      break

    case 413:
      errorMessage = 'Dung lượng ảnh hoặc yêu cầu vượt quá giới hạn.'
      break

    case 429:
      errorMessage = 'Bạn gửi quá nhiều yêu cầu. Vui lòng thử lại sau.'
      break

    case 500:
    case 502:
    case 503:
      errorMessage = 'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.'
      break

    default:
      errorMessage = `Không thể tải ảnh lên. HTTP ${response.status}.`
  }

  throw new Error(errorMessage)
}

      if (response.status !== 202) {
        throw new Error(`Trạng thái phản hồi không đúng: ${response.status}`)
      }

      const data: UploadResponse = await response.json()

      setResult(data)
      setMessage('Backend đã tiếp nhận ảnh và tạo yêu cầu phân tích.')
    } catch (error) {
  if (error instanceof TypeError) {
    setMessage(
      'Không thể kết nối đến Backend. Vui lòng kiểm tra mạng hoặc máy chủ API.',
    )
  } else {
    setMessage(
      error instanceof Error
        ? error.message
        : 'Không thể tải ảnh lên. Vui lòng thử lại.',
    )
  }
}finally {
      setLoading(false)
    }
  }

  return (
    <main style={{ maxWidth: 700, margin: '40px auto', padding: 24 }}>
      <h1>AURA — Upload ảnh võng mạc</h1>

      <p>
        Tải ảnh đáy mắt để gửi yêu cầu sàng lọc sức khỏe
        mạch máu võng mạc.
      </p>

      <form onSubmit={(event) => void handleUpload(event)}>
        <div style={{ marginBottom: 20 }}>
          <label htmlFor="patientId">Patient ID (UUID)</label>

          <input
            id="patientId"
            type="text"
            value={patientId}
            onChange={(event) => setPatientId(event.target.value)}
            placeholder="Nhập UUID bệnh nhân"
            required
            style={{ display: 'block', width: '100%', padding: 10 }}
          />
        </div>

        <div style={{ marginBottom: 20 }}>
          <label htmlFor="eyeSide">Mắt được chụp</label>

          <select
            id="eyeSide"
            value={eyeSide}
            onChange={(event) => setEyeSide(event.target.value as EyeSide)}
            style={{ display: 'block', width: '100%', padding: 10 }}
          >
            <option value="unknown">Chưa xác định</option>
            <option value="left">Mắt trái</option>
            <option value="right">Mắt phải</option>
          </select>
        </div>

        <div style={{ marginBottom: 20 }}>
          <label htmlFor="images">Chọn ảnh võng mạc</label>

          <input
            id="images"
            type="file"
            accept="image/jpeg,image/png"
            multiple
            onChange={handleImageChange}
            style={{ display: 'block', marginTop: 10 }}
          />

          {images.length > 0 && (
  <div style={{ marginTop: 16 }}>
    <p>Đã chọn {images.length} ảnh:</p>

    {images.map((image, index) => (
  <ImagePreview
    key={`${image.name}-${image.size}-${index}`}
    file={image}
  />
))}
  </div>
)}
        </div>

        <button type="submit" disabled={loading}>
          {loading ? 'Đang gửi ảnh...' : 'Gửi ảnh phân tích'}
        </button>
      </form>

      {message && (
        <p role="status" style={{ marginTop: 20 }}>
          {message}
        </p>
      )}

      {result && (
        <section style={{ marginTop: 24 }}>
          <h2>Thông tin tiếp nhận</h2>
          <p><strong>Batch ID:</strong> {result.batchId}</p>
          <p><strong>Số ảnh:</strong> {result.items.length}</p>
          <p><strong>Thời gian:</strong> {result.createdAt}</p>
          <p><strong>Status URL:</strong> {result.statusUrl}</p>
        </section>
      )}
    </main>
  )
}
