import { expect, test } from '@playwright/test'

test('새로고침 후 자동 저장으로 복원한다', async ({ page }) => {
  test.setTimeout(120_000)
  page.on('dialog', (dialog) => dialog.accept())
  await page.goto('/')
  await page.getByRole('button', { name: /새 게임/ }).click()
  await expect(page.getByText('이 작품의 인물·회사·사건·조사기관은 허구입니다.')).toBeVisible()

  await page.reload()
  await expect(page.getByRole('button', { name: /이어하기/ })).toBeEnabled()
  await page.getByRole('button', { name: /이어하기/ }).click()
  const dialog = page.getByRole('dialog', { name: '저장 슬롯' })
  await expect(dialog).toBeVisible()
  await dialog.getByRole('button', { name: '불러오기' }).first().click()
  await expect(page.getByText('이 작품의 인물·회사·사건·조사기관은 허구입니다.')).toBeVisible()
})
