using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CustomPipelinesTest
{
    [TestClass]
    public class ReadAsyncCompletionTests
    {
        //[TestMethod]
        //public void AwaitingReadAsyncAwaitableTwiceCompletesWriterWithException()
        //{
        //    async Task Await(ValueTask<ReadResult> a)
        //    {
        //        await a;
        //    }

        //    ValueTask<ReadResult> awaitable = Pipe.Reader.ReadAsync();

        //    Task task1 = Await(awaitable);
        //    Task task2 = Await(awaitable);

        //    Assert.True(task1.IsCompleted);
        //    Assert.True(task1.IsFaulted);
        //    Assert.Equal("Concurrent reads or writes are not supported.", task1.Exception.InnerExceptions[0].Message);

        //    Assert.True(task2.IsCompleted);
        //    Assert.True(task2.IsFaulted);
        //    Assert.Equal("Concurrent reads or writes are not supported.", task2.Exception.InnerExceptions[0].Message);
        //}

        //[TestMethod]
        //public async Task CompletingWithExceptionDoesNotAffectState()
        //{
        //    Pipe.Reader.Complete();
        //    Pipe.Reader.Complete(new Exception());

        //    var result = await Pipe.Writer.FlushAsync();
        //    Assert.True(result.IsCompleted);
        //}

        //[TestMethod]
        //public async Task CompletingWithExceptionDoesNotAffectFailedState()
        //{
        //    Pipe.Reader.Complete(new InvalidOperationException());
        //    Pipe.Reader.Complete(new Exception());

        //    await Assert.ThrowsAsync<InvalidOperationException>(async () => await Pipe.Writer.FlushAsync());
        //}

        //[TestMethod]
        //public async Task CompletingWithoutExceptionDoesNotAffectState()
        //{
        //    Pipe.Reader.Complete(new InvalidOperationException());
        //    Pipe.Reader.Complete();

        //    await Assert.ThrowsAsync<InvalidOperationException>(async () => await Pipe.Writer.FlushAsync());
        //}
    }
}