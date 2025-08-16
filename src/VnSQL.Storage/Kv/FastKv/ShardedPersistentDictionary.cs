// FastKV.Advanced.cs (fully documented)
// Target: .NET 7+
//
// This single-file implementation provides a high-performance, crash-safe, thread-safe
// log-structured key–value store with:
//   - PersistentDictionary<TKey, TValue> (single file)
//   - ShardedPersistentDictionary<TKey, TValue> (multiple files for parallelism)
//   - Pluggable key codecs and value serializers
//   - Batch upsert/delete, seeding, snapshot/scan, compaction
//   - IDictionary<TKey,TValue> compatibility so it can be used as a drop-in dictionary
//
// Record layout (little-endian):
//   [int32 payloadLen]
//   [byte op]                 // 1=PUT, 2=DEL
//   [int32 keyLen]
//   [int32 valLen]            // 0 for DEL
//   [key bytes]
//   [value bytes]             // omitted for DEL
//   [int32 payloadLen]
//
// Crash recovery: during startup we scan the file verifying prefix/suffix lengths.
// If a partial record is found, we truncate at the last known-good boundary.
//
// Threading model:
//   - Multi-reader via ReaderWriterLockSlim (read lock per TryGet/read path)
//   - Single writer serialized by SemaphoreSlim
//   - Compaction acquires write lock to stop the world, then atomically swaps files
//
// Performance notes:
//   - Append-only writes are fast, and RandomAccess.Read avoids sharing a mutable Position
//   - In-memory index stores only (offset, length, deleted flag)
//   - Batch APIs coalesce writes into ~8MB chunks by default to reduce syscalls/LOH

using Microsoft.Win32.SafeHandles;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace VnSQL.Storage.Kv.FastKv
{
    // =============================================================
    // Key/Value plug-ins
    // =============================================================

    /// <summary>
    /// Encodes/decodes keys and provides equality + a 64-bit hash used for sharding.
    /// </summary>
    public interface IKeyCodec<TKey>
    {
        /// <summary>Encode a key to raw bytes.</summary>
        ReadOnlyMemory<byte> Encode(TKey key);
        /// <summary>Decode a key from raw bytes.</summary>
        TKey Decode(ReadOnlySpan<byte> data);
        /// <summary>Equality comparer used for in-memory index & snapshots.</summary>
        IEqualityComparer<TKey> Comparer { get; }
        /// <summary>64-bit hash for sharding; must be stable across processes/OS.</summary>
        ulong Hash64(TKey key);
    }

    /// <summary>
    /// Serializes/deserializes values to/from raw bytes.
    /// </summary>
    public interface IValueSerializer<TValue>
    {
        /// <summary>Serialize a value to bytes.</summary>
        ReadOnlyMemory<byte> Serialize(TValue value);
        /// <summary>Deserialize a value from bytes.</summary>
        TValue Deserialize(ReadOnlySpan<byte> data);
    }

    // -------- Built-in key codecs --------

    /// <summary>String key codec (UTF-8 + FNV-1a hash).</summary>
    public sealed class StringKeyCodec : IKeyCodec<string>
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        public IEqualityComparer<string> Comparer { get; } = StringComparer.Ordinal;
        public ReadOnlyMemory<byte> Encode(string key) => Utf8.GetBytes(key ?? string.Empty);
        public string Decode(ReadOnlySpan<byte> data) => Utf8.GetString(data);
        public ulong Hash64(string key)
        {
            if (key is null) return 0;
            ulong h = 1469598103934665603UL; // FNV offset basis
            var span = Utf8.GetBytes(key);
            for (int i = 0; i < span.Length; i++) { h ^= span[i]; h *= 1099511628211UL; }
            return h;
        }
    }

    /// <summary>byte[] key codec (raw + FNV-1a hash).</summary>
    public sealed class BytesKeyCodec : IKeyCodec<byte[]>
    {
        public IEqualityComparer<byte[]> Comparer { get; } = new ByteArrayComparer();
        public ReadOnlyMemory<byte> Encode(byte[] key) => key ?? Array.Empty<byte>();
        public byte[] Decode(ReadOnlySpan<byte> data) => data.ToArray();
        public ulong Hash64(byte[] key)
        {
            if (key == null) return 0;
            ulong h = 1469598103934665603UL;
            for (int i = 0; i < key.Length; i++) { h ^= key[i]; h *= 1099511628211UL; }
            return h;
        }
        private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[]? x, byte[]? y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null || x.Length != y.Length) return false;
                return x.AsSpan().SequenceEqual(y);
            }
            public int GetHashCode(byte[] obj)
            {
                unchecked
                {
                    uint h = 2166136261u;
                    foreach (var b in obj) { h ^= b; h *= 16777619u; }
                    return (int)h;
                }
            }
        }
    }

    /// <summary>Guid key codec (16 bytes + FNV-1a hash).</summary>
    public sealed class GuidKeyCodec : IKeyCodec<Guid>
    {
        public IEqualityComparer<Guid> Comparer { get; } = EqualityComparer<Guid>.Default;
        public ReadOnlyMemory<byte> Encode(Guid key) { var buf = new byte[16]; key.TryWriteBytes(buf); return buf; }
        public Guid Decode(ReadOnlySpan<byte> data) => new Guid(data);
        public ulong Hash64(Guid key)
        {
            Span<byte> b = stackalloc byte[16];
            key.TryWriteBytes(b);
            ulong h = 1469598103934665603UL; for (int i = 0; i < 16; i++) { h ^= b[i]; h *= 1099511628211UL; }
            return h;
        }
    }

    /// <summary>Int64 key codec (little-endian + multiplicative hash).</summary>
    public sealed class Int64KeyCodec : IKeyCodec<long>
    {
        public IEqualityComparer<long> Comparer { get; } = EqualityComparer<long>.Default;
        public ReadOnlyMemory<byte> Encode(long key) { var buf = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(buf, key); return buf; }
        public long Decode(ReadOnlySpan<byte> data) => BinaryPrimitives.ReadInt64LittleEndian(data);
        public ulong Hash64(long key) => unchecked((ulong)key * 11400714819323198485UL);
    }

    // -------- Built-in value serializers --------

    /// <summary>Value serializer for byte[] (pass-through).</summary>
    public sealed class BytesSerializer : IValueSerializer<byte[]>
    {
        public ReadOnlyMemory<byte> Serialize(byte[] value) => value ?? Array.Empty<byte>();
        public byte[] Deserialize(ReadOnlySpan<byte> data) => data.ToArray();
    }

    /// <summary>Value serializer for string (UTF-8).</summary>
    public sealed class Utf8StringSerializer : IValueSerializer<string>
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        public ReadOnlyMemory<byte> Serialize(string value) => Utf8.GetBytes(value ?? string.Empty);
        public string Deserialize(ReadOnlySpan<byte> data) => Utf8.GetString(data);
    }

    /// <summary>Value serializer using System.Text.Json.</summary>
    public sealed class JsonSerializerAdapter<T> : IValueSerializer<T>
    {
        private readonly JsonSerializerOptions _opt;
        public JsonSerializerAdapter(JsonSerializerOptions? opt = null) => _opt = opt ?? new JsonSerializerOptions { WriteIndented = false };
        public ReadOnlyMemory<byte> Serialize(T value) => JsonSerializer.SerializeToUtf8Bytes(value, _opt);
        public T Deserialize(ReadOnlySpan<byte> data) => JsonSerializer.Deserialize<T>(data, _opt)!;
    }

    // =============================================================
    // PersistentDictionary<TKey, TValue>
    // =============================================================

    /// <summary>
    /// Disk-backed, log-structured dictionary with single append-only file.
    /// Crash-safe, thread-safe (multi-reader, single-writer). Implements IDictionary.
    /// </summary>
    public sealed class PersistentDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDisposable
    {
        // Operation tags
        private const byte OP_PUT = 1;
        private const byte OP_DEL = 2;

        // File state
        private readonly string _path;
        private SafeFileHandle _handle;
        private long _length;
        private volatile bool _disposed;

        // Pluggable codecs
        private readonly IKeyCodec<TKey> _keyCodec;
        private readonly IValueSerializer<TValue> _valCodec;
        private readonly bool _writeThrough;

        // Concurrency controls
        private readonly ReaderWriterLockSlim _gate = new(LockRecursionPolicy.NoRecursion);
        private readonly SemaphoreSlim _writer = new(1, 1);

        // In-memory index: key -> (offset,length,deleted)
        private readonly ConcurrentDictionary<TKey, IndexEntry> _index;
        private readonly byte[] _i4 = new byte[4]; // scratch for int reads

        private readonly struct IndexEntry
        {
            public readonly long ValueOffset;
            public readonly int ValueLength;
            public readonly bool IsDeleted;
            public IndexEntry(long off, int len, bool del) { ValueOffset = off; ValueLength = len; IsDeleted = del; }
        }

        /// <summary>
        /// Create or open a persistent dictionary on a given file path.
        /// </summary>
        public PersistentDictionary(
            string filePath,
            IKeyCodec<TKey>? keyCodec = null,
            IValueSerializer<TValue>? valueSerializer = null,
            bool writeThrough = false)
        {
            _path = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);

            _keyCodec = keyCodec ?? DefaultKeyCodec();
            _valCodec = valueSerializer ?? DefaultValueCodec();
            _writeThrough = writeThrough;

            _index = new ConcurrentDictionary<TKey, IndexEntry>(_keyCodec.Comparer);

            var opts = FileOptions.RandomAccess | (writeThrough ? FileOptions.WriteThrough : FileOptions.None);
            _handle = File.OpenHandle(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, options: opts);
            _length = GetFileLength();
            RecoverAndBuildIndex();
        }

        // ---------------- IDictionary ----------------

        /// <inheritdoc/>
        public TValue this[TKey key]
        {
            get
            {
                if (!TryGet(key, out var v)) throw new KeyNotFoundException();
                return v;
            }
            set => PutAsync(key, value).GetAwaiter().GetResult();
        }

        /// <summary>Total number of entries visible (live keys) — exposed as long.</summary>
        public long Count => _index.Count;

        int ICollection<KeyValuePair<TKey, TValue>>.Count => (int)Math.Min(int.MaxValue, Count);
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        /// <inheritdoc/>
        public ICollection<TKey> Keys => SnapshotToDictionary().Keys;
        /// <inheritdoc/>
        public ICollection<TValue> Values => SnapshotToDictionary().Values;

        /// <inheritdoc/>
        public void Add(TKey key, TValue value) => PutAsync(key, value).GetAwaiter().GetResult();
        /// <inheritdoc/>
        public bool ContainsKey(TKey key) => _index.TryGetValue(key, out var e) && !e.IsDeleted;
        /// <inheritdoc/>
        public bool Remove(TKey key) => DeleteAsync(key).GetAwaiter().GetResult();
        /// <inheritdoc/>
        public bool TryGetValue(TKey key, out TValue value) => TryGet(key, out value);

        /// <inheritdoc/>
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
        /// <inheritdoc/>
        public void Clear() => SeedAsync(Array.Empty<KeyValuePair<TKey, TValue>>(), truncateExisting: true).GetAwaiter().GetResult();
        /// <inheritdoc/>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (!TryGet(item.Key, out var v)) return false;
            return EqualityComparer<TValue>.Default.Equals(v, item.Value);
        }
        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach (var kv in SnapshotToDictionary()) array[arrayIndex++] = kv;
        }
        /// <inheritdoc/>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (Contains(item)) return Remove(item.Key);
            return false;
        }
        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => ScanLiveItems().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        // ---------------- Core operations ----------------

        /// <summary>
        /// Try to get a value by key without throwing.
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            value = default!;
            _gate.EnterReadLock();
            try
            {
                if (!_index.TryGetValue(key, out var e) || e.IsDeleted) return false;
                var buf = new byte[e.ValueLength];
                var r = RandomAccess.Read(_handle, buf, e.ValueOffset);
                if (r != e.ValueLength) throw new IOException("Short read");
                value = _valCodec.Deserialize(buf);
                return true;
            }
            finally { _gate.ExitReadLock(); }
        }

        /// <summary>
        /// Upsert a single key/value. Durable if <paramref name="writeThrough"/> was set in ctor or if you call <see cref="Flush"/> later.
        /// </summary>
        public Task PutAsync(TKey key, TValue value, CancellationToken ct = default)
            => PutBatchAsync(new[] { new KeyValuePair<TKey, TValue>(key, value) }, flush: _writeThrough, ct);

        /// <summary>
        /// Delete a key if it exists. Returns true if a live key was deleted.
        /// </summary>
        public async Task<bool> DeleteAsync(TKey key, CancellationToken ct = default)
        {
            _gate.EnterReadLock();
            try
            {
                if (!_index.TryGetValue(key, out var e) || e.IsDeleted)
                    return false;

                var k = _keyCodec.Encode(key).ToArray();
                int payloadLen = 1 + 4 + 4 + k.Length; // valLen=0 for DEL
                int recLen = 4 + payloadLen + 4;
                var buf = new byte[recLen];
                int p = 0;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), payloadLen); p += 4;
                buf[p++] = OP_DEL;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), k.Length); p += 4;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), 0); p += 4;
                k.AsSpan().CopyTo(buf.AsSpan(p)); p += k.Length;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), payloadLen);

                await _writer.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var off = _length;
                    RandomAccess.Write(_handle, buf, off);
                    _length += buf.Length;
                    if (_writeThrough) FlushToDisk();
                }
                finally { _writer.Release(); }

                _index[key] = new IndexEntry(0, 0, del: true);
                return true;
            }
            finally { _gate.ExitReadLock(); }
        }

        // ---------------- Batch APIs ----------------

        /// <summary>
        /// Batch upsert. Internally chunks data (~8MB) into one contiguous buffer per chunk
        /// to reduce syscalls and LOH allocations. After write completes, in-memory index is updated.
        /// </summary>
        public async Task PutBatchAsync(IEnumerable<KeyValuePair<TKey, TValue>> items, bool flush = false, CancellationToken ct = default)
        {
            const int MaxChunkBytes = 8 * 1024 * 1024; // 8 MB
            List<(TKey key, byte[] k, byte[] v)> batch = new();
            int currentSize = 0;

            void FlushChunk()
            {
                if (batch.Count == 0) return;
                WriteEncodedRecords(batch, flush || _writeThrough, ct).GetAwaiter().GetResult();
                batch.Clear();
                currentSize = 0;
            }

            foreach (var kv in items)
            {
                ct.ThrowIfCancellationRequested();
                var kb = _keyCodec.Encode(kv.Key).ToArray();
                var vb = _valCodec.Serialize(kv.Value).ToArray();
                int payloadLen = 1 + 4 + 4 + kb.Length + vb.Length;
                int recLen = 4 + payloadLen + 4;
                if (currentSize + recLen > MaxChunkBytes) FlushChunk();
                batch.Add((kv.Key, kb, vb));
                currentSize += recLen;
            }
            FlushChunk();
        }

        /// <summary>
        /// Batch delete. Records are appended as DEL operations; index marks entries deleted.
        /// </summary>
        public async Task DeleteBatchAsync(IEnumerable<TKey> keys, bool flush = false, CancellationToken ct = default)
        {
            const int MaxChunkBytes = 8 * 1024 * 1024;
            List<(TKey key, byte[] k)> batch = new();
            int currentSize = 0;

            void FlushChunk()
            {
                if (batch.Count == 0) return;
                WriteEncodedDeletes(batch, flush || _writeThrough, ct).GetAwaiter().GetResult();
                batch.Clear();
                currentSize = 0;
            }

            foreach (var key in keys)
            {
                ct.ThrowIfCancellationRequested();
                var kb = _keyCodec.Encode(key).ToArray();
                int payloadLen = 1 + 4 + 4 + kb.Length;
                int recLen = 4 + payloadLen + 4;
                if (currentSize + recLen > MaxChunkBytes) FlushChunk();
                batch.Add((key, kb));
                currentSize += recLen;
            }
            FlushChunk();
        }

        /// <summary>
        /// Seed the store with a large dataset. If <paramref name="truncateExisting"/> is true,
        /// the file is truncated and index cleared before writing.
        /// </summary>
        public async Task SeedAsync(IEnumerable<KeyValuePair<TKey, TValue>> items, bool truncateExisting, CancellationToken ct = default)
        {
            _gate.EnterWriteLock();
            await _writer.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (truncateExisting)
                {
                    using var fs = new FileStream(_handle, FileAccess.Write);
                    fs.SetLength(0);
                    _index.Clear();
                    _length = 0;
                }
            }
            finally
            {
                _writer.Release();
                _gate.ExitWriteLock();
            }

            await PutBatchAsync(items, flush: true, ct);
        }

        // ---------------- Snapshots & scans ----------------

        /// <summary>
        /// Stream all live items as a stable snapshot (values are re-read from disk).
        /// </summary>
        public IEnumerable<KeyValuePair<TKey, TValue>> ScanLiveItems()
        {
            var snap = _index.ToArray();
            foreach (var e in snap)
            {
                if (e.Value.IsDeleted) continue;
                if (TryGet(e.Key, out var v)) yield return new KeyValuePair<TKey, TValue>(e.Key, v);
            }
        }

        /// <summary>
        /// Materialize a RAM dictionary from the current live view.
        /// </summary>
        public Dictionary<TKey, TValue> SnapshotToDictionary()
        {
            var dict = new Dictionary<TKey, TValue>(_keyCodec.Comparer);
            foreach (var kv in ScanLiveItems()) dict[kv.Key] = kv.Value;
            return dict;
        }

        // ---------------- Maintenance ----------------

        /// <summary>
        /// Compact the file by rewriting only the latest live entries to a new file and atomically replacing the old one.
        /// </summary>
        public async Task CompactAsync(CancellationToken ct = default)
        {
            _gate.EnterWriteLock();
            await _writer.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                string tmp = _path + ".compacting";
                if (File.Exists(tmp)) File.Delete(tmp);

                var opts = FileOptions.RandomAccess | (_writeThrough ? FileOptions.WriteThrough : FileOptions.None);
                var tmpHandle = File.OpenHandle(tmp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, options: opts);
                long newLen = 0;

                var snap = _index.ToArray();
                foreach (var kv in snap)
                {
                    ct.ThrowIfCancellationRequested();
                    if (kv.Value.IsDeleted) continue;

                    // read old value
                    var val = new byte[kv.Value.ValueLength];
                    var r = RandomAccess.Read(_handle, val, kv.Value.ValueOffset);
                    if (r != kv.Value.ValueLength) throw new IOException("Short read during compaction");

                    var kb = _keyCodec.Encode(kv.Key).ToArray();
                    int payloadLen = 1 + 4 + 4 + kb.Length + val.Length;
                    int recLen = 4 + payloadLen + 4;
                    var buf = new byte[recLen];
                    int p = 0;
                    BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), payloadLen); p += 4;
                    buf[p++] = OP_PUT;
                    BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), kb.Length); p += 4;
                    BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), val.Length); p += 4;
                    kb.AsSpan().CopyTo(buf.AsSpan(p)); p += kb.Length;
                    val.AsSpan().CopyTo(buf.AsSpan(p)); p += val.Length;
                    BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), payloadLen);

                    RandomAccess.Write(tmpHandle, buf, newLen);
                    newLen += buf.Length;
                }

                // fsync tmp and swap
                using (var fs = new FileStream(tmpHandle, FileAccess.Write)) { fs.Flush(true); }
                tmpHandle.Dispose();

                _handle.Dispose();
                File.Replace(tmp, _path, null);

                _handle = File.OpenHandle(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, options: opts);
                _index.Clear();
                _length = GetFileLength();
                RecoverAndBuildIndex();
            }
            finally
            {
                _writer.Release();
                _gate.ExitWriteLock();
            }
        }

        /// <summary>Force a durable flush to disk (fsync).</summary>
        public void Flush() => FlushToDisk();

        /// <summary>
        /// Dispose file handle and synchronization primitives.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _handle?.Dispose();
            _writer.Dispose();
            _gate.Dispose();
        }

        // ---------------- Internals ----------------

        /// <summary>
        /// Scan the file to rebuild index and truncate any partial tail.
        /// </summary>
        private void RecoverAndBuildIndex()
        {
            long pos = 0; long fileLen = _length;
            while (pos + 4 <= fileLen)
            {
                int prefixLen = ReadInt32At(pos);
                if (prefixLen <= 0) { Truncate(pos); break; }
                long payloadStart = pos + 4;
                long payloadEnd = payloadStart + prefixLen;
                long suffixPos = payloadEnd;
                if (suffixPos + 4 > fileLen) { Truncate(pos); break; }
                int suffixLen = ReadInt32At(suffixPos);
                if (suffixLen != prefixLen) { Truncate(pos); break; }

                Span<byte> head = stackalloc byte[1 + 4 + 4];
                RandomAccess.Read(_handle, head, payloadStart);
                byte op = head[0];
                int keyLen = BinaryPrimitives.ReadInt32LittleEndian(head[1..5]);
                int valLen = BinaryPrimitives.ReadInt32LittleEndian(head[5..9]);
                long keyPos = payloadStart + 1 + 4 + 4;
                long valPos = keyPos + keyLen;

                if (keyLen < 0 || valLen < 0 || valPos > payloadEnd || op == OP_PUT && valPos + valLen > payloadEnd)
                { Truncate(pos); break; }

                var keyBytes = new byte[keyLen];
                RandomAccess.Read(_handle, keyBytes, keyPos);
                var key = _keyCodec.Decode(keyBytes);

                if (op == OP_PUT) _index[key] = new IndexEntry(valPos, valLen, del: false);
                else if (op == OP_DEL) _index[key] = new IndexEntry(0, 0, del: true);
                else { Truncate(pos); break; }

                pos = suffixPos + 4;
            }

            if (pos != fileLen) _length = pos; // we truncated tail
        }

        /// <summary>
        /// Write a batch of already-encoded PUT records as one contiguous buffer, update index after.
        /// </summary>
        private async Task WriteEncodedRecords(List<(TKey key, byte[] k, byte[] v)> batch, bool flush, CancellationToken ct)
        {
            int total = 0;
            foreach (var it in batch) { int payload = 1 + 4 + 4 + it.k.Length + it.v.Length; total += 4 + payload + 4; }
            var buf = new byte[total];
            int p = 0;
            foreach (var it in batch)
            {
                int payload = 1 + 4 + 4 + it.k.Length + it.v.Length;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), payload); p += 4;
                buf[p++] = OP_PUT;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), it.k.Length); p += 4;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), it.v.Length); p += 4;
                it.k.AsSpan().CopyTo(buf.AsSpan(p)); p += it.k.Length;
                it.v.AsSpan().CopyTo(buf.AsSpan(p)); p += it.v.Length;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), payload); p += 4;
            }

            long writeOffset;
            _gate.EnterReadLock();
            await _writer.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                writeOffset = _length;
                RandomAccess.Write(_handle, buf, writeOffset);
                _length += buf.Length;
                if (flush) FlushToDisk();
            }
            finally { _writer.Release(); _gate.ExitReadLock(); }

            // Update index after durable write
            long cursor = writeOffset;
            foreach (var it in batch)
            {
                int payload = 1 + 4 + 4 + it.k.Length + it.v.Length;
                cursor += 4;                   // prefix
                cursor += 1 + 4 + 4;           // header
                cursor += it.k.Length;         // key
                long valueOff = cursor;        // value starts
                cursor += it.v.Length;         // value bytes
                cursor += 4;                   // suffix
                _index[it.key] = new IndexEntry(valueOff, it.v.Length, del: false);
            }
        }

        /// <summary>
        /// Write a batch of already-encoded DEL records as one contiguous buffer, then mark deleted in index.
        /// </summary>
        private async Task WriteEncodedDeletes(List<(TKey key, byte[] k)> batch, bool flush, CancellationToken ct)
        {
            int total = 0;
            foreach (var it in batch) { int payload = 1 + 4 + 4 + it.k.Length; total += 4 + payload + 4; }
            var buf = new byte[total];
            int p = 0;
            foreach (var it in batch)
            {
                int payload = 1 + 4 + 4 + it.k.Length;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), payload); p += 4;
                buf[p++] = OP_DEL;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), it.k.Length); p += 4;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), 0); p += 4;
                it.k.AsSpan().CopyTo(buf.AsSpan(p)); p += it.k.Length;
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p, 4), payload); p += 4;
            }

            _gate.EnterReadLock();
            await _writer.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var off = _length;
                RandomAccess.Write(_handle, buf, off);
                _length += buf.Length;
                if (flush) FlushToDisk();
            }
            finally { _writer.Release(); _gate.ExitReadLock(); }

            foreach (var it in batch) _index[it.key] = new IndexEntry(0, 0, del: true);
        }

        /// <summary>Read little-endian Int32 at offset, returning -1 if short read.</summary>
        private int ReadInt32At(long offset)
        {
            var span = _i4.AsSpan(0, 4);
            int read = RandomAccess.Read(_handle, span, offset);
            if (read != 4) return -1;
            return BinaryPrimitives.ReadInt32LittleEndian(span);
        }

        /// <summary>Get current file length via FileStream wrapper.</summary>
        private long GetFileLength()
        {
            try { using var fs = new FileStream(_handle, FileAccess.Read); return fs.Length; }
            catch { return 0; }
        }

        /// <summary>Truncate file to a new length.</summary>
        private void Truncate(long newLen)
        {
            using var fs = new FileStream(_handle, FileAccess.Write);
            fs.SetLength(newLen);
        }

        /// <summary>Force fsync.</summary>
        private void FlushToDisk()
        {
            using var fs = new FileStream(_handle, FileAccess.Write);
            fs.Flush(true);
        }

        /// <summary>Default key codec selection for common key types.</summary>
        private static IKeyCodec<TKey> DefaultKeyCodec()
        {
            var t = typeof(TKey);
            if (t == typeof(string)) return (IKeyCodec<TKey>)(object)new StringKeyCodec();
            if (t == typeof(byte[])) return (IKeyCodec<TKey>)(object)new BytesKeyCodec();
            if (t == typeof(Guid)) return (IKeyCodec<TKey>)(object)new GuidKeyCodec();
            if (t == typeof(long)) return (IKeyCodec<TKey>)(object)new Int64KeyCodec();
            throw new NotSupportedException($"No default IKeyCodec for {t.Name}. Provide a custom codec.");
        }

        /// <summary>Default value serializer selection for common value types.</summary>
        private static IValueSerializer<TValue> DefaultValueCodec()
        {
            var t = typeof(TValue);
            if (t == typeof(byte[])) return (IValueSerializer<TValue>)(object)new BytesSerializer();
            if (t == typeof(string)) return (IValueSerializer<TValue>)(object)new Utf8StringSerializer();
            return new JsonSerializerAdapter<TValue>();
        }
    }

    // =============================================================
    // ShardedPersistentDictionary<TKey, TValue>
    // =============================================================

    /// <summary>
    /// A facade over multiple <see cref="PersistentDictionary{TKey, TValue}"/> files, selected by a stable 64-bit hash.
    /// Implements IDictionary for ergonomic usage. Batch operations run in parallel per shard.
    /// </summary>
    public sealed class ShardedPersistentDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDisposable
    {
        private readonly PersistentDictionary<TKey, TValue>[] _shards;
        private readonly IKeyCodec<TKey> _keyCodec;

        /// <summary>Number of backing shards/files.</summary>
        public int ShardCount => _shards.Length;

        /// <summary>
        /// Create a sharded store with N shards where each shard is a separate file with suffix ".shardXX.log".
        /// </summary>
        public ShardedPersistentDictionary(
            string basePath,
            int shardCount,
            IKeyCodec<TKey>? keyCodec = null,
            IValueSerializer<TValue>? valueSerializer = null,
            bool writeThrough = false)
        {
            if (shardCount <= 0) throw new ArgumentOutOfRangeException(nameof(shardCount));
            _keyCodec = keyCodec ?? DefaultKeyCodec();

            _shards = new PersistentDictionary<TKey, TValue>[shardCount];
            for (int i = 0; i < shardCount; i++)
            {
                string path = basePath + $".shard{i:D2}.log";
                _shards[i] = new PersistentDictionary<TKey, TValue>(path, _keyCodec, valueSerializer, writeThrough);
            }
        }

        /// <summary>Dispose all shard dictionaries.</summary>
        public void Dispose() { foreach (var s in _shards) s.Dispose(); }

        /// <summary>Compute shard index from key hash.</summary>
        private int ShardOf(TKey key)
        {
            ulong h = _keyCodec.Hash64(key);
            return (int)(h % (uint)_shards.Length);
        }

        // --------------- IDictionary ---------------

        /// <inheritdoc/>
        public TValue this[TKey key]
        {
            get { if (!TryGetValue(key, out var v)) throw new KeyNotFoundException(); return v; }
            set => _shards[ShardOf(key)].PutAsync(key, value).GetAwaiter().GetResult();
        }

        /// <summary>Total live items across shards (as long).</summary>
        public long TotalCount { get { long sum = 0; foreach (var s in _shards) sum += s.Count; return sum; } }
        int ICollection<KeyValuePair<TKey, TValue>>.Count => (int)Math.Min(int.MaxValue, TotalCount);
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        /// <inheritdoc/>
        public ICollection<TKey> Keys => SnapshotToDictionary().Keys;
        /// <inheritdoc/>
        public ICollection<TValue> Values => SnapshotToDictionary().Values;

        /// <inheritdoc/>
        public void Add(TKey key, TValue value) => _shards[ShardOf(key)].PutAsync(key, value).GetAwaiter().GetResult();
        /// <inheritdoc/>
        public bool ContainsKey(TKey key) => _shards[ShardOf(key)].ContainsKey(key);
        /// <inheritdoc/>
        public bool Remove(TKey key) => _shards[ShardOf(key)].DeleteAsync(key).GetAwaiter().GetResult();
        /// <inheritdoc/>
        public bool TryGetValue(TKey key, out TValue value) => _shards[ShardOf(key)].TryGet(key, out value);

        /// <inheritdoc/>
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
        /// <inheritdoc/>
        public void Clear() { foreach (var s in _shards) s.SeedAsync(Array.Empty<KeyValuePair<TKey, TValue>>(), truncateExisting: true).GetAwaiter().GetResult(); }
        /// <inheritdoc/>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (!TryGetValue(item.Key, out var v)) return false;
            return EqualityComparer<TValue>.Default.Equals(v, item.Value);
        }
        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        { foreach (var kv in SnapshotToDictionary()) array[arrayIndex++] = kv; }
        /// <inheritdoc/>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        { if (Contains(item)) return Remove(item.Key); return false; }
        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => ScanAllLiveItems().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        // --------------- High-level APIs ---------------

        /// <summary>Async upsert on the appropriate shard.</summary>
        public Task PutAsync(TKey key, TValue value, CancellationToken ct = default)
            => _shards[ShardOf(key)].PutAsync(key, value, ct);

        /// <summary>Async delete on the appropriate shard.</summary>
        public Task<bool> DeleteAsync(TKey key, CancellationToken ct = default)
            => _shards[ShardOf(key)].DeleteAsync(key, ct);

        /// <summary>
        /// Batch upsert grouped by shard; executes per-shard batches in parallel.
        /// </summary>
        public async Task PutBatchAsync(IEnumerable<KeyValuePair<TKey, TValue>> items, bool flush = false, CancellationToken ct = default)
        {
            var groups = new Dictionary<int, List<KeyValuePair<TKey, TValue>>>();
            foreach (var kv in items)
            {
                int s = ShardOf(kv.Key);
                if (!groups.TryGetValue(s, out var list)) groups[s] = list = new();
                list.Add(kv);
            }
            var tasks = new List<Task>(groups.Count);
            foreach (var kv in groups) tasks.Add(_shards[kv.Key].PutBatchAsync(kv.Value, flush, ct));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Batch delete grouped by shard; executes per-shard batches in parallel.
        /// </summary>
        public async Task DeleteBatchAsync(IEnumerable<TKey> keys, bool flush = false, CancellationToken ct = default)
        {
            var groups = new Dictionary<int, List<TKey>>();
            foreach (var k in keys)
            {
                int s = ShardOf(k);
                if (!groups.TryGetValue(s, out var list)) groups[s] = list = new();
                list.Add(k);
            }
            var tasks = new List<Task>(groups.Count);
            foreach (var kv in groups) tasks.Add(_shards[kv.Key].DeleteBatchAsync(kv.Value, flush, ct));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Iterate all live items across shards as a stream.
        /// </summary>
        public IEnumerable<KeyValuePair<TKey, TValue>> ScanAllLiveItems()
        {
            foreach (var s in _shards)
                foreach (var kv in s.ScanLiveItems())
                    yield return kv;
        }

        /// <summary>
        /// Build a RAM dictionary containing all live items across shards.
        /// </summary>
        public Dictionary<TKey, TValue> SnapshotToDictionary()
        {
            var dict = new Dictionary<TKey, TValue>(_keyCodec.Comparer);
            foreach (var kv in ScanAllLiveItems()) dict[kv.Key] = kv.Value;
            return dict;
        }

        /// <summary>
        /// Compact all shards in parallel.
        /// </summary>
        public async Task CompactAllAsync(CancellationToken ct = default)
        {
            var tasks = new Task[_shards.Length];
            for (int i = 0; i < _shards.Length; i++) tasks[i] = _shards[i].CompactAsync(ct);
            await Task.WhenAll(tasks);
        }

        /// <summary>Default key codec selection for common key types.</summary>
        private static IKeyCodec<TKey> DefaultKeyCodec()
        {
            var t = typeof(TKey);
            if (t == typeof(string)) return (IKeyCodec<TKey>)(object)new StringKeyCodec();
            if (t == typeof(byte[])) return (IKeyCodec<TKey>)(object)new BytesKeyCodec();
            if (t == typeof(Guid)) return (IKeyCodec<TKey>)(object)new GuidKeyCodec();
            if (t == typeof(long)) return (IKeyCodec<TKey>)(object)new Int64KeyCodec();
            throw new NotSupportedException($"No default IKeyCodec for {t.Name}. Provide a custom codec.");
        }
    }
}
