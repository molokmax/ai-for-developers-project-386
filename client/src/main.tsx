import '@mantine/core/styles.css'

import { MantineProvider } from '@mantine/core'
import { DatesProvider } from '@mantine/dates'
import { Notifications } from '@mantine/notifications'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import dayjs from 'dayjs'
import 'dayjs/locale/ru'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import App from './App'
import BookingPage from './pages/BookingPage'
import HomePage from './pages/HomePage'
import EventTypesPage from './pages/admin/EventTypesPage'
import MeetingsPage from './pages/admin/MeetingsPage'

dayjs.locale('ru')

const queryClient = new QueryClient()

const router = createBrowserRouter([
  {
    path: '/',
    element: <App />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'book/:eventTypeId', element: <BookingPage /> },
      { path: 'admin/event-types', element: <EventTypesPage /> },
      { path: 'admin/meetings', element: <MeetingsPage /> },
    ],
  },
])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <MantineProvider>
        <DatesProvider settings={{ locale: 'ru', firstDayOfWeek: 1 }}>
          <Notifications />
          <RouterProvider router={router} />
        </DatesProvider>
      </MantineProvider>
    </QueryClientProvider>
  </StrictMode>,
)
