using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AE.Net.Mail
{
	public class LimitedInputStream : Stream
	{
		readonly Stream _stream;
		long _cbRemaining;
		readonly bool _fFixedLength;
		readonly bool _fOwnsStream;

		public LimitedInputStream(Stream stream, long cbLength, bool fFixedLength = true, bool fOwnsStream = false)
		{
			_stream = stream;
			Length = _cbRemaining = cbLength;
			_fFixedLength = fFixedLength;
			_fOwnsStream = fOwnsStream;
		}

		public override bool CanRead => _stream.CanRead;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override void Flush() { }

		public override long Length { get; }
		public override long Position
		{
			get => Length - _cbRemaining;
			set => throw new InvalidOperationException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			var cbRemaining = _cbRemaining;
			if (cbRemaining == 0)
				return 0;

			if (cbRemaining < int.MaxValue && count > cbRemaining)
				count = (int)cbRemaining;

			int cbRead = _stream.Read(buffer, offset, count);
			_cbRemaining -= cbRead;
			return cbRead;
		}

		public override int ReadByte()
		{
			if (_cbRemaining == 0)
				return -1;

			int result = _stream.ReadByte();
			if (result != -1)
				_cbRemaining--;

			return result;
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new InvalidOperationException();

		public override void SetLength(long value) => throw new InvalidOperationException();

		public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException();

		static readonly byte[] _rgbTemp = new byte[1024];

		protected override void Dispose(bool disposing)
		{
			if (_fFixedLength)
			{
				while (_cbRemaining > 0)
					Read(_rgbTemp, 0, _rgbTemp.Length);
			}

			if (_fOwnsStream)
				_stream.Dispose();

			base.Dispose(disposing);
		}
	}
}
