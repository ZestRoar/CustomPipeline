using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text;

namespace PipelineAsync
{

    public class PipeAsync : IPipeAsync
    {
        private readonly Pipe actorPipe;
        private PipeReader readerPipe;
        private readonly PipeWriter writerPipe;
        private bool waiting = true;

        private SequencePosition? readPosition;

        public delegate void ProcessPipe(ReadOnlySequence<byte> sequence);

        public PipeAsync()
        {
            actorPipe = new Pipe();
            readerPipe = actorPipe.Reader;
            writerPipe = actorPipe.Writer;
        }
        public PipeAsync(PipeOptions optionsOfPipe)
        {
            actorPipe = new Pipe(optionsOfPipe);
            readerPipe = actorPipe.Reader;
            writerPipe = actorPipe.Writer;
        }

        public PipeReader Reader => this.readerPipe;
        public PipeWriter Writer => this.writerPipe;
        public bool IsWaiting => this.waiting;

        public void Create() => this.waiting = false;
        public void Release() => this.waiting = true;
        public Memory<byte> WrittenMemory(int sizeHint) => this.writerPipe.GetMemory(sizeHint);
        public void Advance(int bytes) => this.writerPipe.Advance(bytes);
        public ValueTask<FlushResult> FlushAsync() => this.writerPipe.FlushAsync();
        public async ValueTask<FlushResult> FlushTask()
        {
            var flushTask = writerPipe.FlushAsync();
            var result = flushTask.IsCompletedSuccessfully ? flushTask.Result : await flushTask;
            return result;
        }

        public bool TryRead(out ReadResult result) => readerPipe.TryRead(out result);
        public ValueTask<ReadResult> ReadAsync(CancellationToken token = default) => this.readerPipe.ReadAsync(token);
        public void AdvanceTo(SequencePosition consumed) => this.readerPipe.AdvanceTo(consumed);

        public PipeReader CreateStreamReader(NetworkStream stream) => readerPipe = PipeReader.Create(stream);

        // === 위에는 래핑해본 것 ===
        // === 아래는 비동기 소켓 파이프라인 작업 ===
        public async Task WritePipeAsync(Socket socket, int sizeHint)   // 소켓에서 파이프라인으로 쓰기작업
        {
            try
            {
                int bytesRead = 1;

                while (bytesRead != 0)
                {
                    Memory<byte> memory = writerPipe.GetMemory(sizeHint); // 할당할 최소크기를 지정


                    // 소켓 대신 소켓을 래핑한 정보 구조체를 가져오면 플래그 값 설정 가능
                    bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);
                    if (bytesRead != 0)
                    {
                        writerPipe.Advance(bytesRead); // 파이프라인에 데이터량 알리기
                    }


                    //var result = await FlushTask(); // 성공적으로 리더에 전달할 때까지 Flush
                    FlushResult result = await writerPipe.FlushAsync();

                    if (result.IsCompleted)
                    {
                        break;
                    }
                    else if (result.IsCanceled)
                    {
                        break;
                    }

                }
            }
            catch (Exception)
            {
                Console.WriteLine($"[{socket.RemoteEndPoint}]: Receive Exception");
            }

            await writerPipe.CompleteAsync();
        }

        public async Task ReadPipeAsync(Socket socket, ProcessPipe process)
        {
            try
            {
                while (true)
                {
                    var result = await readerPipe.ReadAsync();
                    var buffer = result.Buffer;     // 불연속적인 메모리를 다룸 (ReadOnlySequence)

                    if (readPosition != null)
                    {
                        buffer = buffer.Slice(buffer.GetPosition(1, readPosition.Value), buffer.End);
                    }

                    process(buffer);

                    readerPipe.AdvanceTo(buffer.Start, buffer.End); // 사용된 데이터 알리기
                    readPosition = buffer.PositionOf((byte)'\n');
                    if (result.IsCanceled || (result.IsCompleted && buffer.IsEmpty))
                    {
                        break;
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine($"[{socket.RemoteEndPoint}]: Read Exception");
            }


            await readerPipe.CompleteAsync();


        }
        public async Task ReadPipeAndSendAsync(Socket socket, SocketAsyncEventArgs eventArgs)
        {
            try
            {
                while (true)
                {
                    if (readerPipe.TryRead(out var result) == false)
                    {
                        result = await readerPipe.ReadAsync();
                    }

                    var buffer = result.Buffer; // 불연속적인 메모리를 다룸 (ReadOnlySequence)

                    if (result.IsCanceled || (result.IsCompleted && buffer.IsEmpty))
                    {
                        break;
                    }

                    try
                    {
                        if (buffer.IsEmpty)
                        {
                            // 무한 루프 or 에러 예외 처리
                            continue;
                        }

                        foreach (var seg in buffer)
                        {
                            Console.Write(Encoding.UTF8.GetString(seg.Span));
                        }

                    }
                    finally
                    {
                        readerPipe.AdvanceTo(buffer.Start, buffer.End); // 사용된 데이터 알리기
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{socket.RemoteEndPoint}]: Send Exception : {ex}");
            }

            await readerPipe.CompleteAsync();
        }
        private List<ArraySegment<byte>> GetBufferList(in ReadOnlySequence<byte> buffer, SocketAsyncEventArgs eventArgs)
        {
            Debug.Assert(!buffer.IsEmpty);
            Debug.Assert(!buffer.IsSingleSegment);

            if (eventArgs.BufferList is List<ArraySegment<byte>> list)
            {
                list.Clear();
            }
            else
            {
                list = new List<ArraySegment<byte>>();
            }

            foreach (var memory in buffer)
            {
                if (!MemoryMarshal.TryGetArray(memory, out var segment))
                {
                    throw new InvalidOperationException("MemoryMarshal.TryGetArray error");
                }
                list.Add(segment);
            }

            return list;
        }

        public async Task RunProcess(Socket socket, int sizeHint, ProcessPipe process)
        {
            Task writing = WritePipeAsync(socket, sizeHint);
            Task reading = ReadPipeAsync(socket, process);

            await Task.WhenAll(reading, writing);

            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
            this.waiting = true;
        }
        public async Task RunSending(Socket socket, int sizeHint, SocketAsyncEventArgs eventArgs)
        {
            Task writing = WritePipeAsync(socket, sizeHint);
            Task reading = ReadPipeAndSendAsync(socket, eventArgs);

            await Task.WhenAll(reading, writing);

            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
            this.waiting = true;
        }
    }
}
