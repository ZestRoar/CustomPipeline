namespace Mad.Core.Concurrent.Synchronization
{
  using System;

  public interface IPromise
  {
    public void TryComplete(object value);
    public void SetException(Exception exception);
  }

  public sealed class Promise<TResultType> : IPromise
  {
    private readonly Future<TResultType> future;        // 단일 퓨쳐 생성 및 겟터 지원

    public Promise()
    {
      this.future = new ();
    }

    public Future<TResultType> GetFuture()              // GetFuture().Then(Action); 으로 콜백 설정
        {                                                   
      return this.future;
    }

    public void TryComplete(object value)               // 타입 체크 후 해당 result로 콜백 실행
    {

      if (value is TResultType result)  // 타입 체크 (object -> TResultType 캐스팅이 가능한가?)
      {
        this.Complete(result);
      }
      else
      {
        this.SetException(new InvalidCastException(
            $"attempt invalid type casting : {value.GetType().Name} to {typeof(TResultType)}"));
      }
    }

    public void Complete(TResultType result)            // 콜백 실행 및 삭제
    {
      this.future.SetResult(result);
    }

    public void SetException(Exception exception)       // 예외 발생
    {
      this.future.SetException(exception);
    }
  }
}
