import { Container, Skeleton, Stack, Table, Text, Title } from '@mantine/core'
import dayjs from 'dayjs'
import { useMeetingsList } from '../../api/gen'

export default function MeetingsPage() {
  const { data, isLoading } = useMeetingsList()
  const meetings = data?.data ?? []

  return (
    <Container size="md" py="xl">
      <Stack gap="lg">
        <Title order={1}>Предстоящие встречи</Title>

        {isLoading ? (
          <Skeleton height={200} radius="sm" />
        ) : meetings.length === 0 ? (
          <Text c="dimmed">Предстоящих встреч нет</Text>
        ) : (
          <Table withTableBorder>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Время</Table.Th>
                <Table.Th>Тип события</Table.Th>
                <Table.Th>Гость</Table.Th>
                <Table.Th>Email</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {meetings.map(meeting => (
                <Table.Tr key={meeting.id}>
                  <Table.Td>
                    {dayjs(meeting.startUtc).format('DD.MM.YYYY HH:mm')}–{dayjs(meeting.endUtc).format('HH:mm')}
                  </Table.Td>
                  <Table.Td>{meeting.eventType.name}</Table.Td>
                  <Table.Td>{meeting.customerName}</Table.Td>
                  <Table.Td>{meeting.customerEmail}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
      </Stack>
    </Container>
  )
}
