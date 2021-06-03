namespace Mad.Core.Concurrent.Synchronization
{
  using System;
  using System.Threading;

  public class Signal
  {
#nullable enable

        // OnCompleted -> 신호 완료
        // Reset -> 콜백 대기
        // Set -> 신호 완료

        private static readonly Action ContinuationCompleted = delegate
    {
    };

    private ContinuationMode mode;  // 인라인 or 쓰레드
    private Action? continuation;   // 신호 set 시 콜백

    public Signal(ContinuationMode mode = ContinuationMode.Inline)
    {
      this.mode = mode;
    }

    public bool IsCompleted => ReferenceEquals(this.continuation, ContinuationCompleted);   // 신호 완료 체크 (주소 비교)
    
    public void OnCompleted(Action continuation)    // 콜백 등록
    {
      _ = continuation ?? throw new NullReferenceException();   

      // 콜백 등록 대기 중이면 콜백 등록하고
      var oldValue = Interlocked.CompareExchange(ref this.continuation, continuation, null);

      // 신호 완료 상태면 한번 더 콜백 호출
      if (ReferenceEquals(oldValue, ContinuationCompleted))
      {
        this.Invoke(continuation);
      }
    }

    public void Reset() // 신호 콜백 등록 대기 중 == null
    {
      Volatile.Write(ref this.continuation, null);
    }

    public void Set()  // 신호 콜백 실행 (continuation == ContinuationCompleted는 신호 완료 상태를 나타냄)
    {
      var continuation = Interlocked.Exchange(ref this.continuation, ContinuationCompleted);
      if (continuation != null && ReferenceEquals(continuation, ContinuationCompleted) == false)    // 콜백 대기 혹은 신호 완료 상태면 필터링
      {
        this.Invoke(continuation);
      }
    }








    public void Set(ContinuationMode mode)
    {
      this.mode = mode;

      var continuation = Interlocked.Exchange(ref this.continuation, ContinuationCompleted);
      if (continuation != null && ReferenceEquals(continuation, ContinuationCompleted) == false)
      {
        this.Invoke(continuation);
      }
    }

    private void Invoke(Action continuation)
    {
      switch (this.mode)
      {
        case ContinuationMode.Inline:
          continuation.Invoke();
          break;
        case ContinuationMode.ThreadPool:
          ThreadPool.QueueUserWorkItem(action => action.Invoke(), continuation, false);
          break;
        default:
          break;
      }
    }
  }
}
