import { MantineProvider } from '@mantine/core'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import App from './App'

afterEach(cleanup)

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
