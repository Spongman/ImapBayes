using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;

namespace AE.Net.Mail
{
	public abstract class ObjectWHeaders
	{
		protected ObjectWHeaders()
		{
			this.Attachments = new List<Attachment>();
		}

		string _strRawHeaders;
		public virtual string RawHeaders
		{
			get => _strRawHeaders;
			internal set
			{
				_strRawHeaders = value;
				_headers = null;
				_encoding = null;
			}
		}

		HeaderDictionary _headers;
		public virtual HeaderDictionary Headers => _headers ?? (_headers = HeaderDictionary.Parse(RawHeaders, _defaultEncoding));
		public virtual string ContentTransferEncoding
		{
			get => Headers["Content-Transfer-Encoding"].Value ?? "";
			set => Headers.Set("Content-Transfer-Encoding", new HeaderValue(value));
		}

		public virtual string ContentType
		{
			get => Headers["Content-Type"].Value.NotEmpty("text/plain");
			set => Headers.Set("Content-Type", new HeaderValue(value));
		}

		public virtual string Charset => Headers["Content-Transfer-Encoding"]["charset"].NotEmpty(
					Headers["Content-Type"]["charset"]
				);

		protected Encoding _defaultEncoding = Encoding.GetEncoding(1252);
		protected Encoding _encoding;
		public virtual Encoding Encoding
		{
			get => _encoding ?? (_encoding = Utilities.ParseCharsetToEncoding(Charset, _defaultEncoding));
			set
			{
				_defaultEncoding = value ?? _defaultEncoding;
				if (_encoding != null) //Encoding has been initialized from the specified Charset
					_encoding = value ?? _defaultEncoding;
			}
		}

		public ICollection<Attachment> Attachments { get; }

		public virtual string Body { get; set; }

		internal void SetBody(string value)
		{
			if (ContentTransferEncoding.Is("quoted-printable"))
			{
				value = Utilities.DecodeQuotedPrintable(value, Encoding);
			}
			else if (ContentTransferEncoding.Is("base64")
				//only decode the content if it is a text document
						  && ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
						  && Utilities.IsValidBase64String(ref value))
			{
				var data = Convert.FromBase64String(value);
				using (var mem = new System.IO.MemoryStream(data))
				using (var str = new System.IO.StreamReader(mem, Encoding))
				{
					value = str.ReadToEnd();
				}

				ContentTransferEncoding = "";
			}

			Body = value;
		}

		internal void SetBody(byte[] data)
		{
			ContentTransferEncoding = "base64";
			Body = Convert.ToBase64String(data);
		}

		public virtual string Parse(Stream reader, char? termChar = null, Encoding encoding = null, Stack<string> rgBoundaries = null)
		{
			rgBoundaries = rgBoundaries ?? new Stack<string>();

			encoding = encoding ?? _defaultEncoding;

			//var level = rgBoundaries.Count;

			var headers = new StringBuilder();
			string line;
			while ((line = reader.ReadLine(encoding, termChar)) != null)
			{
				//Debug.WriteLine("{0} : header : {1}", level, line);
				if (line.Trim(new[] { ' ' }).Length == 0)
				{
					if (headers.Length == 0)
						continue;
					break;
				}

				headers.AppendLine(line);
			}
			RawHeaders = headers.ToString();

			encoding = _encoding ?? encoding;

			var body = new StringBuilder();
			try
			{
				string boundary = Headers.GetBoundary();
				if (!string.IsNullOrEmpty(boundary))
				{
					string bounderInner = "--" + boundary;

					rgBoundaries.Push(bounderInner);

					while ((line = reader.ReadLine(encoding, termChar)) != null)
					{
						//Debug.WriteLine("{0} : pre : {1}", level, line);
						if (line.StartsWith("--") && rgBoundaries.Any(line.StartsWith))
							break;

						body.AppendLine(line);
					}

					while (line == bounderInner)
					{
						var part = new Attachment { Encoding = encoding };
						line = part.Parse(reader, termChar, encoding, rgBoundaries);
						Attachments.Add(part);
					}

					rgBoundaries.Pop();

					if (line == null || !line.StartsWith(bounderInner))
						return line;
				}

				while ((line = reader.ReadLine(encoding, termChar)) != null)
				{
					//Debug.WriteLine("{0} : post : {1}", level, line);
					if (line.StartsWith("--") && rgBoundaries.Any(line.StartsWith))
						return line;

					body.AppendLine(line);
				}
			}
			finally
			{
				SetBody(body.ToString());
			}

			return null;
		}
	}
}
