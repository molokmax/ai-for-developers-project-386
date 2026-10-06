import { fetchSlots } from '../api/slots'
import { Badge, Button, Card, Group, SimpleGrid, Skeleton, Stack, Text } from '@mantine/core'
import { DateInput } from '@mantine/dates'
import { useQuery } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'

export default function SlotsPage() {
  const [date, setDate] = useState<string | null>(dayjs().format('YYYY-MM-DD'))
  const navigate = useNavigate()

  const { data: slots, isLoading } = useQuery({
    queryKey: ['slots', date],
    queryFn: () =>
      date
        ? fetchSlots(dayjs(date).startOf('day').toDate(), dayjs(date).endOf('day').toDate())
        : fetchSlots(),
  })

  return (
    <Stack maw={900} mx="auto">
      <Text fw={600} size="lg">
        Слоты
      </Text>

      <DateInput
        value={date}
        onChange={setDate}
        label="Дата"
        placeholder="Выберите дату"
        valueFormat="DD.MM.YYYY"
        maw={300}
      />

      {isLoading && <Skeleton height={200} radius="sm" />}

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
        {slots?.map(slot => (
          <Card key={slot.id} withBorder padding="md">
            <Group justify="space-between">
              <Text fw={500}>
                {dayjs(slot.startUtc).format('DD.MM HH:mm')}
              </Text>
              <Badge color={slot.isBooked ? 'gray' : 'green'}>
                {slot.isBooked ? 'Занято' : 'Свободно'}
              </Badge>
            </Group>
            <Text c="dimmed" size="sm" mt="xs">
              Длительность {Number(slot.duration.split(':')[0])} ч
            </Text>
            <Button
              mt="sm"
              fullWidth
              disabled={slot.isBooked}
              onClick={() => navigate(`/book/${slot.id}`)}
            >
              Записаться
            </Button>
          </Card>
        ))}
      </SimpleGrid>
    </Stack>
  )
}
