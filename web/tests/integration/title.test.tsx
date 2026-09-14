import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { App } from '../../src/app/App'

describe('타이틀 화면', () => {
  it('타이틀과 메뉴가 렌더링된다', () => {
    render(<App />)
    expect(screen.getByRole('heading', { name: 'AI는 거짓말을 하지 않는다' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /새 게임/ })).toBeEnabled()
    expect(screen.getByRole('button', { name: /이어하기/ })).toBeDisabled()
    expect(screen.getByText('저장된 진행이 없습니다.')).toBeInTheDocument()
  })

  it('새 게임을 누르면 개발 상태 화면으로 이동하고 돌아올 수 있다', async () => {
    const user = userEvent.setup()
    render(<App />)
    await user.click(screen.getByRole('button', { name: /새 게임/ }))
    expect(screen.getByRole('heading', { name: '새 사건' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: '타이틀로 돌아가기' }))
    expect(screen.getByRole('heading', { name: 'AI는 거짓말을 하지 않는다' })).toBeInTheDocument()
  })

  it('설정 모달에서 글자 크기를 바꾸면 html에 반영되고 저장된다', async () => {
    const user = userEvent.setup()
    render(<App />)
    await user.click(screen.getByRole('button', { name: /설정/ }))
    expect(screen.getByRole('dialog', { name: '설정' })).toBeInTheDocument()
    await user.click(screen.getByRole('radio', { name: '150%' }))
    expect(document.documentElement.dataset.textSize).toBe('150')
    expect(window.localStorage.getItem('anl.ep01.settings')).toContain('150')
  })

  it('정보 모달을 Esc로 닫는다', async () => {
    const user = userEvent.setup()
    render(<App />)
    await user.click(screen.getByRole('button', { name: /정보/ }))
    expect(screen.getByRole('dialog', { name: '정보' })).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: '정보' })).not.toBeInTheDocument()
  })

  it('저장이 있으면 이어하기가 활성화된다', () => {
    window.localStorage.setItem('anl.ep01.save.autosave', '{}')
    render(<App />)
    expect(screen.getByRole('button', { name: /이어하기/ })).toBeEnabled()
  })
})
