﻿using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace BosunReporter.Infrastructure
{
    /// <summary>
    /// An opaque type which is used internally to handle the serialization of metrics.
    /// </summary>
    public class MetricWriter
    {
        const int DIGITS_IN_TIMESTAMP = 13; // all dates within a reasonable range 2000-2250 generate 13 decimal digit timestamps

        static readonly byte[] s_openCurlyMetricColon;
        static readonly byte[] s_commaValueColon;
        static readonly byte[] s_commaTagsColon;
        static readonly byte[] s_commaTimestampColon;
        static readonly byte[] s_closeCurlyComma;

        static readonly DateTime s_unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        static readonly DateTime s_minimumTimestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        static readonly DateTime s_maximumTimestamp = new DateTime(2250, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Payload _payload;
        readonly PayloadQueue _queue;

        // Denormalized references to the payload data just to remove an extra layer of indirection.
        // It's also useful to keep _payload.Used in its original state until we finalize the payload.
        int _used;

        byte[] _data;

        int _startOfWrite;
        int _payloadsCount;
        int _bytesWrittenByPreviousPayloads;

        DateTime _timestampCache = DateTime.MaxValue;
        readonly byte[] _timestampStringCache = new byte[DIGITS_IN_TIMESTAMP];

        int BytesWrittenToCurrentPayload => _payload == null ? 0 : _used - _payload.Used;
        internal int TotalBytesWritten => _bytesWrittenByPreviousPayloads + BytesWrittenToCurrentPayload;

        internal int MetricsCount { get; private set; }

        static MetricWriter()
        {
            var ascii = new ASCIIEncoding();

            s_openCurlyMetricColon = ascii.GetBytes("{\"metric\":\"");
            s_commaValueColon = ascii.GetBytes("\",\"value\":");
            s_commaTagsColon = ascii.GetBytes(",\"tags\":");
            s_commaTimestampColon = ascii.GetBytes(",\"timestamp\":");
            s_closeCurlyComma = ascii.GetBytes("},");
        }

        internal MetricWriter(PayloadQueue queue)
        {
            _queue = queue;
        }

        internal void EndBatch()
        {
            FinalizeAndSendPayload();
            _queue.SetBatchPayloadCount(_payloadsCount);
            _payloadsCount = 0;
        }

        internal void AddMetric(string name, string suffix, double value, string tagsJson, DateTime timestamp)
        {
            MarkStartOfWrite();

            Append(s_openCurlyMetricColon);
            Append(name);
            if (!string.IsNullOrEmpty(suffix))
                Append(suffix);
            Append(s_commaValueColon);
            Append(value);
            Append(s_commaTagsColon);
            Append(tagsJson);
            Append(s_commaTimestampColon);
            Append(timestamp);
            Append(s_closeCurlyComma);

            EndOfWrite();
        }

        void Append(byte[] bytes)
        {
            EnsureRoomFor(bytes.Length);
            Array.Copy(bytes, 0, _data, _used, bytes.Length);
            _used += bytes.Length;
        }

        void Append(string s)
        {
            var len = s.Length;
            EnsureRoomFor(len);

            var data = _data;
            var used = _used;
            for (var i = 0; i < len; i++, used++)
            {
                data[used] = (byte)s[i];
            }

            _used = used;
        }

        void Append(double d)
        {
            Append(d.ToString("R", CultureInfo.InvariantCulture)); // todo - use Grisu
        }

        void Append(DateTime timestamp)
        {
            if (timestamp != _timestampCache)
                SetTimestampCache(timestamp);

            Append(_timestampStringCache);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void MarkStartOfWrite()
        {
            _startOfWrite = _used;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EndOfWrite()
        {
            MetricsCount++;

            if (_used + 150 >= _data.Length)
            {
                // If there aren't at least 150 bytes left in the buffer,
                // there probably isn't enough room to write another metric,
                // so we're just going to flush it now.
                FinalizeAndSendPayload();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EnsureRoomFor(int length)
        {
            if (_data == null || _used + length > _data.Length)
            {
                SwapPayload();
                AssertLength(_data, _used + length);
            }
        }

        void SwapPayload()
        {
            var newPayload = _queue.GetPayloadForMetricWriter();
            var newData = newPayload.Data;
            var newUsed = newPayload.Used;

            if (newUsed == 0)
            {
                // payload is fresh, so we make the first character an open bracket
                newData[0] = (byte)'[';
                newUsed++;
            }
            else
            {
                // we're reusing a previously finalized payload, so we need to turn the close bracket into a comma
                newData[newUsed - 1] = (byte)',';
            }


            if (_data != null)
            {
                var oldStartOfWrite = _startOfWrite;
                _startOfWrite = newUsed;

                if (_used > oldStartOfWrite)
                {
                    // We started writing a metric to the old payload, but ran out of room.
                    // Need to copy what we started to the new payload.
                    var len = _used - oldStartOfWrite;
                    AssertLength(newData, newUsed + len);
                    Array.Copy(_data, oldStartOfWrite, newData, newUsed, len);
                    newUsed += len;

                    _used = oldStartOfWrite; // don't want an incomplete metric in the old buffer
                }

                FinalizeAndSendPayload();
            }

            _payload = newPayload;
            _used = newUsed;
            _data = newData;
            MetricsCount = newPayload.MetricsCount;
        }

        static void AssertLength(byte[] array, int length)
        {
            if (array.Length < length)
                throw new Exception($"BosunReporter is trying to write something way too big. This shouldn't happen. Are you using a crazy number of tags on a metric? Length {length}.");
        }

        void FinalizeAndSendPayload()
        {
            if (_used > 1 && _data != null)
            {
                // need to change the last character from a comma to a close bracket
                _data[_used - 1] = (byte)']';

                var payload = _payload;
                _bytesWrittenByPreviousPayloads += BytesWrittenToCurrentPayload;

                // update the Used property of the payload
                payload.Used = _used;
                payload.MetricsCount = MetricsCount;

                _queue.AddPendingPayload(payload);
                _payloadsCount++;
            }

            _payload = null;
            _used = 0;
            _data = null;
            MetricsCount = 0;
        }

        void SetTimestampCache(DateTime timestamp)
        {
            if (timestamp < s_minimumTimestamp)
                throw new Exception($"BosunReporter cannot serialize metrics dated before {s_minimumTimestamp}.");

            if (timestamp > s_maximumTimestamp)
                throw new Exception($"BosunReporter cannot serialize metrics dated after {s_maximumTimestamp}.");

            var bytes = _timestampStringCache;
            var val = (long)(timestamp - s_unixEpoch).TotalMilliseconds;
            for (var i = DIGITS_IN_TIMESTAMP - 1; i >= 0; i--)
            {
                bytes[i] = (byte)(val % 10 + '0');
                val /= 10;
            }

            _timestampCache = timestamp;
        }
    }
}