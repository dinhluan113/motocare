const currencyFormatter = new Intl.NumberFormat('vi-VN', {
  style: 'currency',
  currency: 'VND',
  maximumFractionDigits: 0
})

const numberFormatter = new Intl.NumberFormat('vi-VN')

export const formatCurrency = (value?: number | null) =>
  currencyFormatter.format(Number(value || 0))

export const formatNumber = (value?: number | null) =>
  numberFormatter.format(Number(value || 0))

export const formatDate = (value?: string | Date | null, withTime = false) => {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: withTime ? 'short' : undefined
  }).format(date)
}

const statusLabels: Record<string, string> = {
  Received: 'Mới tiếp nhận',
  Inspecting: 'Đang kiểm tra',
  AwaitingApproval: 'Chờ duyệt',
  Repairing: 'Đang sửa',
  AwaitingParts: 'Chờ phụ tùng',
  Completed: 'Hoàn tất',
  Delivered: 'Đã giao',
  Cancelled: 'Đã hủy',
  Unpaid: 'Chưa thanh toán',
  PartiallyPaid: 'Thanh toán một phần',
  Paid: 'Đã thanh toán',
  Refunded: 'Đã hoàn tiền',
  Active: 'Đang làm',
  OnLeave: 'Tạm nghỉ',
  Inactive: 'Đã nghỉ',
  Pending: 'Chờ thực hiện',
  InProgress: 'Đang thực hiện'
}

export const statusLabel = (status?: string | null) =>
  status ? statusLabels[status] || status : '—'

export const statusTone = (status?: string): 'success' | 'warning' | 'danger' | 'info' | 'neutral' => {
  if (!status) return 'neutral'
  if (['Completed', 'Delivered', 'Paid', 'Active'].includes(status)) return 'success'
  if (['Cancelled', 'Refunded', 'Inactive'].includes(status)) return 'danger'
  if (['AwaitingApproval', 'AwaitingParts', 'PartiallyPaid', 'OnLeave'].includes(status)) return 'warning'
  if (['Repairing', 'Inspecting', 'InProgress'].includes(status)) return 'info'
  return 'neutral'
}
