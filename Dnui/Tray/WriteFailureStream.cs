namespace Dnui.Tray;

/// <summary>
/// Wraps the protocol output: the first failed write or flush (the caller stopped reading or is
/// gone) runs <paramref name="onFailure"/> once. StreamJsonRpc does not end the connection on a
/// failed write by itself.
/// </summary>
internal sealed class WriteFailureStream(Stream inner, Action onFailure) : Stream
{
    private int _failed;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count) => Guard(() => inner.Write(buffer, offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        try
        {
            inner.Write(buffer);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            Fail();
            throw;
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            Fail();
            throw;
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() => Guard(inner.Flush);

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await inner.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            Fail();
            throw;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }
    }

    private void Guard(Action write)
    {
        try
        {
            write();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            Fail();
            throw;
        }
    }

    private void Fail()
    {
        if (Interlocked.Exchange(ref _failed, 1) == 0)
        {
            ThreadPool.QueueUserWorkItem(_ => onFailure());
        }
    }
}
