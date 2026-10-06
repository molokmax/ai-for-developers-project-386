import { createBooking, fetchSlots } from '../api/slots'
import { Button, Stack, Text, TextInput } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'

export default function BookingPage() {
  const { slotId } = useParams<{ slotId: string }>()
  const id = Number(slotId)
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [name, setName] = useState('')
  const [email, setEmail] = useState('')

  const { data: slots } = useQuery({ queryKey: ['slots', 'all'], queryFn: () => fetchSlots() })
  const slot = slots?.find(s => s.id === id)

  const bookingMutation = useMutation({
    mutationFn: () => createBooking({ slotId: id, customerName: name, customerEmail: email }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['slots'] })
      notifications.show({ title: 'Готово', message: 'Вы записаны на звонок', color: 'green' })
      navigate('/slots')
    },
    onError: (error: Error) => {
      notifications.show({ title: 'Ошибка', message: error.message, color: 'red' })
    },
  })

  return (
    <Stack maw={400} mx="auto">
      <Text fw={600} size="lg">
        Запись на звонок
      </Text>

      {slot ? (
        <Text c="dimmed">
          {dayjs(slot.startUtc).format('DD.MM.YYYY HH:mm')} ({slot.duration.split(':')[0]} ч)
        </Text>
      ) : (
        <Text c="dimmed">Загружаем слот...</Text>
      )}

      <TextInput
        label="Имя"
        withAsterisk
        value={name}
        onChange={event => setName(event.currentTarget.value)}
      />
      <TextInput
        label="Email"
        withAsterisk
        value={email}
        onChange={event => setEmail(event.currentTarget.value)}
      />

      <Button
        loading={bookingMutation.isPending}
        disabled={!slot || !name || !email}
        onClick={() => bookingMutation.mutate()}
      >
        Записаться
      </Button>
      <Button variant="subtle" onClick={() => navigate('/slots')}>
        Назад
      </Button>
    </Stack>
  )
}
