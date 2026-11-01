import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { useBackendSession } from '../auth/backendSession'
import BackendSessionNotice from '../auth/BackendSessionNotice'
import './SessionHomePage.css'

export interface CampaignItem {
  id: string
  tenantId: string
  name: string
  description?: string | null
  location: string
  doctorInChargeId?: string | null
  targetPatientCount: number
  status: 'PLANNED' | 'IN_PROGRESS' | 'COMPLETED' | 'CLOSED'
  startDate: string
  endDate: string
  createdAt: string
  updatedAt: string
}

export interface CampaignSummary {
  campaignId: string
  tenantId: string
  campaignName: string
  status: string
  targetPatientCount: number
  totalBatches: number
  totalImages: number
  completedImages: number
  pendingImages: number
  failedImages: number
  completionRatePercent: number
  normalCases: number
  suspectedCases: number
  highRiskCases: number
  uniquePatients: number
}

export default function CampaignManagementPage() {
  const { state } = useBackendSession()
  const [campaigns, setCampaigns] = useState<CampaignItem[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [successMsg, setSuccessMsg] = useState<string | null>(null)

  // Modal State
  const [showCreateModal, setShowCreateModal] = useState<boolean>(false)
  const [newName, setNewName] = useState('')
  const [newLocation, setNewLocation] = useState('')
  const [newDesc, setNewDesc] = useState('')
  const [newTarget, setNewTarget] = useState<number>(100)
  const [newStartDate, setNewStartDate] = useState(new Date().toISOString().slice(0, 10))
  const [newEndDate, setNewEndDate] = useState(new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 10))

  // Link Batch Modal
  const [linkingCampaignId, setLinkingCampaignId] = useState<string | null>(null)
  const [linkBatchId, setLinkBatchId] = useState('')

  // Summary State
  const [selectedSummary, setSelectedSummary] = useState<CampaignSummary | null>(null)

  const clinicId = 'c1111111-0000-0000-0000-000000000001' // Default clinic demo context

  useEffect(() => {
    loadCampaigns()
  }, [])

  async function loadCampaigns() {
    setLoading(true)
    setError(null)
    try {
      const res = await fetch(`/api/v1/campaigns?clinicContextId=${clinicId}`)
      if (!res.ok) throw new Error('Không thể tải danh sách chiến dịch.')
      const data = await res.json()
      setCampaigns(data)
    } catch (err: any) {
      setError(err.message || 'Lỗi kết nối.')
    } finally {
      setLoading(false)
    }
  }

  async function handleCreateCampaign(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSuccessMsg(null)

    try {
      const res = await fetch(`/api/v1/campaigns?clinicContextId=${clinicId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: newName,
          description: newDesc,
          location: newLocation,
          targetPatientCount: Number(newTarget),
          startDate: new Date(newStartDate).toISOString(),
          endDate: new Date(newEndDate).toISOString()
        })
      })

      if (!res.ok) {
        const errJson = await res.json().catch(() => null)
        throw new Error(errJson?.error?.message || 'Tạo chiến dịch thất bại.')
      }

      setSuccessMsg('Tạo chiến dịch tầm soát thành công!')
      setShowCreateModal(false)
      setNewName('')
      setNewLocation('')
      setNewDesc('')
      loadCampaigns()
    } catch (err: any) {
      setError(err.message)
    }
  }

  async function handleUpdateStatus(campaignId: string, status: string) {
    setError(null)
    setSuccessMsg(null)
    try {
      const res = await fetch(`/api/v1/campaigns/${campaignId}/status?clinicContextId=${clinicId}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status })
      })
      if (!res.ok) throw new Error('Cập nhật trạng thái thất bại.')
      setSuccessMsg(`Đã cập nhật chiến dịch sang trạng thái ${status}.`)
      loadCampaigns()
    } catch (err: any) {
      setError(err.message)
    }
  }

  async function handleLinkBatch(e: React.FormEvent) {
    e.preventDefault()
    if (!linkingCampaignId || !linkBatchId.trim()) return

    setError(null)
    setSuccessMsg(null)
    try {
      const res = await fetch(`/api/v1/campaigns/${linkingCampaignId}/batches?clinicContextId=${clinicId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ batchId: linkBatchId.trim() })
      })

      if (!res.ok) {
        const errJson = await res.json().catch(() => null)
        throw new Error(errJson?.error?.message || 'Không thể liên kết batch.')
      }

      setSuccessMsg('Gắn batch ảnh vào chiến dịch thành công!')
      setLinkingCampaignId(null)
      setLinkBatchId('')
    } catch (err: any) {
      setError(err.message)
    }
  }

  async function handleViewSummary(campaignId: string) {
    setError(null)
    try {
      const res = await fetch(`/api/v1/campaigns/${campaignId}/summary?clinicContextId=${clinicId}`)
      if (!res.ok) throw new Error('Không thể tải số liệu thống kê chiến dịch.')
      const data = await res.json()
      setSelectedSummary(data)
    } catch (err: any) {
      setError(err.message)
    }
  }

  function handleExportCsv(campaignId: string) {
    window.open(`/api/v1/campaigns/${campaignId}/export?clinicContextId=${clinicId}`, '_blank')
  }

  if (state.status !== 'ready') return <BackendSessionNotice />

  const totalCampaigns = campaigns.length
  const activeCampaigns = campaigns.filter(c => c.status === 'IN_PROGRESS' || c.status === 'PLANNED').length
  const closedCampaigns = campaigns.filter(c => c.status === 'CLOSED').length

  return (
    <div className="session-home" style={{ maxWidth: 1200, margin: '0 auto', padding: '20px 24px' }}>
      <header className="session-header" style={{ marginBottom: 24 }}>
        <Link className="session-brand" to="/">AURA</Link>
        <nav aria-label="Điều hướng">
          <Link to="/">Trang chủ</Link>
          <Link to="/batches">Theo dõi batch</Link>
          <Link to="/campaigns" aria-current="page" style={{ fontWeight: 'bold', color: '#0056b3' }}>Chiến dịch tầm soát</Link>
          <Link to="/notifications">Thông báo</Link>
        </nav>
      </header>

      <main>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
          <div>
            <h1 style={{ margin: 0, fontSize: 26, color: '#1a202c' }}>Chiến Dịch Tầm Soát Sức Khỏe Mắt</h1>
            <p style={{ margin: '4px 0 0', color: '#718096' }}>Quản lý các đợt khám lưu động cộng đồng và tầm soát sức khỏe định kỳ doanh nghiệp</p>
          </div>
          <button
            type="button"
            onClick={() => setShowCreateModal(true)}
            style={{
              padding: '10px 18px',
              backgroundColor: '#0066cc',
              color: '#fff',
              border: 'none',
              borderRadius: 6,
              cursor: 'pointer',
              fontWeight: 600
            }}
          >
            + Tạo Chiến Dịch Mới
          </button>
        </div>

        {/* Thông báo thông tin */}
        {error && (
          <div style={{ padding: '12px 16px', background: '#fed7d7', color: '#9b2c2c', borderRadius: 6, marginBottom: 16 }}>
            {error}
          </div>
        )}
        {successMsg && (
          <div style={{ padding: '12px 16px', background: '#c6f6d5', color: '#22543d', borderRadius: 6, marginBottom: 16 }}>
            {successMsg}
          </div>
        )}

        {/* Thẻ KPI Thống kê nhanh */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 16, marginBottom: 24 }}>
          <div style={{ background: '#fff', padding: 16, borderRadius: 8, boxShadow: '0 1px 3px rgba(0,0,0,0.1)', borderLeft: '4px solid #3182ce' }}>
            <span style={{ fontSize: 13, color: '#718096', textTransform: 'uppercase' }}>Tổng chiến dịch</span>
            <div style={{ fontSize: 28, fontWeight: 'bold', color: '#2d3748', marginTop: 4 }}>{totalCampaigns}</div>
          </div>
          <div style={{ background: '#fff', padding: 16, borderRadius: 8, boxShadow: '0 1px 3px rgba(0,0,0,0.1)', borderLeft: '4px solid #38a169' }}>
            <span style={{ fontSize: 13, color: '#718096', textTransform: 'uppercase' }}>Đang triển khai</span>
            <div style={{ fontSize: 28, fontWeight: 'bold', color: '#38a169', marginTop: 4 }}>{activeCampaigns}</div>
          </div>
          <div style={{ background: '#fff', padding: 16, borderRadius: 8, boxShadow: '0 1px 3px rgba(0,0,0,0.1)', borderLeft: '4px solid #e53e3e' }}>
            <span style={{ fontSize: 13, color: '#718096', textTransform: 'uppercase' }}>Đã hoàn tất / Đóng</span>
            <div style={{ fontSize: 28, fontWeight: 'bold', color: '#718096', marginTop: 4 }}>{closedCampaigns}</div>
          </div>
        </div>

        {/* Bảng danh sách chiến dịch */}
        <section style={{ background: '#fff', borderRadius: 8, padding: 20, boxShadow: '0 1px 3px rgba(0,0,0,0.1)' }}>
          <h2 style={{ fontSize: 18, marginTop: 0, marginBottom: 16 }}>Danh Sách Đợt Tầm Soát</h2>
          {loading ? (
            <p style={{ color: '#718096' }}>Đang tải dữ liệu chiến dịch...</p>
          ) : campaigns.length === 0 ? (
            <div style={{ textAlign: 'center', padding: 40, color: '#a0aec0' }}>
              <p>Chưa có chiến dịch tầm soát nào được khởi tạo.</p>
            </div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: 14 }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid #edf2f7', color: '#4a5568' }}>
                    <th style={{ padding: '10px 12px' }}>Tên chiến dịch</th>
                    <th style={{ padding: '10px 12px' }}>Địa điểm</th>
                    <th style={{ padding: '10px 12px' }}>Chỉ tiêu</th>
                    <th style={{ padding: '10px 12px' }}>Thời gian</th>
                    <th style={{ padding: '10px 12px' }}>Trạng thái</th>
                    <th style={{ padding: '10px 12px', textAlign: 'right' }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {campaigns.map((camp) => {
                    const statusColor = camp.status === 'CLOSED'
                      ? '#718096'
                      : camp.status === 'IN_PROGRESS'
                      ? '#3182ce'
                      : camp.status === 'COMPLETED'
                      ? '#38a169'
                      : '#d69e2e'

                    return (
                      <tr key={camp.id} style={{ borderBottom: '1px solid #edf2f7' }}>
                        <td style={{ padding: '12px', fontWeight: 600 }}>{camp.name}</td>
                        <td style={{ padding: '12px', color: '#4a5568' }}>{camp.location}</td>
                        <td style={{ padding: '12px' }}>{camp.targetPatientCount} người</td>
                        <td style={{ padding: '12px', color: '#718096', fontSize: 13 }}>
                          {new Date(camp.startDate).toLocaleDateString('vi-VN')} – {new Date(camp.endDate).toLocaleDateString('vi-VN')}
                        </td>
                        <td style={{ padding: '12px' }}>
                          <span style={{
                            padding: '4px 8px',
                            borderRadius: 12,
                            fontSize: 12,
                            fontWeight: 600,
                            backgroundColor: `${statusColor}20`,
                            color: statusColor
                          }}>
                            {camp.status}
                          </span>
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right' }}>
                          <button
                            type="button"
                            onClick={() => handleViewSummary(camp.id)}
                            style={{ marginRight: 6, padding: '4px 8px', fontSize: 12, cursor: 'pointer', background: '#edf2f7', border: '1px solid #cbd5e0', borderRadius: 4 }}
                          >
                            Thống kê
                          </button>
                          {camp.status !== 'CLOSED' && (
                            <button
                              type="button"
                              onClick={() => setLinkingCampaignId(camp.id)}
                              style={{ marginRight: 6, padding: '4px 8px', fontSize: 12, cursor: 'pointer', background: '#ebf8ff', color: '#3182ce', border: '1px solid #bee3f8', borderRadius: 4 }}
                            >
                              Gắn Batch
                            </button>
                          )}
                          <button
                            type="button"
                            onClick={() => handleExportCsv(camp.id)}
                            style={{ marginRight: 6, padding: '4px 8px', fontSize: 12, cursor: 'pointer', background: '#f0fff4', color: '#276749', border: '1px solid #c6f6d5', borderRadius: 4 }}
                          >
                            Xuất CSV
                          </button>
                          {camp.status !== 'CLOSED' && (
                            <button
                              type="button"
                              onClick={() => handleUpdateStatus(camp.id, 'CLOSED')}
                              style={{ padding: '4px 8px', fontSize: 12, cursor: 'pointer', background: '#fff5f5', color: '#c53030', border: '1px solid #fed7d7', borderRadius: 4 }}
                            >
                              Đóng
                            </button>
                          )}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>

        {/* Modal Xem Thống Kê & Tiến Độ */}
        {selectedSummary && (
          <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
            <div style={{ background: '#fff', padding: 24, borderRadius: 8, width: 560, maxWidth: '90%' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
                <h3 style={{ margin: 0, fontSize: 18 }}>Tiến Độ & Kết Quả Tầm Soát: {selectedSummary.campaignName}</h3>
                <button type="button" onClick={() => setSelectedSummary(null)} style={{ border: 'none', background: 'none', fontSize: 18, cursor: 'pointer' }}>✕</button>
              </div>

              {/* Progress Bar */}
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginBottom: 4 }}>
                  <span>Tỷ lệ hoàn thành phân tích:</span>
                  <strong>{selectedSummary.completionRatePercent}% ({selectedSummary.completedImages}/{selectedSummary.totalImages} ảnh)</strong>
                </div>
                <div style={{ height: 12, background: '#edf2f7', borderRadius: 6, overflow: 'hidden' }}>
                  <div style={{ width: `${selectedSummary.completionRatePercent}%`, height: '100%', background: '#38a169', transition: 'width 0.3s' }} />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 20 }}>
                <div style={{ background: '#f7fafc', padding: 12, borderRadius: 6 }}>
                  <span style={{ fontSize: 12, color: '#718096' }}>Tổng số Batch đã nạp</span>
                  <div style={{ fontSize: 20, fontWeight: 700 }}>{selectedSummary.totalBatches}</div>
                </div>
                <div style={{ background: '#f7fafc', padding: 12, borderRadius: 6 }}>
                  <span style={{ fontSize: 12, color: '#718096' }}>Số bệnh nhân thực tế</span>
                  <div style={{ fontSize: 20, fontWeight: 700 }}>{selectedSummary.uniquePatients} / {selectedSummary.targetPatientCount}</div>
                </div>
                <div style={{ background: '#f0fff4', padding: 12, borderRadius: 6, borderLeft: '4px solid #38a169' }}>
                  <span style={{ fontSize: 12, color: '#276749' }}>Ca bình thường (LOW)</span>
                  <div style={{ fontSize: 20, fontWeight: 700, color: '#22543d' }}>{selectedSummary.normalCases}</div>
                </div>
                <div style={{ background: '#fffaf0', padding: 12, borderRadius: 6, borderLeft: '4px solid #dd6b20' }}>
                  <span style={{ fontSize: 12, color: '#c05621' }}>Nghi ngờ tổn thương</span>
                  <div style={{ fontSize: 20, fontWeight: 700, color: '#7b341e' }}>{selectedSummary.suspectedCases} ({selectedSummary.highRiskCases} ca nặng)</div>
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                <button
                  type="button"
                  onClick={() => handleExportCsv(selectedSummary.campaignId)}
                  style={{ padding: '8px 16px', background: '#38a169', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}
                >
                  Tải Báo Cáo CSV
                </button>
                <button
                  type="button"
                  onClick={() => setSelectedSummary(null)}
                  style={{ padding: '8px 16px', background: '#edf2f7', border: 'none', borderRadius: 4, cursor: 'pointer' }}
                >
                  Đóng
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Modal Gắn Batch */}
        {linkingCampaignId && (
          <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
            <form onSubmit={handleLinkBatch} style={{ background: '#fff', padding: 24, borderRadius: 8, width: 440, maxWidth: '90%' }}>
              <h3 style={{ marginTop: 0, marginBottom: 12 }}>Gắn Batch Ảnh Vào Chiến Dịch</h3>
              <p style={{ fontSize: 13, color: '#718096', marginBottom: 16 }}>Nhập mã Batch ID đã upload thành công để liên kết vào đợt tầm soát này:</p>
              <input
                type="text"
                placeholder="Ví dụ: b1111111-0000-0000-0000-000000000001"
                value={linkBatchId}
                onChange={e => setLinkBatchId(e.target.value)}
                required
                style={{ width: '100%', padding: '10px 12px', border: '1px solid #cbd5e0', borderRadius: 4, marginBottom: 16, boxSizing: 'border-box' }}
              />
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                <button type="button" onClick={() => setLinkingCampaignId(null)} style={{ padding: '8px 14px', background: '#edf2f7', border: 'none', borderRadius: 4, cursor: 'pointer' }}>Hủy</button>
                <button type="submit" style={{ padding: '8px 16px', background: '#0066cc', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer' }}>Gắn Batch</button>
              </div>
            </form>
          </div>
        )}

        {/* Modal Tạo Chiến Dịch Mới */}
        {showCreateModal && (
          <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
            <form onSubmit={handleCreateCampaign} style={{ background: '#fff', padding: 24, borderRadius: 8, width: 500, maxWidth: '90%' }}>
              <h3 style={{ marginTop: 0, marginBottom: 16 }}>Khởi Tạo Chiến Dịch Tầm Soát Mới</h3>
              <div style={{ marginBottom: 12 }}>
                <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 4 }}>Tên đợt tầm soát *</label>
                <input
                  type="text"
                  placeholder="Ví dụ: Khám mắt học đường THPT Nguyễn Huệ"
                  value={newName}
                  onChange={e => setNewName(e.target.value)}
                  required
                  style={{ width: '100%', padding: '8px 12px', border: '1px solid #cbd5e0', borderRadius: 4, boxSizing: 'border-box' }}
                />
              </div>
              <div style={{ marginBottom: 12 }}>
                <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 4 }}>Địa điểm tổ chức *</label>
                <input
                  type="text"
                  placeholder="Ví dụ: Hội trường B - Quận 5, TP.HCM"
                  value={newLocation}
                  onChange={e => setNewLocation(e.target.value)}
                  required
                  style={{ width: '100%', padding: '8px 12px', border: '1px solid #cbd5e0', borderRadius: 4, boxSizing: 'border-box' }}
                />
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 12 }}>
                <div>
                  <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 4 }}>Ngày bắt đầu *</label>
                  <input
                    type="date"
                    value={newStartDate}
                    onChange={e => setNewStartDate(e.target.value)}
                    required
                    style={{ width: '100%', padding: '8px 12px', border: '1px solid #cbd5e0', borderRadius: 4, boxSizing: 'border-box' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 4 }}>Ngày kết thúc *</label>
                  <input
                    type="date"
                    value={newEndDate}
                    onChange={e => setNewEndDate(e.target.value)}
                    required
                    style={{ width: '100%', padding: '8px 12px', border: '1px solid #cbd5e0', borderRadius: 4, boxSizing: 'border-box' }}
                  />
                </div>
              </div>
              <div style={{ marginBottom: 16 }}>
                <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 4 }}>Chỉ tiêu số người khám dự kiến</label>
                <input
                  type="number"
                  min="0"
                  value={newTarget}
                  onChange={e => setNewTarget(Number(e.target.value))}
                  style={{ width: '100%', padding: '8px 12px', border: '1px solid #cbd5e0', borderRadius: 4, boxSizing: 'border-box' }}
                />
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                <button type="button" onClick={() => setShowCreateModal(false)} style={{ padding: '8px 14px', background: '#edf2f7', border: 'none', borderRadius: 4, cursor: 'pointer' }}>Hủy</button>
                <button type="submit" style={{ padding: '8px 18px', background: '#0066cc', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer', fontWeight: 600 }}>Tạo Mới</button>
              </div>
            </form>
          </div>
        )}
      </main>
    </div>
  )
}
