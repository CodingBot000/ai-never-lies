import { Component, type ErrorInfo, type ReactNode } from 'react'

interface Props {
  children: ReactNode
}

interface State {
  error: Error | null
}

/**
 * 렌더링 오류 경계. 사용자에게는 복구 행동을, 개발 모드에는 원인을 보여준다.
 */
export class ErrorBoundary extends Component<Props, State> {
  override state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  override componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error('[ANL] rendering error', error, info.componentStack)
  }

  private readonly handleReload = (): void => {
    window.location.reload()
  }

  override render(): ReactNode {
    if (this.state.error === null) {
      return this.props.children
    }

    return (
      <div className="anl-error-boundary" role="alert">
        <h1>화면을 표시하는 중 문제가 발생했습니다.</h1>
        <p>진행 상황은 저장되어 있을 수 있습니다. 새로고침 후 다시 시도해 주세요.</p>
        <button type="button" onClick={this.handleReload}>
          새로고침
        </button>
        {import.meta.env.DEV ? (
          <pre className="anl-error-boundary__detail">{this.state.error.stack}</pre>
        ) : null}
      </div>
    )
  }
}
