import { render, screen } from '@testing-library/react'
import { MantineProvider } from '@mantine/core'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import App from './App'

describe('App (smoke)', () => {
  it('рендерит шапку приложения', () => {
    render(
      <MantineProvider>
        <MemoryRouter>
          <App />
        </MemoryRouter>
      </MantineProvider>,
    )

    expect(screen.getByText('Календарь звонков')).toBeDefined()
  })
})
