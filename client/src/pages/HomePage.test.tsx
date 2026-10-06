import { MantineProvider } from '@mantine/core'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import HomePage from './HomePage'

afterEach(cleanup)

describe('HomePage (лендинг)', () => {
  it('рассказывает про сервис и ведёт на страницу записи', () => {
    render(
      <MantineProvider>
        <MemoryRouter>
          <HomePage />
        </MemoryRouter>
      </MantineProvider>,
    )

    expect(screen.getByRole('heading', { level: 1 })).toBeDefined()
    expect(screen.getByText('Как это работает')).toBeDefined()

    const ctaLinks = screen.getAllByRole('link', { name: /записаться|выбрать слот/i })
    expect(ctaLinks.length).toBeGreaterThan(0)
    for (const link of ctaLinks) {
      expect(link.getAttribute('href')).toBe('/slots')
    }
  })
})
