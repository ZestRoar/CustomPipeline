namespace Mad.Core.Concurrent.Synchronization
{
  using System;
  using System.Threading;

  // exception 의 경우 다음 future 로 전파 할 필요 있을까? 
  // 현재 생각으론 필요 없어 보인다.
  public sealed class Future<TResult>
  {
#nullable enable
      
    private static readonly Action<TResult> Dummy = _ =>
    {
    };

    private volatile Action<TResult>? successAction;
    private Action<Exception>? failureAction;
    private Future<TResult>? next;
    private TResult? result;
    
    public static Future<TResult> FromResult(TResult result)    // 생성자 대용?
    {
      var future = new Future<TResult>
      {
        result = result,
        successAction = Dummy
      };

      return future;
    }

    public Future<TResult> Then(Action<TResult> onSuccess, Action<Exception> onFailure)
    {
      this.failureAction = onFailure;       // 실패 콜백 등록

      return this.Then(onSuccess);
    }

    public Future<TResult> Then(Action<TResult> onSuccess)
    {
      var nextFuture = new Future<TResult>();
      this.next = nextFuture;       // 다음 링크를 설정

      // 성공 콜백 등록 + 호출 + 더미처리까지
      if (Interlocked.Exchange(ref this.successAction, onSuccess) != null)  // 이미 지정되있는 경우는 외부에서 Tail을 지정하지 않고 있던 것
      {
        var result = this.result!;
        onSuccess(result);
        nextFuture.SetResult(result);   // 다음 노드들 콜백 실행 및 더미 처리
      }

      return nextFuture; // Tail 반환해서 계속 next 이어 나가도록 지정
    }

    internal void SetResult(TResult result) // 리스트 전부 콜백 발생시킨 후 콜백을 더미 처리
    {
      this.result = result;

      var old = Interlocked.Exchange(ref this.successAction, Dummy);
      if (old != null)
      {
        old(this.result);
        this.next?.SetResult(result);
      }
    }

    internal void SetException(Exception exception)     // 실패 콜백이 있으면 예외를 캡쳐 + 발생시키고 없으면 그냥 발생시키기
    {
      if (this.successAction != null)
      {
        var captured = this.failureAction;
        if (captured != null)
        {
          captured(exception);
          return;
        }
      }

      throw exception;
    }
  }
}
