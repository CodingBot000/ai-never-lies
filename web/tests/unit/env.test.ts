import { describe, expect, it } from 'vitest'
import {
  ART_CONTRACT_VERSION,
  CONTENT_VERSION,
  SAVE_SCHEMA_VERSION,
  testToolsEnabled,
} from '../../src/app/env'

describe('env 기준값', () => {
  it('콘텐츠 버전은 ep01.v1.0.0으로 고정된다', () => {
    expect(CONTENT_VERSION).toBe('ep01.v1.0.0')
  })

  it('아트 계약 버전은 art.v1로 고정된다', () => {
    expect(ART_CONTRACT_VERSION).toBe('art.v1')
  })

  it('저장 스키마 버전은 1이다', () => {
    expect(SAVE_SCHEMA_VERSION).toBe(1)
  })

  it('테스트 지원은 기본적으로 꺼져 있다', () => {
    expect(testToolsEnabled).toBe(false)
  })
})
