import type { AppSettings, TextSize, TextSpeed } from '../../app/settings'

interface SettingsPanelProps {
  settings: AppSettings
  onChange: (patch: Partial<AppSettings>) => void
}

const TEXT_SIZE_LABELS: Record<TextSize, string> = {
  '100': '100%',
  '125': '125%',
  '150': '150%',
}

const TEXT_SPEED_LABELS: Record<TextSpeed, string> = {
  slow: '느리게',
  normal: '보통',
  fast: '빠르게',
  instant: '즉시 표시',
}

export function SettingsPanel({ settings, onChange }: SettingsPanelProps) {
  return (
    <div className="anl-settings">
      <fieldset className="anl-settings__group">
        <legend>글자 크기</legend>
        <div className="anl-settings__options">
          {(Object.keys(TEXT_SIZE_LABELS) as TextSize[]).map((size) => (
            <label key={size} className="anl-settings__option">
              <input
                type="radio"
                name="text-size"
                value={size}
                checked={settings.textSize === size}
                onChange={() => onChange({ textSize: size })}
              />
              <span>{TEXT_SIZE_LABELS[size]}</span>
            </label>
          ))}
        </div>
      </fieldset>

      <fieldset className="anl-settings__group">
        <legend>대사 출력 속도</legend>
        <div className="anl-settings__options">
          {(Object.keys(TEXT_SPEED_LABELS) as TextSpeed[]).map((speed) => (
            <label key={speed} className="anl-settings__option">
              <input
                type="radio"
                name="text-speed"
                value={speed}
                checked={settings.textSpeed === speed}
                onChange={() => onChange({ textSpeed: speed })}
              />
              <span>{TEXT_SPEED_LABELS[speed]}</span>
            </label>
          ))}
        </div>
      </fieldset>

      <label className="anl-settings__check">
        <input
          type="checkbox"
          checked={settings.autoAdvance}
          onChange={(event) => onChange({ autoAdvance: event.target.checked })}
        />
        <span>대사 자동 진행 (기본 OFF)</span>
      </label>

      <label className="anl-settings__check">
        <input
          type="checkbox"
          checked={settings.reducedMotion}
          onChange={(event) => onChange({ reducedMotion: event.target.checked })}
        />
        <span>동작 줄이기 (흔들림·점멸 최소화)</span>
      </label>
    </div>
  )
}
