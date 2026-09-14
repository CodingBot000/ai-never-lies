export interface DiagEntry {
  level: 'error' | 'warn'
  message: string
  at: string
}

/** 사용자 개인정보를 수집하지 않는 진단 로그. 개발 패널과 오류 표시에 사용한다. */
class Diagnostics {
  private entries: DiagEntry[] = []

  error(message: string): void {
    this.entries.push({ level: 'error', message, at: new Date().toISOString() })
  }

  warn(message: string): void {
    this.entries.push({ level: 'warn', message, at: new Date().toISOString() })
  }

  clear(): void {
    this.entries = []
  }

  get recent(): readonly DiagEntry[] {
    return this.entries
  }

  get recentErrors(): string[] {
    return this.entries.filter((entry) => entry.level === 'error').map((entry) => entry.message)
  }

  get recentWarnings(): string[] {
    return this.entries.filter((entry) => entry.level === 'warn').map((entry) => entry.message)
  }
}

export const Diag = new Diagnostics()
