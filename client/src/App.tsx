import { Anchor, AppShell, Button, Group, Text } from '@mantine/core'
import { Link, Outlet } from 'react-router-dom'

export default function App() {
  return (
    <AppShell header={{ height: 60 }} padding="md">
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Anchor component={Link} to="/" c="inherit" underline="never">
            <Text fw={700} size="lg">
              Календарь звонков
            </Text>
          </Anchor>
          <Button component={Link} to="/slots">
            Записаться
          </Button>
        </Group>
      </AppShell.Header>

      <AppShell.Main>
        <Outlet />
      </AppShell.Main>
    </AppShell>
  )
}
