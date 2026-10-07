import { Button, Card, Container, SimpleGrid, Skeleton, Stack, Text, Title } from '@mantine/core'
import { Link } from 'react-router-dom'
import { useEventTypesList } from '../api/gen'
import type { EventType } from '../api/gen/model'

function EventTypeCard({ eventType }: { eventType: EventType }) {
  return (
    <Card withBorder padding="lg">
      <Stack gap="xs">
        <Title order={3}>{eventType.name}</Title>
        <Text c="dimmed">{eventType.description}</Text>
        <Text size="sm">{eventType.durationMinutes} минут</Text>
      </Stack>
      <Button component={Link} to={`/book/${eventType.id}`} mt="md">
        Записаться
      </Button>
    </Card>
  )
}

// Каталог типов событий для гостя; тот же эндпоинт обслуживает и раздел владельца
export default function HomePage() {
  const { data, isLoading } = useEventTypesList()
  const eventTypes = data?.data ?? []

  return (
    <Container size="md" py="xl">
      <Stack gap="xl">
        <Stack gap="md" ta="center" py="md">
          <Title order={1}>Запишитесь на звонок в удобное время</Title>
          <Text c="dimmed" size="lg" maw={600} mx="auto">
            Выберите тип события, свободный слот и оставьте контакты: мы позвоним вам в назначенное время.
          </Text>
        </Stack>

        {isLoading && <Skeleton height={200} radius="sm" />}

        {!isLoading && eventTypes.length === 0 && (
          <Text ta="center" c="dimmed">
            Нет доступных типов событий
          </Text>
        )}

        <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
          {eventTypes.map(eventType => (
            <EventTypeCard key={eventType.id} eventType={eventType} />
          ))}
        </SimpleGrid>
      </Stack>
    </Container>
  )
}
