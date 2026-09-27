using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace WinUIMusicPlayer.Helper;

/// <summary>可克隆的只读文件映射；读者共享文件页，清理缓存不影响已打开的封面。</summary>
internal sealed class ReadOnlyMappedStream : IRandomAccessStream
{
    private readonly SharedMapping _mapping;
    private readonly MemoryMappedViewStream _view;
    private IInputStream? _reader;
    private bool _disposed;

    private ReadOnlyMappedStream(SharedMapping mapping)
    {
        _mapping = mapping;
        _view = mapping.OpenView();
    }

    internal static IRandomAccessStream Open(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, 1, FileOptions.None);
        ulong length = (ulong)file.Length;
        if (length == 0) throw new InvalidDataException("封面文件为空。");
        var mapped = MemoryMappedFile.CreateFromFile(file, null, 0,
            MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
        return new ReadOnlyMappedStream(new SharedMapping(mapped, length));
    }

    public bool CanRead => !_disposed;
    public bool CanWrite => false;
    public ulong Position => (ulong)_view.Position;
    public ulong Size { get => _mapping.Length; set => throw new NotSupportedException(); }
    public void Seek(ulong position) => _view.Position = checked((long)position);
    public IRandomAccessStream CloneStream()
    {
        lock (_mapping)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new ReadOnlyMappedStream(_mapping);
        }
    }
    public IInputStream GetInputStreamAt(ulong position)
    {
        var clone = CloneStream();
        try { clone.Seek(position); return clone; }
        catch { clone.Dispose(); throw; }
    }
    public IOutputStream GetOutputStreamAt(ulong position) => throw new NotSupportedException();
    public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
    {
        lock (_mapping)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // .NET 的 WinRT 适配器有自己的缓冲区；只有真正读取的克隆才创建它。
            _reader ??= _view.AsInputStream();
            return _reader.ReadAsync(buffer, count, options);
        }
    }
    public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer) => throw new NotSupportedException();
    public IAsyncOperation<bool> FlushAsync() => throw new NotSupportedException();
    public void Dispose()
    {
        lock (_mapping)
        {
            if (_disposed) return;
            _disposed = true;
            try { _reader?.Dispose(); }
            finally
            {
                try { _view.Dispose(); }
                finally { _mapping.Release(); }
            }
        }
    }

    private sealed class SharedMapping(MemoryMappedFile file, ulong length)
    {
        internal ulong Length { get; } = length;
        private int _readers;
        private bool _closed;
        internal MemoryMappedViewStream OpenView()
        {
            lock (this)
            {
                ObjectDisposedException.ThrowIf(_closed, this);
                _readers++;
                MemoryMappedViewStream? view = null;
                try
                {
                    view = file.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
                    return view;
                }
                catch
                {
                    view?.Dispose();
                    Release();
                    throw;
                }
            }
        }
        internal void Release()
        {
            lock (this)
            {
                if (--_readers != 0) return;
                _closed = true;
                file.Dispose();
            }
        }
    }
}
