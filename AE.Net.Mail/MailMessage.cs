using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace AE.Net.Mail
{
	public enum MailPriority
	{
		Normal = 3,
		High = 5,
		Low = 1
	}

	[DebuggerDisplay("{Subject}")]
	public class MailMessage : ObjectWHeaders
	{
		public static implicit operator System.Net.Mail.MailMessage(MailMessage msg)
		{
			var ret = new System.Net.Mail.MailMessage()
			{
				Subject = msg.Subject,
				Sender = msg.Sender,
				Body = msg.Body,
				IsBodyHtml = msg.ContentType.Contains("html"),
				From = msg.From,
				Priority = (System.Net.Mail.MailPriority) msg.Importance
			};

			foreach (var a in msg.Bcc)
				ret.Bcc.Add(a);

			foreach (var a in msg.ReplyTo)
				ret.ReplyToList.Add(a);

			foreach (var a in msg.To)
				ret.To.Add(a);

			foreach (var a in msg.AllAttachments())
			{
				ret.Attachments.Add(new System.Net.Mail.Attachment(new System.IO.MemoryStream(a.GetData()), a.Filename, a.ContentType));
			}
			/*
foreach (var a in msg.AlternateViews)
	ret.AlternateViews.Add(new System.Net.Mail.AlternateView(new System.IO.MemoryStream(a.GetData()), a.ContentType));
*/
			return ret;
		}

		//readonly bool _headersOnly; // set to true if only headers have been fetched. 

		public MailMessage()
		{
			Subject = "";
			//Attachments = new Collection<Attachment>();
			//AlternateViews = new Collection<Attachment>();
		}

		public IEnumerable<Attachment> AllAttachments()
		{
			var rgAttachments = new List<Attachment>();
			IEnumerable<Attachment> rgNew = this.Attachments;
			while (rgNew.Any())
			{
				rgAttachments.AddRange(rgNew);
				rgNew = rgNew.SelectMany(a => a.Attachments).ToList();
			}
			return rgAttachments;
		}

		public virtual DateTime Date { get; private set; }
		public virtual DateTime InternalDate { get; set; }


		HashSet<string> _flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		public virtual ICollection<string> Flags => _flags;
		public virtual int Size { get; internal set; }
		public virtual string Subject { get; set; }
		public virtual ICollection<MailAddress> To { get; } = new Collection<MailAddress>();
		public virtual ICollection<MailAddress> Cc { get; } = new Collection<MailAddress>();
		public virtual ICollection<MailAddress> Bcc { get; } = new Collection<MailAddress>();
		public virtual ICollection<MailAddress> ReplyTo { get; } = new Collection<MailAddress>();

		/*
		public virtual ICollection<Attachment> Attachments { get; set; }
		public virtual ICollection<Attachment> AlternateViews { get; set; }
		*/

		public virtual MailAddress From { get; set; }
		public virtual MailAddress Sender { get; set; }
		public virtual string MessageID { get; set; }
		public virtual long Uid { get; internal set; }
		public virtual MailPriority Importance { get; set; }

		public virtual void Load(string message, bool headersOnly = false)
		{
			if (string.IsNullOrEmpty(message))
				return;

			using (var mem = new MemoryStream(_defaultEncoding.GetBytes(message)))
			{
				//Load(mem, headersOnly);
				Parse(mem);
			}
		}

		public override string Parse(Stream reader, char? termChar = null, Encoding encoding = null, Stack<string> rgBoundaries = null)
		{
			var last = base.Parse(reader, termChar, encoding, rgBoundaries);

			Date = Headers.GetDate();
			To.AddRange(Headers.GetMailAddresses("To"));
			Cc.AddRange(Headers.GetMailAddresses("Cc"));
			Bcc.AddRange(Headers.GetMailAddresses("Bcc"));
			Sender = Headers.GetMailAddresses("Sender").FirstOrDefault();
			ReplyTo.AddRange(Headers.GetMailAddresses("Reply-To"));
			From = Headers.GetMailAddresses("From").FirstOrDefault();
			MessageID = Headers["Message-ID"].RawValue;

			Importance = Headers.GetEnum<MailPriority>("Importance");
			Subject = Headers["Subject"].RawValue;

			return last;
		}

#if false
		public virtual void Load(Stream reader, bool headersOnly = false, char? termChar = null)
		{
			_HeadersOnly = headersOnly;
			Body = null;

			var headers = new StringBuilder();
			string line;
			while ((line = reader.ReadLine(_DefaultEncoding, termChar)) != null)
			{
				//Debug.WriteLine("Load Headers: " + line);
				if (line.Trim(new [] { ' ' }).Length == 0)
					if (headers.Length == 0)
						continue;
					else
						break;
				headers.AppendLine(line);
			}
			RawHeaders = headers.ToString();

			if (!headersOnly)
			{
				string boundary = Headers.GetBoundary();
				if (!string.IsNullOrEmpty(boundary))
				{
					var atts = new List<Attachment>();
					var body = ParseMime(reader, boundary, atts, Encoding, termChar);
					if (!string.IsNullOrEmpty(body))
						SetBody(body);

					foreach (var att in atts)
					{
						/*
						if (att.IsAttachment)
							Attachments.Add(att);
						else
						{
							if (Body == null && att.ContentType == "text/plain")
								SetBody(att.Body);
							else
								AlternateViews.Add(att);
						}
						*/
						(att.IsAttachment ? Attachments : AlternateViews).Add(att);
					}

					reader.ReadToEnd();
				}
				else
				{
					SetBody(reader.ReadToEnd(Encoding));
				}

				if (string.IsNullOrWhiteSpace(Body) || ContentType.StartsWith("multipart/"))
				{
					var att = AlternateViews.GetTextView() ?? AlternateViews.GetHtmlView() ?? Attachments.GetTextView() ?? Attachments.GetHtmlView();
					//Debug.Assert(att != null);
					if (att != null)
					{
						Body = att.Body;
						ContentTransferEncoding = att.Headers["Content-Transfer-Encoding"].RawValue;
						ContentType = att.Headers["Content-Type"].RawValue;
					}
				}
			}

			Date = Headers.GetDate();
			To = Headers.GetMailAddresses("To").ToList();
			Cc = Headers.GetMailAddresses("Cc").ToList();
			Bcc = Headers.GetMailAddresses("Bcc").ToList();
			Sender = Headers.GetMailAddresses("Sender").FirstOrDefault();
			ReplyTo = Headers.GetMailAddresses("Reply-To").ToList();
			From = Headers.GetMailAddresses("From").FirstOrDefault();
			MessageID = Headers["Message-ID"].RawValue;

			Importance = Headers.GetEnum<MailPriority>("Importance");
			Subject = Headers["Subject"].RawValue;
		}

		public static string ParseMime(Stream reader, string boundary, ICollection<Attachment> attachments, Encoding encoding, char? termChar)
		{
			string data = null,
				bounderInner = "--" + boundary,
				bounderOuter = bounderInner + "--";
			var body = new System.Text.StringBuilder();
			while (true)
			{
				data = reader.ReadLine(encoding, termChar);
				//Debug.WriteLine(bounderInner + " ---1--- " + data);
				if (data == null || data.StartsWith(bounderInner))
					break;
				body.Append(data);
			}

			while (data != null && !data.StartsWith(bounderOuter))
			{
				data = reader.ReadLine(encoding, termChar);
				//Debug.WriteLine(bounderOuter + " ---2--- " + data);

				if (data == null) break;
				if (data == "" && body.Length != 0)
					break;
				var a = new Attachment { Encoding = encoding };

				var part = new StringBuilder();
				// read part header
				while (!data.StartsWith(bounderInner) && data != "")
				{
					part.AppendLine(data);
					data = reader.ReadLine(encoding, termChar);
					//Debug.WriteLine(bounderInner + " ---3--- " + data);
					if (data == null) break;
				}
				a.RawHeaders = part.ToString();
				// header body

				// check for nested part
				var nestedboundary = a.Headers.GetBoundary();
				if (!string.IsNullOrEmpty(nestedboundary))
				{
					ParseMime(reader, nestedboundary, attachments, encoding, termChar);
					while (data != null && !data.StartsWith(bounderInner))
					{
						data = reader.ReadLine(encoding, termChar);
						//Debug.WriteLine(bounderInner + " ---5--- " + data);
					}
				}
				else
				{
					data = reader.ReadLine(a.Encoding, termChar);
					//Debug.WriteLine(bounderInner + " ---7--- " + data);
					if (data == null) break;
					var nestedBody = new StringBuilder();
					while (data != null && !data.StartsWith(bounderInner))
					{
						nestedBody.AppendLine(data);
						data = reader.ReadLine(a.Encoding, termChar);
						//Debug.WriteLine(bounderInner + " ---8--- " + data);
					}
					a.SetBody(nestedBody.ToString());
					attachments.Add(a);
				}
			}
			return body.ToString();
		}
#endif

		internal bool SetFlags(IEnumerable<string> flags) => AddFlags(flags) | RemoveFlags(Flags.Except(flags).ToList());
		internal bool AddFlags(IEnumerable<string> flags) => _flags.AddRange(flags);
		internal bool RemoveFlags(IEnumerable<string> flags) => _flags.RemoveRange(flags);

		public bool HasFlag(string flag) => Flags.Contains(flag);

		public virtual void Save(System.IO.Stream stream, Encoding encoding = null)
		{
			using (var str = new System.IO.StreamWriter(stream, encoding ?? Encoding.UTF8))
				Save(str);
		}

		static readonly string[] SpecialHeaders = "Date,To,Cc,Reply-To,Bcc,Sender,From,Message-ID,Importance,Subject".Split(',');
		public virtual void Save(System.IO.TextWriter txt)
		{
			txt.WriteLine("Date: {0}", Date.GetRFC2060Date());
			txt.WriteLine("To: {0}", string.Join("; ", To.Select(x => x.ToString())));
			txt.WriteLine("Cc: {0}", string.Join("; ", Cc.Select(x => x.ToString())));
			txt.WriteLine("Reply-To: {0}", string.Join("; ", ReplyTo.Select(x => x.ToString())));
			txt.WriteLine("Bcc: {0}", string.Join("; ", Bcc.Select(x => x.ToString())));
			if (Sender != null)
				txt.WriteLine("Sender: {0}", Sender);

			if (From != null)
				txt.WriteLine("From: {0}", From);

			if (!string.IsNullOrEmpty(MessageID))
				txt.WriteLine("Message-ID: {0}", MessageID);

			var otherHeaders = Headers.Where(x => !SpecialHeaders.Contains(x.Key, StringComparer.OrdinalIgnoreCase));
			foreach (var header in otherHeaders)
				txt.WriteLine("{0}: {1}", header.Key, header.Value);

			if (Importance != MailPriority.Normal)
				txt.WriteLine("Importance: {0}", (int) Importance);

			txt.WriteLine("Subject: {0}", Subject);
			txt.WriteLine();

			//todo: attachments
			txt.Write(Body);
		}
	}
}
