
import './FormField.css'
type FormFieldProps = {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  placeholder?: string
  type?: string
  required?: boolean
  error?: string
}

export default function FormField({
  id,
  label,
  value,
  onChange,
  placeholder = '',
  type = 'text',
  required = false,
  error,
}: FormFieldProps) {
  return (
    <div className="aura-form-field">
      <label htmlFor={id}>
        {label}
        {required && <span> *</span>}
      </label>

      <input
        id={id}
        name={id}
        type={type}
        value={value}
        placeholder={placeholder}
        required={required}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `${id}-error` : undefined}
        onChange={(event) => onChange(event.target.value)}
      />

      {error && (
        <p id={`${id}-error`} role="alert">
          {error}
        </p>
      )}
    </div>
  )
}
