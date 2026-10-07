import {
  Button,
  Container,
  NumberInput,
  Skeleton,
  Stack,
  Table,
  Text,
  TextInput,
  Textarea,
  Title,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { eventTypesCreate, getEventTypesListQueryKey, useEventTypesList } from '../../api/gen'
import { problemFieldErrors, type FieldErrors } from '../../api/problemErrors'
import type { CreateEventTypeRequest } from '../../api/gen/model'

type EventTypeFieldErrors = FieldErrors<'name' | 'description' | 'durationMinutes'>

// Ключи ошибок валидации: имена полей контракта -> поля формы
const CONTRACT_FIELD_MAP = {
  name: 'name',
  description: 'description',
  durationMinutes: 'durationMinutes',
} as const

export default function EventTypesPage() {
  const { data, isLoading } = useEventTypesList()
  const eventTypes = data?.data ?? []
  const queryClient = useQueryClient()

  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [duration, setDuration] = useState<string | number>(30)
  const [fieldErrors, setFieldErrors] = useState<EventTypeFieldErrors>({})

  const createMutation = useMutation({
    mutationFn: (request: CreateEventTypeRequest) => eventTypesCreate(request),
    onSuccess: async response => {
      if (response.status === 201) {
        notifications.show({
          title: 'Тип события создан',
          message: response.data.name,
          color: 'green',
        })
        setName('')
        setDescription('')
        setDuration(30)
        setFieldErrors({})
        await queryClient.invalidateQueries({ queryKey: getEventTypesListQueryKey() })
        return
      }

      const errors = problemFieldErrors(response.data, CONTRACT_FIELD_MAP)
      setFieldErrors(errors)
      if (Object.keys(errors).length === 0) {
        notifications.show({
          title: response.data.title ?? 'Некорректный запрос',
          message: response.data.detail ?? 'Проверьте данные формы',
          color: 'red',
        })
      }
    },
  })

  const submit = () => {
    const errors: EventTypeFieldErrors = {}
    if (!name.trim()) errors.name = 'Укажите название'
    const minutes = Number(duration)
    if (!Number.isFinite(minutes) || minutes < 30 || minutes % 30 !== 0) {
      errors.durationMinutes = 'Длительность: минимум 30 минут, кратна 30'
    }
    setFieldErrors(errors)
    if (errors.name || errors.durationMinutes) return
    createMutation.mutate({
      name: name.trim(),
      description: description.trim(),
      durationMinutes: minutes,
    })
  }

  return (
    <Container size="md" py="xl">
      <Stack gap="lg">
        <Title order={1}>Типы событий</Title>

        {isLoading ? (
          <Skeleton height={200} radius="sm" />
        ) : eventTypes.length === 0 ? (
          <Text c="dimmed">Нет доступных типов событий</Text>
        ) : (
          <Table withTableBorder>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Название</Table.Th>
                <Table.Th>Описание</Table.Th>
                <Table.Th>Длительность</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {eventTypes.map(eventType => (
                <Table.Tr key={eventType.id}>
                  <Table.Td>{eventType.name}</Table.Td>
                  <Table.Td>{eventType.description}</Table.Td>
                  <Table.Td>{eventType.durationMinutes} минут</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}

        <Stack gap="md" maw={480}>
          <Title order={2}>Новый тип события</Title>
          <TextInput
            label="Название"
            withAsterisk
            value={name}
            error={fieldErrors.name}
            onChange={event => {
              setName(event.currentTarget.value)
              setFieldErrors(prev => ({ ...prev, name: undefined }))
            }}
          />
          <Textarea
            label="Описание"
            value={description}
            error={fieldErrors.description}
            onChange={event => {
              setDescription(event.currentTarget.value)
              setFieldErrors(prev => ({ ...prev, description: undefined }))
            }}
          />
          <NumberInput
            label="Длительность (минут)"
            withAsterisk
            min={30}
            step={30}
            value={duration}
            error={fieldErrors.durationMinutes}
            onChange={value => {
              setDuration(value)
              setFieldErrors(prev => ({ ...prev, durationMinutes: undefined }))
            }}
          />
          <Button loading={createMutation.isPending} onClick={submit} w="fit-content">
            Создать
          </Button>
        </Stack>
      </Stack>
    </Container>
  )
}
