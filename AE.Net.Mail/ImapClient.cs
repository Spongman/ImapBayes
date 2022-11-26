using AE.Net.Mail.Imap;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace AE.Net.Mail
{
	public static class Flags
	{
		public const string None = "\\None";
		public const string Seen = "\\Seen";
		public const string Answered = "\\Answered";
		public const string Flagged = "\\Flagged";
		public const string Deleted = "\\Deleted";
		public const string Draft = "\\Draft";
	}

	public class ImapClient : TextClient, IMailClient
	{
		string _selectedMailbox;
		static int _tag;
		string[] _capability;

		bool _fIdling;
		string _idleTag;
		static readonly int _idleTimeout = (int)TimeSpan.FromMinutes(29).TotalMilliseconds;

		string _fetchHeaders;

		public Mailbox RootMailbox { get; private set; }

		public string MailboxDelimiter => RootMailbox.Delimiter;
		public ImapClient() { }
		public ImapClient(string host, string username, string password, AuthMethods method = AuthMethods.Login, int port = 143, bool secure = false, bool skipSslValidation = false)
		{
			Connect(host, port, secure, skipSslValidation);
			AuthMethod = method;
			Login(username, password);
		}

		public enum AuthMethods
		{
			Login = 0,
			CRAMMD5 = 1,
			SaslOAuth = 2
		}

		public virtual AuthMethods AuthMethod { get; set; }

		string GetTag()
		{
			Interlocked.Increment(ref _tag);
			return string.Format("xm{0:000} ", _tag);
		}

		public virtual bool Supports(string command) => (_capability ?? Capability()).Contains(command, StringComparer.OrdinalIgnoreCase);

		public delegate void ExistsHandler(ImapClient sender, int count);
		public virtual event ExistsHandler HandleExists;

		public delegate void ExpungeHandler(ImapClient sender, int expunged);
		public virtual event ExpungeHandler HandleExpunge;

		public delegate void FetchHandler(ImapClient sender, int id);
		public virtual event FetchHandler HandleFetch;

		readonly Regex _regExpunge = new Regex(@"\* (\d+) EXPUNGE", RegexOptions.Compiled);
		readonly Regex _regExists = new Regex(@"\* (\d+) EXISTS", RegexOptions.Compiled);
		readonly Regex _regFetch = new Regex(@"\* (\d+) FETCH", RegexOptions.Compiled);

		public void Idle()
		{
			_fIdling = true;

			IdleResumeCommand();

			string response;
			while (!(response = GetResponse()).StartsWith("+"))
			{
			}

			while (_fIdling)
			{
				while (!TryGetResponse(out response, _idleTimeout))
					SendTaggedCommand("NOOP");

				if (response.StartsWith("+"))
					break;

				//Debug.Assert(response.StartsWith("* "));

				Match m;
				if ((m = _regExpunge.Match(response)).Success)
				{
					if (this.HandleExpunge != null)
					{
						var id = int.Parse(m.Groups[1].Value);
						this.HandleExpunge.Invoke(this, id);
					}
				}
				else if ((m = _regExists.Match(response)).Success)
				{
					if (this.HandleExists != null)
					{
						var count = int.Parse(m.Groups[1].Value);
						this.HandleExists.Invoke(this, count);
					}
				}
				else if ((m = _regFetch.Match(response)).Success)
				{
					if (this.HandleFetch != null)
					{
						var id = int.Parse(m.Groups[1].Value);
						this.HandleFetch.Invoke(this, id);
					}
				}
			}

			SendCommand("DONE");
			GetTaggedResponse(_idleTag);
		}

		public void StopIdle()
		{
			Debug.Assert(_fIdling);
			_fIdling = false;
		}

		protected virtual void IdlePause()
		{
			if (_fIdling)
			{
				CheckConnectionStatus();
				SendTaggedCommandNoIdle("DONE");
				/*
				if (!_IdleEvents.Join(2000))
					_IdleEvents.Abort();
				_IdleEvents = null;
				*/
			}
		}

		protected virtual void IdleResume()
		{
			if (_fIdling)
			{
				IdleResumeCommand();

				/*
				if (_IdleEvents == null)
				{
					_IdleEvents = new Thread(WatchIdleQueue);
					_IdleEvents.Name = "_IdleEvents";
					_IdleEvents.Start();
				}
				*/
			}
		}

		void IdleResumeCommand()
		{
			_idleTag = GetTag();
			SendCommand(_idleTag + "IDLE");
			//_IdleARE.Set();
		}

		public string SendTaggedCommand(string command, Action<string> handleUntaggedResponse = null)
		{
			IdlePause();

			var result = SendTaggedCommandNoIdle(command, handleUntaggedResponse);

			IdleResume();
			return result;
		}

		protected string SendTaggedCommandNoIdle(string command, Action<string> handleUntaggedResponse = null)
		{
			var tag = GetTag();

			SendCommand(tag + command);

			return GetTaggedResponse(tag, handleUntaggedResponse);
		}

		string GetTaggedResponse(string tag, Action<string> handleUntaggedResponse = null)
		{
			string response;
			while (true)
			{
				response = GetResponse();
				if (response == null)
					return null;

				if (response.StartsWith(tag))
					break;

				handleUntaggedResponse?.Invoke(response);
			}

			return response.Substring(tag.Length);
		}

		readonly CancellationTokenSource tokenSource = new CancellationTokenSource();

		public virtual bool TryGetResponse(out string response, int millisecondsTimeout)
		{
			Thread thread = null;

			var task = Task.Run(() =>
			{
				thread = Thread.CurrentThread;
				return GetResponse();
			}, tokenSource.Token);

			if (task.Wait(millisecondsTimeout))
			{
				response = task.Result;
				return true;
			}
			tokenSource.Cancel();
#if !NETSTANDARD1_6
			thread.Abort();
#endif
			task.Wait();

			response = null;
			return false;
		}

#if FALSE

		private AutoResetEvent _IdleARE = new AutoResetEvent(false);
		private Thread _IdleEvents;

		protected virtual void IdleStart()
		{
			if (string.IsNullOrEmpty(_SelectedMailbox))
			{
				SelectMailbox("Inbox");
			}
			_Idling = true;
			if (!Supports("IDLE"))
			{
				throw new InvalidOperationException("This IMAP server does not support the IDLE command");
			}
			CheckMailboxSelected();
			IdleResume();
		}

		private bool HasEvents
		{
			get
			{
				return _MessageDeleted != null || _NewMessage != null;
			}
		}

		protected virtual void IdleStop()
		{
			_Idling = false;
			IdlePause();
			if (_IdleEvents != null)
			{
				_IdleARE.Close();
				if (!_IdleEvents.Join(2000))
					_IdleEvents.Abort();
				_IdleEvents = null;
			}
		}

		private void WatchIdleQueue()
		{
			try
			{
				string last = null, resp;

				while (true)
				{
					if (!TryGetResponse(out resp, _idleTimeout))
					{   //send NOOP every 20 minutes
						Noop(false);		//call noop without aborting this Idle thread
						continue;
					}

					if (resp.Contains("OK IDLE"))
						return;

					var data = resp.Split(' ');
					if (data[0] == "*" && data.Length >= 3)
					{
						var e = new MessageEventArgs { Client = this, MessageCount = int.Parse(data[1]) };
						if (data[2].Is("EXISTS") && !last.Is("EXPUNGE") && e.MessageCount > 0)
						{
							ThreadPool.QueueUserWorkItem(callback => _NewMessage.Fire(this, e));	//Fire the event on a separate thread
						}
						else if (data[2].Is("EXPUNGE"))
						{
							_MessageDeleted.Fire(this, e);
						}
						last = data[2];
					}
				}
			}
			catch (Exception) { }
		}

		protected override void OnDispose()
		{
			base.OnDispose();
			if (_IdleEvents != null)
			{
				_IdleEvents.Abort();
				_IdleEvents = null;
			}
		}
#endif
		public virtual void AppendMail(MailMessage email, string mailbox = null)
		{
			IdlePause();

			mailbox = ModifiedUtf7Encoding.Encode(mailbox);
			string flags = "";
			var body = new StringBuilder();
			using (var txt = new System.IO.StringWriter(body))
				email.Save(txt);

			string size = body.Length.ToString();
			if (email.Flags.Count > 0)
				flags = " (" + string.Join(" ", email.Flags) + ")";

			if (mailbox == null)
				CheckMailboxSelected();

			mailbox = mailbox ?? _selectedMailbox;

			string command = GetTag() + "APPEND " + (mailbox ?? _selectedMailbox).QuoteString() + flags + " {" + size + "}";
			string response = SendCommandGetResponse(command);
			if (response.StartsWith("+"))
				response = SendCommandGetResponse(body.ToString());
			IdleResume();
		}

		public virtual void Noop()
		{
			Noop(true);
		}

		void Noop(bool pauseIdle)
		{
			if (pauseIdle)
				IdlePause();
			else
				SendTaggedCommandNoIdle("DONE");

			SendTaggedCommandNoIdle("NOOP");

			if (pauseIdle)
				IdleResume();
			else
				IdleResumeCommand();
		}

		public virtual string[] Capability()
		{
			CheckTaggedCommand("CAPABILITY", response =>
			{
				if (response.StartsWith("* CAPABILITY "))
					_capability = response.Substring(13).Trim().Split(' ');
			});
			return _capability;
		}

		public virtual bool Copy(string messageset, string destination)
		{
			CheckMailboxSelected();
			string prefix = "";
			if (messageset.StartsWith("UID ", StringComparison.OrdinalIgnoreCase))
			{
				messageset = messageset.Substring(4);
				prefix = "UID ";
			}
			return CheckTaggedCommand(prefix + "COPY " + messageset + " " + destination.QuoteString());
		}

		public virtual bool CreateMailbox(string mailbox)
		{
			return CheckTaggedCommand("CREATE " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString());
		}

		public virtual bool DeleteMailbox(string mailbox)
		{
			return CheckTaggedCommand("DELETE " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString());
		}

		public virtual Mailbox Examine(string mailbox)
		{
			var x = new Mailbox(mailbox);
			CheckTaggedCommand("EXAMINE " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString(), response =>
			{
				Match m = Regex.Match(response, @"(\d+) EXISTS");
				if (m.Success)
					x.NumMsg = Convert.ToInt32(m.Groups[1].Value);

				m = Regex.Match(response, @"(\d+) RECENT");
				if (m.Success)
					x.NumNewMsg = Convert.ToInt32(m.Groups[1].Value);

				m = Regex.Match(response, @"UNSEEN (\d+)");
				if (m.Success)
					x.NumUnSeen = Convert.ToInt32(m.Groups[1].Value);

				m = Regex.Match(response, @" FLAGS \((.*?)\)");
				if (m.Success)
					x.SetFlags(m.Groups[1].Value);
			});

			_selectedMailbox = mailbox;

			return x;
		}

		public virtual bool Expunge()
		{
			CheckMailboxSelected();

			return CheckTaggedCommand("EXPUNGE");
		}

		public virtual bool DeleteMessage(AE.Net.Mail.MailMessage msg)
		{
			return AddFlags(new[] { Flags.Seen, Flags.Deleted }, msg);
		}

		readonly Regex _reCopyUid = new Regex("COPYUID ([0-9]+) ([0-9,:]+) ([0-9,:]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public virtual void MoveMessage(AE.Net.Mail.MailMessage msg, string folderName)
		{
			CheckMailboxSelected();

			try
			{
				if (this.Supports("MOVE"))
				{
					CheckTaggedCommand(string.Format("UID MOVE " + msg.Uid + " " + folderName.QuoteString()));
				}
				else
				{
					var r = SendTaggedCommand($"UID COPY {msg.Uid} {folderName.QuoteString()}");
					if (IsOK(r))
					{
						var match = _reCopyUid.Match(r);
						if (match != null && match.Success)
						{
							if (long.TryParse(match.Groups[3].Value, out long uidNew))
							{
								var msgNew = new MailMessage()
								{
									Uid = uidNew
								};
								var mailbox = _selectedMailbox;
								this.SelectMailbox(folderName);
								this.AddFlags(msg.Flags, msgNew);
								this.SelectMailbox(mailbox);
							}
						}

						DeleteMessage(msg);
					}
				}
			}
			catch { }
		}

		protected virtual void CheckMailboxSelected()
		{
			if (string.IsNullOrEmpty(_selectedMailbox))
				SelectMailbox("INBOX");
		}

		public virtual MailMessage GetMessage(long uid, bool headersonly = false) => GetMessage(uid, headersonly, true);

		public virtual MailMessage GetMessage(int index, bool headersonly = false) => GetMessage(index, headersonly, true);

		public virtual MailMessage GetMessage(int index, bool headersonly, bool setseen) => GetMessages(index, index, headersonly, setseen).FirstOrDefault();

		public virtual MailMessage GetMessage(long uid, bool headersonly, bool setseen) => GetMessages(uid, uid, headersonly, setseen).FirstOrDefault();

		public virtual IEnumerable<MailMessage> GetMessages(long startUID, long endUID, bool headersonly = true, bool setseen = false) => GetMessages(startUID, endUID, true, headersonly, setseen);

		public virtual void DownloadMessage(System.IO.Stream stream, int index, bool setseen)
		{
			GetMessages(index + 1, index + 1, false, false, setseen, (message, headers) => message.CopyTo(stream));
		}

		public virtual void DownloadMessage(System.IO.Stream stream, long uid, bool setseen)
		{
			GetMessages(uid, uid, true, false, setseen, (message, headers) => message.CopyTo(stream));
		}

		public virtual void GetMessages(long start, long end, bool uid, bool headersonly, bool setseen, Action<MailMessage> action)
		{
			GetMessages(start, end, uid, headersonly, setseen, (stream, imapHeaders) =>
			{
				if (!long.TryParse(imapHeaders["UID"], out long uidMsg))
					return;

				var msg = new MailMessage { Encoding = Encoding, Uid = uidMsg };

				if (!string.IsNullOrWhiteSpace(imapHeaders["INTERNALDATE"]))
				{
					var date = imapHeaders["INTERNALDATE"].ToNullDate();
					if (date != null)
						msg.InternalDate = date.Value;
				}

				if (!string.IsNullOrWhiteSpace(imapHeaders["Flags"]))
					msg.SetFlags(imapHeaders["Flags"].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()));

				if (stream != null)
				{
					msg.Size = (int)stream.Length;
					//mail.Load(stream, headersonly);
					msg.Parse(stream);
				}

				foreach (var key in imapHeaders.AllKeys.Except(new[] { "UID", "Flags", "BODY[]", "BODY[HEADER]" }, StringComparer.OrdinalIgnoreCase))
				{
					msg.Headers.Add(key, new HeaderValue(imapHeaders[key]));
				}

				action(msg);
			});
		}

		public virtual IEnumerable<MailMessage> GetMessages(long start, long end, bool uid, bool headersonly, bool setseen)
		{
			var rgMessages = new List<MailMessage>();
			GetMessages(start, end, uid, headersonly, setseen, msg => rgMessages.Add(msg));
			return rgMessages;
		}

		static readonly Regex _reFetchResponse = new Regex("\\* (\\d+) FETCH \\((.*)");

		public virtual void GetMessages(long start, long end, bool uid, bool headersonly, bool setseen, Action<LimitedInputStream, NameValueCollection> action)
		{
			CheckMailboxSelected();

			string command = (uid ? "UID " : null)
				+ "FETCH " + start + ":" + end + " ("
				+ _fetchHeaders + "UID INTERNALDATE FLAGS BODY"
				+ (setseen ? "" : ".PEEK")
				+ "[" + (headersonly ? "HEADER" : null) + "])";

			CheckTaggedCommand(command, response =>
			{
				Match m = _reFetchResponse.Match(response);
				if (m.Success)
				{
					var imapHeaders = Utilities.ParseImapHeader(m.Groups[2].Value);

					string strHeader = null;
					if (!string.IsNullOrEmpty(imapHeaders["BODY[HEADER]"]))
						strHeader = imapHeaders["BODY[HEADER]"];
					else if (!string.IsNullOrEmpty(imapHeaders["BODY[]"]))
						strHeader = imapHeaders["BODY[]"];

					int? size = null;
					if (strHeader != null)
					{
						strHeader = strHeader.Trim('{', '}', ' ');
						if (strHeader != "NIL")
							size = strHeader.ToInt();
					}

					if (size != null)
					{
						using (var body = new LimitedInputStream(_streamInput, size.Value))
							action(body, imapHeaders);

						response = GetResponse();
						var n = response.Trim().LastOrDefault();
						if (n != ')')
						{
							System.Diagnostics.Debugger.Break();
							RaiseWarning(null, "Expected \")\" in stream, but received \"" + response + "\"");
						}
					}
					else
					{
						action(null, imapHeaders);
					}
				}
			});
		}

		/*

		public async Task<IEnumerable<MailMessage>> GetMessagesAsync(long start, long end, bool uid, bool headersonly, bool setseen, Func<System.IO.Stream, int?, NameValueCollection, Task<MailMessage>> action)
		{
			CheckMailboxSelected();

			string command = (uid ? "UID " : null)
				+ "FETCH " + start + ":" + end + " ("
				+ _FetchHeaders + "UID INTERNALDATE FLAGS BODY"
				+ (setseen ? "" : ".PEEK")
				+ "[" + (headersonly ? "HEADER" : null) + "])";

			const string reg = "\\* (\\d+) FETCH \\((.*)";

			var rgMessages = new List<MailMessage>();

			CheckOK(await SendTaggedCommandAsync(command, async response =>
			{
				Match m = Regex.Match(response, reg);
				if (m.Success)
				{
					var imapHeaders = Utilities.ParseImapHeader(m.Groups[2].Value);

					int? size = 0;

					string strHeader = null;
					if (!string.IsNullOrEmpty(imapHeaders["BODY[HEADER]"]))
						strHeader = imapHeaders["BODY[HEADER]"];
					else if (!string.IsNullOrEmpty(imapHeaders["BODY[]"]))
						strHeader = imapHeaders["BODY[]"];

					if (strHeader != null)
					{
						strHeader = strHeader.Trim('{', '}', ' ');
						if (strHeader == "NIL")
							size = null;
						else
							size = strHeader.ToInt();
					}

					var msg = await action(_streamInput, size, imapHeaders);

					if (size > 0)
					{
						response = await GetResponseAsync();
						var n = response.Trim().LastOrDefault();
						if (n != ')')
						{
							System.Diagnostics.Debugger.Break();
							RaiseWarning(null, "Expected \")\" in stream, but received \"" + response + "\"");
						}
					}

					if (msg.Uid != 0)
						rgMessages.Add(msg);
				}
			}));

			return rgMessages;
		}
		*/

		static readonly Regex _reGetQuotaResponse = new Regex("\\* QUOTA (.*?) \\((.*?) (.*?) (.*?)\\)");

		public virtual Quota GetQuota(string mailbox)
		{
			if (!Supports("NAMESPACE"))
				throw new Exception("This command is not supported by the server!");

			Quota quota = null;

			CheckTaggedCommand("GETQUOTAROOT " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString(), response =>
			{
				Match m = _reGetQuotaResponse.Match(response);
				if (m.Success)
				{
					quota = new Quota(
						m.Groups[1].Value,
						m.Groups[2].Value,
						Int32.Parse(m.Groups[3].Value),
						Int32.Parse(m.Groups[4].Value)
					);
				}
			});

			return quota;
		}

		static readonly Regex _reListResponse = new Regex("\\* LIST \\(([^\\)]*)\\) \\\"([^\\\"]+)\\\" \\\"?([^\\\"]+)\\\"?");

		public virtual IEnumerable<Mailbox> ListMailboxes(string reference = "", string pattern = "*")
		{
			var rgMailboxes = new List<Mailbox>();

			SendTaggedCommand("LIST " + reference.QuoteString() + " " + pattern.QuoteString(), response =>
			{
				Match m = _reListResponse.Match(response);
				if (m.Success)
				{
					var mailbox = new Mailbox(m.Groups[3].Value, m.Groups[1].Value, m.Groups[2].Value.Trim());
					rgMailboxes.Add(mailbox);
				}
			});

			return rgMailboxes;
		}

		static readonly Regex _reLsubResponse = new Regex("\\* LSUB \\(([^\\)]*)\\) \\\"([^\\\"]+)\\\" \\\"([^\\\"]+)\\\"");

		public virtual IEnumerable<Mailbox> ListSuscribesMailboxes(string reference, string pattern)
		{
			var rgMailboxes = new List<Mailbox>();

			SendTaggedCommand("LSUB " + reference.QuoteString() + " " + pattern.QuoteString(), response =>
			{
				Match m = _reLsubResponse.Match(response);
				if (m.Success)
				{
					var mailbox = new Mailbox(m.Groups[3].Value);
					rgMailboxes.Add(mailbox);
				}
			});

			return rgMailboxes;
		}

		internal override void OnLogin(string login, string password)
		{
			string command = "";
			string result = "";
			string tag = GetTag();
			string key;

			switch (AuthMethod)
			{
				case AuthMethods.CRAMMD5:
					command = tag + "AUTHENTICATE CRAM-MD5";
					result = SendCommandGetResponse(command);
					// retrieve server key
					key = result.Replace("+ ", "");
					key = Encoding.UTF8.GetString(Convert.FromBase64String(key));
					// calcul hash
					using (var kMd5 = new HMACMD5(Encoding.ASCII.GetBytes(password)))
					{
						byte[] hash1 = kMd5.ComputeHash(Encoding.ASCII.GetBytes(key));
						key = BitConverter.ToString(hash1).ToLower().Replace("-", "");
						result = Convert.ToBase64String(Encoding.ASCII.GetBytes(login + " " + key));
						result = SendCommandGetResponse(result);
					}
					break;

				case AuthMethods.Login:
					command = tag + "LOGIN " + login.QuoteString() + " " + password.QuoteString();
					result = SendCommandGetResponse(command);
					break;

				case AuthMethods.SaslOAuth:
					command = tag + "AUTHENTICATE XOAUTH " + password;
					result = SendCommandGetResponse(command);
					break;

				default:
					throw new NotSupportedException();
			}

			if (result.StartsWith("* CAPABILITY "))
			{
				_capability = result.Substring(13).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				result = GetResponse();
			}

			if (!result.StartsWith(tag + "OK"))
				throw new Exception(result);

			//if (Supports("COMPRESS=DEFLATE")) {
			//  SendCommandCheckOK(GetTag() + "compress deflate");
			//  _Stream0 = _Stream;
			// // _Reader = new System.IO.StreamReader(new System.IO.Compression.DeflateStream(_Stream0, System.IO.Compression.CompressionMode.Decompress, true), System.Text.Encoding.Default);
			// // _Stream = new System.IO.Compression.DeflateStream(_Stream0, System.IO.Compression.CompressionMode.Compress, true);
			//}

			if (Supports("X-GM-EXT-1"))
				_fetchHeaders = "X-GM-MSGID X-GM-THRID X-GM-LABELS ";

			var mbRoot = this.ListMailboxes("", "%").FirstOrDefault();
			Debug.Assert(mbRoot != null);
			if (mbRoot != null)
				this.RootMailbox = mbRoot;
		}

		internal override void OnLogout()
		{
			if (IsConnected)
			{
				IdlePause();
				SendTaggedCommandNoIdle("LOGOUT");
			}
		}

		static readonly Regex _reNamespaceResponse1 = new Regex(@"\((.*?)\) \((.*?)\) \((.*?)\)$");
		static readonly Regex _reNamespaceResponse2 = new Regex("\\(\\\"(.*?)\\\" \\\"(.*?)\\\"\\)");

		public virtual Namespaces Namespace()
		{
			if (!Supports("NAMESPACE"))
				throw new NotSupportedException("This command is not supported by the server!");

			var n = new Namespaces();

			CheckTaggedCommand("NAMESPACE", response =>
			{
				if (response.StartsWith("* NAMESPACE"))
				{
					response = response.Substring(12);
					//[TODO] be sure to parse correctly namespace when not all namespaces are present. NIL character
					Match m = _reNamespaceResponse1.Match(response);
					if (m.Groups.Count != 4)
						throw new Exception("An error occured, this command is not fully supported !");

					foreach (var m2 in _reNamespaceResponse2.Matches(m.Groups[1].Value).Cast<Match>())
						n.ServerNamespace.Add(new Namespace(m2.Groups[1].Value, m2.Groups[2].Value));

					foreach (var m2 in _reNamespaceResponse2.Matches(m.Groups[2].Value).Cast<Match>())
						n.UserNamespace.Add(new Namespace(m2.Groups[1].Value, m2.Groups[2].Value));

					foreach (var m2 in _reNamespaceResponse2.Matches(m.Groups[3].Value).Cast<Match>())
						n.SharedNamespace.Add(new Namespace(m2.Groups[1].Value, m2.Groups[2].Value));
				}
			});

			return n;
		}

		public virtual int GetMessageCount()
		{
			CheckMailboxSelected();
			return GetMessageCount(null);
		}

		static readonly Regex _reStatusResponse1 = new Regex(@"\* STATUS.*MESSAGES (\d+)");

		public virtual int GetMessageCount(string mailbox)
		{
			int result = 0;
			CheckTaggedCommand("STATUS " + ModifiedUtf7Encoding.Encode(mailbox) ?? _selectedMailbox.QuoteString() + " (MESSAGES)", response =>
			{
				Match m = _reStatusResponse1.Match(response);
				if (m.Success)
					result = Convert.ToInt32(m.Groups[1].Value);
			});

			return result;
		}

		public virtual void RenameMailbox(string frommailbox, string tomailbox)
		{
			CheckTaggedCommand("RENAME " + frommailbox.QuoteString() + " " + tomailbox.QuoteString());
		}

		public virtual IEnumerable<long> Search(SearchCondition criteria, bool uid = true)
		{
			return Search(criteria.ToString(), uid);
		}

		static readonly Regex _reSearchResponse = new Regex(@"^\* SEARCH (.*)");

		public virtual IEnumerable<long> Search(string criteria, bool uid = true)
		{
			CheckMailboxSelected();

			string isuid = uid ? "UID " : "";

			var rgIds = new List<long>();

			CheckTaggedCommand(isuid + "SEARCH " + criteria, response =>
			{
				var m = _reSearchResponse.Match(response);
				if (m.Success)
				{
					rgIds.AddRange(m.Groups[1].Value.Trim().Split(' ').Where(x => !string.IsNullOrEmpty(x)).Select(s => long.Parse(s)));
				}
			});

			return rgIds;
		}

		public virtual IEnumerable<Lazy<MailMessage>> SearchMessages(SearchCondition criteria, bool headersonly = false, bool setseen = false)
		{
			return Search(criteria, true)
					.Select(x => new Lazy<MailMessage>(() => GetMessage(x, headersonly, setseen)));
		}

		public virtual Mailbox SelectMailbox(string mailbox)
		{
			var x = new Mailbox(mailbox);

			var r = SendTaggedCommand("SELECT " + mailbox.QuoteString(), response =>
			{
				Match m = Regex.Match(response, @"(\d+) EXISTS");
				if (m.Success)
					x.NumMsg = Convert.ToInt32(m.Groups[1].Value);

				m = Regex.Match(response, @"(\d+) RECENT");
				if (m.Success)
					x.NumNewMsg = Convert.ToInt32(m.Groups[1].Value);

				m = Regex.Match(response, @"UNSEEN (\d+)");
				if (m.Success)
					x.NumUnSeen = Convert.ToInt32(m.Groups[1].Value);

				m = Regex.Match(response, @" FLAGS \((.*?)\)");
				if (m.Success)
					x.SetFlags(m.Groups[1].Value);

				m = Regex.Match(response, @"UIDVALIDITY (\d+)");
				if (m.Success)
					x.Uid = Convert.ToInt64(m.Groups[1].Value);
			});

			if (!IsOK(r))
				return null;

			_selectedMailbox = mailbox;
			x.IsWritable = Regex.IsMatch(r, "READ.WRITE", RegexOptions.IgnoreCase);

			return x;
		}

		enum FlagsOperation
		{
			Add,
			Remove,
			Store,
		}

		public virtual bool SetFlags(IEnumerable<string> flags, params MailMessage[] msgs)
		{
			var rgChanged = msgs.Where(m => m.SetFlags(flags)).ToList();
			return Store(rgChanged, FlagsOperation.Store, flags);
		}

		public virtual bool AddFlags(IEnumerable<string> flags, params MailMessage[] msgs)
		{
			var rgChanged = msgs.Where(m => m.AddFlags(flags)).ToList();
			return Store(rgChanged, FlagsOperation.Add, flags);
		}

		public virtual bool RemoveFlags(IEnumerable<string> flags, params MailMessage[] msgs)
		{
			var rgChanged = msgs.Where(m => m.RemoveFlags(flags)).ToList();
			return Store(rgChanged, FlagsOperation.Remove, flags);

			/*
			var changedMessages = msgs.Where (m => flags.Any(f => m.Flags.Contains (f))).ToList();

			foreach (var group in changedMessages.GroupBy (m => m.Flags.Where (f => !flags.Contains(f))))
			{
				Store (group, true, group.Key);
			}

			foreach (var msg in msgs)
			{
				msg.RemoveFlags (flags);
			}
			*/
		}

		bool Store(IEnumerable<MailMessage> messages, FlagsOperation op, IEnumerable<string> flags)
		{
			var strMessageIds = string.Join(",", messages.Select(m => m.Uid.ToString()));
			if (strMessageIds == "")
				return true;

			CheckMailboxSelected();

			string prefix = "";
			switch (op)
			{
				case FlagsOperation.Add:
					prefix = "+";
					break;
				case FlagsOperation.Remove:
					prefix = "-";
					break;
			}

			return CheckTaggedCommand(string.Format("UID STORE {0} {1}FLAGS.SILENT ({2})", strMessageIds, prefix, string.Join(" ", flags)));
		}

		public virtual bool SuscribeMailbox(string mailbox)
		{
			return CheckTaggedCommand("SUBSCRIBE " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString());
		}

		public virtual bool UnSuscribeMailbox(string mailbox)
		{
			return CheckTaggedCommand("UNSUBSCRIBE " + ModifiedUtf7Encoding.Encode(mailbox).QuoteString());
		}

		internal override void CheckResultOK(string response)
		{
			if (!IsResultOK(response))
			{
				response = response.Substring(response.IndexOf(" ")).Trim();
				throw new Exception(response);
			}
		}

		internal bool IsResultOK(string response)
		{
			response = response.Substring(response.IndexOf(" ")).Trim();
			return response.ToUpper().StartsWith("OK");
		}

		protected bool IsOK(string response)
		{
			if (response == null)
				return false;

			return response.ToUpper().StartsWith("OK");
		}

		protected bool CheckTaggedCommand(string command, Action<string> handleUntaggedResponse = null)
		{
			return IsOK(SendTaggedCommand(command, handleUntaggedResponse));
		}
	}
}
