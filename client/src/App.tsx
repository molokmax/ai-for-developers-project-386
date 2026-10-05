import { AppShell, Group, Text } from '@mantine/core'
import { Outlet } from 'react-router-dom'

export default function App() {
  return (
    <AppShell header={{ height: 60 }} padding="md">
      <AppShell.Header>
        <Group h="100%" px="md">
          <Text fw={700} size="lg">
            Календарь звонков
          </Text>
        </Group>
      </AppShell.Header>

      <AppShell.Main>
        <Outlet />
      </AppShell.Main>
    </AppShell>
  )
}
