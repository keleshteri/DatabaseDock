using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseDock.Models
{
    /// <summary>
    /// Represents a multiplexed stream from Docker container logs
    /// </summary>
    public class MultiplexedStream
    {
        private readonly Stream _stream;

        public MultiplexedStream(Stream stream)
        {
            _stream = stream;
        }

        public async Task<(byte[] Data, bool EOF)> ReadOutputAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            byte[] header = new byte[8];
            int headerBytesRead = await _stream.ReadAsync(header, 0, header.Length, cancellationToken);

            if (headerBytesRead == 0)
            {
                return (Array.Empty<byte>(), true); // EOF
            }

            int bytesToRead = Math.Min(count, BitConverter.ToInt32(header, 4));
            int bytesRead = await _stream.ReadAsync(buffer, offset, bytesToRead, cancellationToken);

            return (buffer, false);
        }
    }
}
