import { expect, test } from '@playwright/test'

const SHOTS = '../reports/screenshots'

test('W4 화면 증빙 스크린샷', async ({ page }) => {
  test.setTimeout(180_000)
  page.on('dialog', (dialog) => dialog.accept())
  await page.setViewportSize({ width: 1920, height: 1080 })
  await page.goto('/')
  await page.screenshot({ path: `${SHOTS}/web_w4_title_1920x1080.png` })

  await page.getByRole('button', { name: /새 게임/ }).click()
  await page.getByText('이 작품의 인물·회사·사건·조사기관은 허구입니다.').waitFor()
  await page.screenshot({ path: `${SHOTS}/web_w4_s000_warning_1920x1080.png` })

  const visit = async (sceneId: string): Promise<void> => {
    await page.getByRole('button', { name: '메뉴' }).click()
    await page.getByRole('button', { name: '개발 패널' }).click()
    const dialog = page.getByRole('dialog', { name: '개발 패널' })
    await dialog.getByLabel('씬 ID').fill(sceneId)
    await dialog.getByRole('button', { name: '픽스처 불러오기' }).click()
    await dialog.getByRole('button', { name: '닫기' }).click()
  }

  const advanceToPuzzle = async (): Promise<void> => {
    for (let i = 0; i < 30; i++) {
      if (await page.locator('.anl-puzzle').count()) return
      const next = page.locator('.anl-dialogue__next')
      if (await next.count()) {
        await next.click()
        continue
      }
      const choice = page.locator('.anl-choice:not([disabled])').first()
      if (await choice.count()) {
        await choice.click()
        continue
      }
      await page.waitForTimeout(200)
    }
  }

  await visit('s010')
  await page.screenshot({ path: `${SHOTS}/web_w4_s010_dialogue_1920x1080.png` })

  await visit('s110')
  await advanceToPuzzle()
  await expect(page.locator('.anl-puzzle')).toBeVisible()
  await page.screenshot({ path: `${SHOTS}/web_w4_s110_investigation_1920x1080.png` })

  await visit('s100')
  await page.screenshot({ path: `${SHOTS}/web_w4_s100_hub_1920x1080.png` })

  await visit('s150')
  await advanceToPuzzle()
  await page.screenshot({ path: `${SHOTS}/web_w4_s150_hearing_1920x1080.png` })

  await visit('s430')
  await advanceToPuzzle()
  await page.screenshot({ path: `${SHOTS}/web_w4_s430_matrix_1920x1080.png` })

  await visit('s610')
  await advanceToPuzzle()
  await page.screenshot({ path: `${SHOTS}/web_w4_s610_causal_1920x1080.png` })

  await visit('s640')
  await advanceToPuzzle()
  await page.screenshot({ path: `${SHOTS}/web_w4_s640_report_1920x1080.png` })

  await page.locator('.anl-hud').getByRole('button', { name: '증거' }).click()
  await page.screenshot({ path: `${SHOTS}/web_w4_evidence_panel_1920x1080.png` })
  await page.getByRole('button', { name: '닫기' }).last().click()
})
