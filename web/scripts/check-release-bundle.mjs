#!/usr/bin/env node
/**
 * 릴리스 번들 경계 검사 (BL02).
 * `npm run build` 후 dist/에 테스트 지원 UI 문자열이 남아 있지 않은지 확인한다.
 * 개발 모드 전용 모듈이 import.meta.env.DEV 분기로 제거되었는지 검증하는 용도다.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('..', import.meta.url))
const distDir = join(root, 'dist')

const FORBIDDEN_MARKERS = [
  '정답 행동 실행',
  'LoadScenarioFixture',
  '개발 패널',
  '개발 전용',
]

function walk(dir) {
  const entries = []
  for (const name of readdirSync(dir)) {
    const full = join(dir, name)
    const stat = statSync(full)
    if (stat.isDirectory()) entries.push(...walk(full))
    else entries.push(full)
  }
  return entries
}

let distStat
try {
  distStat = statSync(distDir)
} catch {
  console.error('FAIL: dist/가 없습니다. 먼저 `npm run build`를 실행하세요.')
  process.exit(1)
}
if (!distStat.isDirectory()) {
  console.error('FAIL: dist/가 디렉터리가 아닙니다.')
  process.exit(1)
}

const files = walk(distDir)
// 코드 번들만 검사한다. dist/content의 정본 JSON은 UI 문자열을 포함할 수 있는 데이터다.
const textExtensions = ['.js', '.mjs', '.css', '.html']
let failures = 0
let totalBytes = 0

for (const file of files) {
  const stat = statSync(file)
  totalBytes += stat.size
  if (!textExtensions.some((extension) => file.endsWith(extension))) continue
  const text = readFileSync(file, 'utf8')
  for (const marker of FORBIDDEN_MARKERS) {
    if (text.includes(marker)) {
      console.error(`FAIL: ${relative(root, file)} 에 금지 문자열 "${marker}" 발견`)
      failures += 1
    }
  }
}

console.log(`검사 파일 ${files.length}개, 총 ${(totalBytes / 1024 / 1024).toFixed(2)}MB`)
if (failures > 0) {
  console.error(`FAIL: 릴리스 경계 위반 ${failures}건`)
  process.exit(1)
}
console.log('PASS: 릴리스 번들에 테스트 지원 문자열이 없다.')
