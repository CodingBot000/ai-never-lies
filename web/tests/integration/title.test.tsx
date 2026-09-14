import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { App } from '../../src/app/App'

vi.mock('../../src/content/loadContent', async () => {
  const { loadTestContent } = await import('../helpers/testContent')
  return {
    loadContent: async () => ({ ok: true, errors: [], index: loadTestContent() }),
  }
})

async function renderApp() {
  const user = userEvent.setup()
  render(<App />)
  await screen.findByRole('button', { name: /새 게임/ })
  return user
}

describe('타이틀과 기본 내비게이션', () => {
  beforeEach(() => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
  })

  it('타이틀과 메뉴가 렌더링된다', async () => {
    await renderApp()
    expect(screen.getByRole('heading', { name: 'AI는 거짓말을 하지 않는다' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /새 게임/ })).toBeEnabled()
    expect(screen.getByRole('button', { name: /이어하기/ })).toBeDisabled()
    expect(screen.getByText('저장된 진행이 없습니다.')).toBeInTheDocument()
  })

  it('새 게임을 시작하면 s000 경고 대사가 표시된다', async () => {
    const user = await renderApp()
    await user.click(screen.getByRole('button', { name: /새 게임/ }))
    expect(
      await screen.findByText('이 작품의 인물·회사·사건·조사기관은 허구입니다.'),
    ).toBeInTheDocument()
  })

  it('설정 모달에서 글자 크기를 바꾸면 html에 반영되고 저장된다', async () => {
    const user = await renderApp()
    await user.click(screen.getByRole('button', { name: /설정/ }))
    expect(screen.getByRole('dialog', { name: '메뉴' })).toBeInTheDocument()
    await user.click(screen.getByRole('radio', { name: '150%' }))
    expect(document.documentElement.dataset.textSize).toBe('150')
    expect(window.localStorage.getItem('anl.ep01.settings')).toContain('150')
  })

  it('정보 모달을 Esc로 닫는다', async () => {
    const user = await renderApp()
    await user.click(screen.getByRole('button', { name: /정보/ }))
    expect(screen.getByRole('dialog', { name: '정보' })).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: '정보' })).not.toBeInTheDocument()
  })

  it('자동 저장이 있으면 이어하기가 활성화되고 슬롯 목록이 열린다', async () => {
    window.localStorage.setItem('anl.ep01.save.autosave', '{"schemaVersion":1}')
    const user = await renderApp()
    const continueButton = screen.getByRole('button', { name: /이어하기/ })
    expect(continueButton).toBeEnabled()
    await user.click(continueButton)
    const dialog = await screen.findByRole('dialog', { name: '저장 슬롯' })
    expect(within(dialog).getByText('자동 저장')).toBeInTheDocument()
  })
})
