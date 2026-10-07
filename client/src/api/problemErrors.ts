import type { ProblemDetails } from './gen/model'

// Ошибки валидации 400 приходят в формате RFC 9457 validation problem
export type FieldErrors<T extends string> = Partial<Record<T, string>>

// Ключи ошибок валидации отдаются как имена C#-свойств (PascalCase, MemberNames
// из DataAnnotations), а поля контракта в JSON camelCase: сравниваем без регистра
export function problemFieldErrors<T extends string>(
  problem: ProblemDetails & { errors?: Record<string, string[]> },
  // Маппинг имени поля контракта на поле формы
  mapping: Record<string, T>,
): FieldErrors<T> {
  const lookup = new Map(
    Object.entries(mapping).map(([contractField, formField]) => [contractField.toLowerCase(), formField]),
  )

  const errors: FieldErrors<T> = {}
  for (const [field, messages] of Object.entries(problem.errors ?? {})) {
    const message = messages[0]
    if (!message) continue
    const formField = lookup.get(field.toLowerCase())
    if (formField) {
      errors[formField] = message
    }
  }
  return errors
}
