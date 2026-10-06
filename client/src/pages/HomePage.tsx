import { Button, Card, Container, SimpleGrid, Stack, Text, Title } from '@mantine/core'
import { Link } from 'react-router-dom'

const steps = [
  {
    title: '1. Выберите слот',
    text: 'Свободные окна на ближайшие дни с 9:00 до 18:00. Длительность звонка — один час.',
  },
  {
    title: '2. Оставьте контакты',
    text: 'Имя и email — этого достаточно, чтобы записаться.',
  },
  {
    title: '3. Ждите звонка',
    text: 'В назначенное время мы позвоним вам.',
  },
]

export default function HomePage() {
  return (
    <Container size="md" py="xl">
      <Stack gap="xl">
        <Stack gap="md" ta="center" py="xl">
          <Title order={1}>Запишитесь на звонок в удобное время</Title>
          <Text c="dimmed" size="lg" maw={600} mx="auto">
            Календарь звонков — сервис онлайн-записи: выберите свободный слот, оставьте
            контакты, и мы позвоним вам в назначенное время.
          </Text>
          <Button component={Link} to="/slots" size="lg" mx="auto">
            Записаться на звонок
          </Button>
        </Stack>

        <Title order={2} ta="center">
          Как это работает
        </Title>
        <SimpleGrid cols={{ base: 1, sm: 3 }}>
          {steps.map(step => (
            <Card key={step.title} withBorder padding="lg">
              <Title order={3}>{step.title}</Title>
              <Text c="dimmed" mt="sm">
                {step.text}
              </Text>
            </Card>
          ))}
        </SimpleGrid>

        <Stack gap="md" ta="center" py="xl">
          <Title order={2}>Готовы записаться?</Title>
          <Button component={Link} to="/slots" size="lg" variant="outline" mx="auto">
            Выбрать слот
          </Button>
        </Stack>
      </Stack>
    </Container>
  )
}
