#define SQLITE

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
//using System.Data.Common;
//using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AE.Net.Mail;
using AE.Net.Mail.Imap;
using HtmlAgilityPack;
using Fizzler.Systems.HtmlAgilityPack;
//using ImapBayes.Properties;
//using System.Configuration;
using System.Data.Common;

#if SQLITE
using RowIdType = System.Int64;
#else
using RowIdType = int;
#endif

namespace ImapBayes
{
	class Program
	{
		static string _strConnectionString;

		public static DbProviderFactory DbFactory;// = SqlClientFactory.Instance;

		static void Main(string[] args)
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

			Trace.Listeners.Add(new ConsoleTraceListener());

#if false
			using (var fs = File.OpenRead(@"c:\temp\mime.txt"))
			{
				var msg = new MailMessage();
				msg.Parse(fs);
				//msg.Load(fs);
				var i = 3;
			}
#endif

			/*
			_strConnectionString = "Data Source=ImapBayes.s3db";
			var providerName = "System.Data.SQLite";
			DbFactory = Microsoft.Data.Sqlite.SqliteFactory.Instance;
			*/

#if SQLITE
			//var connectionString = ConfigurationManager.ConnectionStrings["default"];
			//_strConnectionString = connectionString.ConnectionString;
			//DbFactory = DbProviderFactories.GetFactory(connectionString.ProviderName);
			_strConnectionString = "Data Source=ImapBayes.s3db";
			DbFactory = Microsoft.Data.Sqlite.SqliteFactory.Instance;
#else
			_strConnectionString = @"Integrated Security=SSPI;Data Source=.\SQLEXPRESS";
			DbFactory = System.Data.SqlClient.SqlClientFactory.Instance;
#endif
			/*
			*/

			//_strConnectionString = @"Integrated Security=SSPI;Data Source=(LocalDb)\v11.0;AttachDbFilename=" + mdfPath;
			//_strConnectionString = @"Initial Catalog=master;Data Source=(LocalDb)\v11.0;AttachDbFilename=|DataDirectory|ExecuteNonReader\ImapBayes.mdf";

#if false
			var assemblyLocation = Assembly.GetEntryAssembly().Location;
			AppDomain.CurrentDomain.SetData("DataDirectory", Path.GetDirectoryName(assemblyLocation));

			var mdfPath = Path.Combine(Path.GetDirectoryName(assemblyLocation), @"..\..\App_Data\ImapBayes.mdf");
			mdfPath = Path.GetFullPath(mdfPath);
#endif

			var mapAccountInfos = new Dictionary<int, Account>();
			{
				//var rgAccountIds = new List<int>();
				//using (var con = new IDbDataConnection (@"Data Source=(LocalDb)\v11.0;Integrated Security=SSPI;AttachDbFilename=|DataDirectory|\ImapBayes.mdf"))
				using (var con = GetConnection(false))
				{
					try
					{
						UseDatabase(con);
					}
					catch (DbException)
					{
						Trace.WriteLine("initializing...");

						//var script = ImapBayes.Properties.Resources.DatabaseScript;
						var script = DatabaseScript.Script;
						switch (DbFactory.GetType().Name)
						{
							case "SqliteFactory":
								//((System.Data.SQLite.SQLiteConnection) con).
								//System.Data.SQLite.SQLiteConnection.CreateFile("ImapBayes.s3db")
								//con.ExecuteNonQuery("CREATE DATABASE ImapBayes;", CommandType.Text);

								script = script.Replace("INT IDENTITY PRIMARY KEY", "INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL");
								script = script.Replace("PRIMARY KEY CLUSTERED", "PRIMARY KEY");
								script = script.Replace("BIT", "BOOLEAN");
								break;

							default:

								script = script.Replace("WITHOUT ROWID", "");

								con.ExecuteNonQuery("CREATE DATABASE ImapBayes;", CommandType.Text);
								UseDatabase(con);
								break;
						}

						ExecuteScript(con, script);
					}

					//Trace.WriteLine("trimming...");
					//ExecuteScript(con, Resources.TrimScript);

					using (var reader = con.ExecuteReader(@"
							SELECT id
							FROM tblAccounts
							WHERE fActive <> 0",
							CommandType.Text))
					{
						while (reader.Read())
						{
							var idAccount = reader.GetInt32("id");

							var ai = new Account(idAccount);

							mapAccountInfos[idAccount] = ai;
						}
					}
				}
			}

			/*
			if (!Debugger.IsAttached)
			{
				Trace.WriteLine("starting...");
				foreach (var ai in mapAccountInfos.Values)
				{
					ai.Run();
				}
			}
			*/

			Trace.WriteLine("type 'quit' to exit");

			while (true)
			{
				string strLine = Console.ReadLine();
				if (strLine.Equals("quit", StringComparison.OrdinalIgnoreCase))
					break;

				var rgParts = strLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (rgParts.Length == 0)
					continue;

				using (var con = GetConnection())
				{
					var strCommand = rgParts[0].ToLower();

					IEnumerable<Account> ParseAccountInfos()
					{
						IEnumerable<int> rgAccountIds;
						if (rgParts.Length == 1)
							rgAccountIds = mapAccountInfos.Keys;
						else
							rgAccountIds = rgParts.Skip(1).Select(str => int.TryParse(str, out int value) ? value : default(int?)).Where(v => v != null).Select(v => v.Value);
						foreach (var id in rgAccountIds)
						{
							if (mapAccountInfos.TryGetValue(id, out Account ai))
							{
								yield return ai;
							}
						}
					}

					switch (strCommand)
					{
						default:
							Trace.WriteLine($"{strCommand}: command not found");
							continue;

						case "accounts":
							foreach (var ai in mapAccountInfos.OrderBy(p => p.Key).Select(p => p.Value))
								Trace.WriteLine($"{ai.AccountId}\t: {ai.Status}\t{ai.User}");
							break;

						case "add":
							{
								Console.WriteLine("Name:");
								var strName = Console.ReadLine().Trim();

								Console.WriteLine("Host:");
								var strHost = Console.ReadLine().Trim();

								Console.WriteLine("SSL?");
								var strSSL = Console.ReadLine().Trim();
								var fUseSSL = new[] { "y", "1", "yes", "true" }.Contains(strSSL.ToLower());

								var defaultPort = fUseSSL ? 143 : 993;
								Console.WriteLine($"Port ({defaultPort}):");
								var strPort = Console.ReadLine().Trim();
								int nPort;
								if (!int.TryParse(strPort, out nPort))
									nPort = defaultPort;

								Console.WriteLine("User:");
								var strUser = Console.ReadLine().Trim();

								Console.WriteLine("Password:");
								var strPass = Console.ReadLine().Trim();

								Console.WriteLine("Inbox (INBOX):");
								var strInbox = Console.ReadLine().Trim();
								if (strInbox == "")
									strInbox = "INBOX";

								Console.WriteLine($"Spam ({strInbox}.Spam):");
								var strSpam = Console.ReadLine().Trim();
								if (strSpam == "")
									strSpam = $"{strInbox}.Spam";

								Console.WriteLine($"Unsure ({strInbox}.Unsure):");
								var strUnsure = Console.ReadLine().Trim();
								if (strUnsure == "")
									strUnsure = $"{strInbox}.Unsure";

								Console.WriteLine($"Spam Cutoff (0.93):");
								var strSpamCutoff = Console.ReadLine().Trim();
								float nSpamCutoff;
								if (!float.TryParse(strSpam, out nSpamCutoff))
									nSpamCutoff = 0.93f;

								Console.WriteLine($"Ham Cutoff (0.2):");
								var strHamCutoff = Console.ReadLine().Trim();
								float nHamCutoff;
								if (!float.TryParse(strHamCutoff, out nHamCutoff))
									nHamCutoff = 0.2f;

								var ai = Account.Create(
									strName, strHost, strUser, strPass,
									nPort, fUseSSL,
									strInbox, strSpam, strUnsure,
									nSpamCutoff, nHamCutoff
								);
								mapAccountInfos[ai.AccountId] = ai;
								Trace.WriteLine($"CREATED {ai.AccountId}\t{ai.User}");
							}
							break;

						case "stop":
							foreach (var ai in ParseAccountInfos())
							{
								Trace.WriteLine($"STOPPING {ai.AccountId}\t{ai.User}");
								ai.Stop(true);
							}
							break;

						case "start":
							foreach (var ai in ParseAccountInfos())
							{
								Trace.WriteLine($"STARTING {ai.AccountId}\t{ai.User}");
								ai.Run();
							}
							break;

						case "train":
							{

								if (rgParts.Length < 1 || !int.TryParse(rgParts[1], out int id))
								{
									Trace.WriteLine($"syntax: train <id> [<folder> ...]");
									break;
								}

								if (!mapAccountInfos.TryGetValue(id, out Account ai))
								{
									Trace.WriteLine($"error: account {ai} not found");
									break;
								}

								var folders = rgParts.Skip(2).ToArray();
								Trace.WriteLine($"TRAINING {ai.AccountId}\t{ai.User}");
								ai.Train(folders);
							}
							break;

						case "clean":
							foreach (var ai in ParseAccountInfos())
							{
								Trace.WriteLine($"CLEANING {ai.AccountId}\t{ai.User}");
								ai.Clean();
							}
							break;
					}

					Debug.WriteLine("DONE " + strCommand);
				}
			}

			Trace.WriteLine("cancelling...");
			foreach (var ai in mapAccountInfos.Values)
				ai.Stop();

			Trace.WriteLine("waiting...");
			Task.WaitAll(mapAccountInfos.Values.Select(ai => ai.Task).ToArray());

			Trace.WriteLine("done.");

			Console.ReadLine();
		}

		static void ExecuteScript(IDbConnection con, string script)
		{
			script = Regex.Replace(script, @"/\*(\n|\r|(\*[^/])|[^\*])*\*/", "", RegexOptions.Singleline);

			foreach (var line in script.Split(new[] { "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries))
			{
				Debug.WriteLine(line);
				using (IDbCommand cmd = con.GetCommand(line, CommandType.Text))
				{
					cmd.CommandTimeout = 300;
					cmd.ExecuteNonQuery();
				}
			}
		}

		public static IDbConnection GetConnection(bool fUse = true)
		{
			Exception e = null;
			for (var i = 10; i >= 0; --i)
			{
				var con = DbFactory.CreateConnection();
				try
				{
					con.ConnectionString = _strConnectionString;
					con.Open();
					if (fUse)
						UseDatabase(con);

					return con;
				}
				catch (Exception ex)
				{
					e = ex;
					con.Dispose();

					Thread.Sleep(2000);
				}
			}
			throw e;
		}

		static void UseDatabase(IDbConnection con)
		{
			switch (DbFactory.GetType().Name)
			{
				case "SqliteFactory":
					using (var tb = con.ExecuteReader("SELECT * FROM tblTokens LIMIT 1", CommandType.Text))
					{
					}
					break;

				default:
					con.ExecuteNonQuery("USE ImapBayes", CommandType.Text);
					break;
			}
		}
	}

	class MailboxInfo
	{
		public Mailbox Mailbox { get; set; }
		public MailboxInfo Parent { get; set; }
		public bool IsRecursive { get; set; }

		bool _fSpamSpecified;
		bool? _fSpam;
		public bool? IsSpam
		{
			get
			{
				if (_fSpamSpecified)
					return _fSpam;

				if (Parent == null)
					return null;

				return Parent.GetInheritedSpamFlag();
			}
			set
			{
				_fSpam = value;
				_fSpamSpecified = true;
			}
		}

		bool? GetInheritedSpamFlag()
		{
			if (IsRecursive)
				return this.IsSpam;

			if (Parent == null)
				return null;

			return Parent.GetInheritedSpamFlag();
		}
	}

	class MessageInfo
	{
		public int Id;
		public int AccountId;
		public string MessageId;
		public bool? IsSpam;
		public bool IsTrained;
		public string Subject;
		public float? Score;

		public static string GetSpamText(bool? fSpam)
		{
			switch (fSpam)
			{
				case true: return "SPAM";
				case false: return "HAM";
			}
			return "UNSURE";
		}

		public string SpamText => GetSpamText(this.IsSpam);

		public static MessageInfo FromMessage(IDbConnection con, int idAccount, MailMessage msg)
		{
			var strId = GetUniqueId(msg);
			return FromMessageId(con, idAccount, strId);
		}

		public static MessageInfo FromMessageId(IDbConnection con, int idAccount, string strId)
		{
			using (var reader = con.ExecuteReader(@"
					SELECT id, idAccount, strId, fSpam, strSubject, fTrained, nScore
					FROM tblMessages
					WHERE idAccount = @idAccount AND strId = @strId",
				CommandType.Text,
				Program.DbFactory.CreateParameter("@idAccount", idAccount),
				Program.DbFactory.CreateParameter("@strId", strId)))
			{
				if (!reader.Read())
				{
					return null;
				}

				return new MessageInfo
				{
					Id = reader.GetInt32("id"),
					AccountId = reader.GetInt32("idAccount"),
					MessageId = reader.GetString("strId"),
					IsSpam = reader.GetNullable<bool>("fSpam"),
					Subject = reader.GetString("strSubject"),
					IsTrained = reader.GetBoolean("fTrained"),
					Score = reader.GetNullable<float>("nScore")
				};
			}
		}

		const int _maxIdLength = 200;

		public static string GetUniqueId(MailMessage msg)
		{
			var strId = msg.MessageID;
			if (string.IsNullOrWhiteSpace(strId))
				strId = msg.InternalDate.Ticks.ToString() + "_" + msg.From;

			if (strId.Length > _maxIdLength)
				strId = strId.Substring(0, _maxIdLength);

			return strId;
		}
	}



	class Account
	{
		static readonly ConcurrentDictionary<string, Lazy<TokenRecord>> _mapTokens = new ConcurrentDictionary<string, Lazy<TokenRecord>>(StringComparer.OrdinalIgnoreCase);
		const double _unknownWordStrength = 0.45;
		const double _unknownWordProb = 0.5;

		public int AccountId { get; }
		public string SpamFolder { get; }
		public string UnsureFolder { get; }
		public string InboxFolder { get; }
		public string User { get; }

		readonly string _strHost;
		readonly string _strPass;
		readonly int _nPort;
		readonly bool _fUseSsl;
		readonly double _spamCutoff;
		readonly double _hamCutoff;

		int _cHamTotal;
		int _cSpamTotal;
		bool _fTraining;

		CancellationToken _token;

		public Account(int idAccount)
		{
			AccountId = idAccount;

			this.TokenSource = new CancellationTokenSource();

			using (var con = Program.GetConnection(true))
			{
				using (var reader = con.ExecuteReader(@"
SELECT strName, strHost, strUser, strPass, nPort, fUseSsl, fTraining, strInbox, strSpam, strUnsure, cSpam, cHam, nSpamCutoff, nHamCutoff
FROM tblAccounts
WHERE id = @id",
					CommandType.Text,
					Program.DbFactory.CreateParameter("@id", AccountId)))
				{
					if (!reader.Read())
						throw new Exception("account info not found: " + AccountId);

					_strHost = reader.GetString("strHost");
					User = reader.GetString("strUser");
					_strPass = reader.GetString("strPass");
					_nPort = reader.GetInt32("nPort");
					_fUseSsl = reader.GetBoolean("fUseSsl");
					_fTraining = reader.GetBoolean("fTraining");
					InboxFolder = reader.GetString("strInbox");
					SpamFolder = reader.GetString("strSpam");
					UnsureFolder = reader.GetString("strUnsure");
					_cSpamTotal = reader.GetInt32("cSpam");
					_cHamTotal = reader.GetInt32("cHam");
					_spamCutoff = reader.GetFloat("nSpamCutoff");
					_hamCutoff = reader.GetFloat("nHamCutoff");
				}
			}
		}

#if SQLITE
		const string _scopeIdentity = "last_insert_rowid()";
#else
		const string _scopeIdentity = "@@SCOPE_IDENTITY()";
#endif

		public static Account Create(
			string strName,
			string strHost,
			string strUser,
			string strPass,
			int nPort,
			bool fUseSsl,
			string strInbox,
			string strSpam,
			string strUnsure,
			float nSpamCutoff,
			float nHamCutoff
		)
		{
			using (var con = Program.GetConnection(true))
			{
				var id = con.ExecuteScalar<int>(@"
INSERT INTO tblAccounts (strName, strHost, strUser, strPass, nPort, fUseSsl, fTraining, fActive, strInbox, strSpam, strUnsure, cSpam, cHam, nSpamCutoff, nHamCutoff)
VALUES (@strName, @strHost, @strUser, @strPass, @nPort, @fUseSsl, 0, 1, @strInbox, @strSpam, @strUnsure, 0, 0, @nSpamCutoff, @nHamCutoff);
SELECT " + _scopeIdentity,
					CommandType.Text,
					Program.DbFactory.CreateParameter("@strName", strName),
					Program.DbFactory.CreateParameter("@strHost", strHost),
					Program.DbFactory.CreateParameter("@strUser", strUser),
					Program.DbFactory.CreateParameter("@strPass", strPass),
					Program.DbFactory.CreateParameter("@nPort", nPort),
					Program.DbFactory.CreateParameter("@fUseSsl", fUseSsl),
					Program.DbFactory.CreateParameter("@strInbox", strInbox),
					Program.DbFactory.CreateParameter("@strSpam", strSpam),
					Program.DbFactory.CreateParameter("@strUnsure", strUnsure),
					Program.DbFactory.CreateParameter("@nSpamCutoff", nSpamCutoff),
					Program.DbFactory.CreateParameter("@nHamCutoff", nHamCutoff));

				return new Account(id);
			}
		}


		public CancellationTokenSource TokenSource { get; }

		public Task Task { get; private set; }

		public void Run()
		{
			Debug.Assert(!IsRunning);
			this.Task = Task.Run(() => this.Run(this.TokenSource.Token));
		}

		public void Train(string[] rgFolders)
		{
			Debug.Assert(!IsRunning);
			this.Task = Task.Run(() => this.Train(this.TokenSource.Token, rgFolders));
		}

		public void Clean()
		{
			Debug.Assert(!IsRunning);
			this.Task = Task.Run(() => this.Clean(this.TokenSource.Token));
		}


		public void Stop(bool fWait = false)
		{
			Debug.Assert(IsRunning);
			this.TokenSource.Cancel();
			if (fWait)
			{
				this.Task.Wait();
			}
		}

		public bool IsRunning => (this.Task != null && !this.Task.IsCanceled && !this.Task.IsCanceled);

		public string Status => IsRunning ? "RUNNING" : "STOPPED";


		public void Clean(CancellationToken token)
		{
			_token = token;

			using (var imap = GetImapClient())
			{
				using (var imapSearch = GetImapClient())
				{
					void CleanFolder(string strFolder)
					{
						Trace.WriteLine($"cleaning {this.AccountId} / {strFolder}");

						imap.SelectMailbox(strFolder);
						imapSearch.SelectMailbox(strFolder);

						var rgMessageIds = imapSearch.Search(SearchSpam || SearchHam || SearchTrained).ToList();

						foreach (var (start, end) in rgMessageIds.ToRanges(100))
						{
							var rgMessages = imapSearch.GetMessages(start, end, true, true, false);
							imap.RemoveFlags(new[] { "spam", "ham", "trained" }, rgMessages.ToArray());
						}
					}

					CleanFolder(SpamFolder);
					CleanFolder(UnsureFolder);
					CleanFolder(InboxFolder);
				}

				using (var con = Program.GetConnection())
				{
					con.ExecuteNonQuery(
						"DELETE tblTokenCounts WHERE idAccount = @idAccount",
						CommandType.Text,
						Program.DbFactory.CreateParameter("@idAccount", AccountId)
					);

					con.ExecuteNonQuery(
						"DELETE tblMessages WHERE idAccount = @idAccount",
						CommandType.Text,
						Program.DbFactory.CreateParameter("@idAccount", AccountId)
					);

					_cHamTotal = 0;
					_cSpamTotal = 0;
					SaveSpanCounts(con);
				}
			}
		}


		void Train(CancellationToken token, string [] rgFolders)
		{
			_token = token;

			_fTraining = true;
			try
			{
				using (var imap = GetImapClient())
				{
					using (var con = Program.GetConnection())
					{
						if (rgFolders == null || rgFolders.Length == 0)
						{
							ProcessFolder(con, imap, SpamFolder, true);
							ProcessFolder(con, imap, InboxFolder, false);
						}
						else
						{
							foreach (var folder in rgFolders)
							{
								ProcessFolder(con, imap, folder, false);
							}
						}
					}
				}
			}
			finally
			{
				_fTraining = false;
			}
		}

		void Run(CancellationToken token)
		{
			_token = token;

			void Process()
			{
				try
				{
					using (var imap = GetImapClient())
					{
						imap.HandleExists += (sender, count) => imap.StopIdle();
						//imap.HandleFetch += (sender, count) => imap.StopIdle();

						while (true)
						{
							if (_token.IsCancellationRequested)
								return;

							using (var con = Program.GetConnection())
							{
								bool fChanged;
								do
								{
									fChanged = false;
									fChanged |= ProcessFolder(con, imap, SpamFolder, true);
									fChanged |= ProcessFolder(con, imap, InboxFolder, null);
									//cChanged += ProcessFolder(con, "INBOX.old-messages", false);
								}
								while (fChanged);
							}
#if !NETCOREAPP1_1
							System.Data.SqlClient.SqlConnection.ClearAllPools();
#endif

							imap.SelectMailbox(InboxFolder);
							imap.Idle();
						}
					}
				}
				catch (Exception e)
				{
					Debug.WriteLine(e);
				}
			};

#if false
			_imap.NewMessage += (sender, evt) => {
				Debug.WriteLine("NewMessage");
				process();
			};
			_imap.MessageDeleted += (sender, evt) => {
				Debug.WriteLine("MessageDeleted");
				process();
			};


			process();
			_imap.Idle();
#endif

			while (true)
			{
				Process();
				if (_token.IsCancellationRequested)
				{
					return;
				}
			}
		}

		[DebuggerDisplay("{Value}")]
		class TokenRecord
		{
			public RowIdType Id;
			//public int SpamCount;
			//public int HamCount;
			public string Value;

			class EqualityComparer : IEqualityComparer<TokenRecord>
			{
				public bool Equals(TokenRecord x, TokenRecord y) => x.Id == y.Id;
				public int GetHashCode(TokenRecord obj) => obj.Id.GetHashCode();
			}

			public static IEqualityComparer<TokenRecord> Comparer = new EqualityComparer();
		}

		const int _maxTokenLength = 100;

		static readonly double Ln2 = Math.Log(2);

		static readonly SearchCondition SearchHam = SearchCondition.Keyword("ham");
		static readonly SearchCondition SearchSpam = SearchCondition.Keyword("spam");
		static readonly SearchCondition SearchTrained = SearchCondition.Keyword("trained");

		public ImapClient GetImapClient(string strFolder = null)
		{
			var imap = new ImapClient(_strHost, User, _strPass, ImapClient.AuthMethods.Login, _nPort, _fUseSsl, true);

			if (strFolder != null)
				imap.SelectMailbox(strFolder);

			return imap;
		}

		bool ProcessFolder(IDbConnection con, ImapClient imap, string strFolder, bool? fSpamFolder = null)
		{
			var fTraining = _fTraining;

			var mbox = imap.SelectMailbox(strFolder);
			if (mbox == null)
			{
				Trace.WriteLine(string.Format("{0}:{1}\t NOT FOUND", AccountId, strFolder));
				return false;
			}

			SearchCondition searchFlag = null;
			if (fTraining)
			{
				searchFlag = SearchCondition.All;
				if (fSpamFolder == null)
					fSpamFolder = false;
			}
			else
			{
				switch (fSpamFolder)
				{
					case null:
						searchFlag = !SearchHam || SearchSpam;
						break;
					case true:
						searchFlag = !SearchSpam;
						break;
					case false:
						searchFlag = !SearchHam;
						break;
				}
			}

			var rgMessageIds = imap.Search(!SearchCondition.Deleted && searchFlag).ToList();
			var cMessages = rgMessageIds.Count;
			if (cMessages == 0)
				return false;

			if (fTraining && fSpamFolder == null)
				fSpamFolder = false;

			/*
			var rgNewMessageIds = new List<long>();
			using (var imapRead = GetImapClient(strFolder))
			{
				foreach (var (start, end) in rgMessageIds.ToRanges(100))
				{
					if (_token.IsCancellationRequested)
						return false;

					imap.GetMessages(start, end, true, true, false, (MailMessage msg) =>
					{
						var mi = MessageInfo.FromMessage(con, AccountId, msg);
						if (mi == null || mi.IsSpam != fSpamFolder)
							rgNewMessageIds.Add(msg.Uid);
					});
				}
			}

			if (rgNewMessageIds.Count == 0)
				return false;
			*/

			var fChanged = false;
			int iMessage = 0;

			var paramFetchTokenId = Program.DbFactory.CreateParameter("@idToken", DbType.Int32);
			var paramAddTokenId = Program.DbFactory.CreateParameter("@idToken", DbType.Int32);
			var paramUpdateTokenId = Program.DbFactory.CreateParameter("@idToken", DbType.Int32);
			var paramAddSpamCount = Program.DbFactory.CreateParameter("@cSpam", DbType.Int32);
			var paramUpdateSpamCount = Program.DbFactory.CreateParameter("@cSpam", DbType.Int32);
			var paramAddHamCount = Program.DbFactory.CreateParameter("@cHam", DbType.Int32);
			var paramUpdateHamCount = Program.DbFactory.CreateParameter("@cHam", DbType.Int32);

			using (var cmdFetchTokenCounts = con.GetCommand(
				"SELECT cSpam, cHam FROM tblTokenCounts WHERE idAccount = @idAccount AND idToken = @idToken",
				CommandType.Text,
				Program.DbFactory.CreateParameter("@idAccount", AccountId),
				paramFetchTokenId))
			using (var cmdAddTokenCount = con.GetCommand(
				"INSERT INTO tblTokenCounts (idAccount, idToken, cSpam, cHam) VALUES (@idAccount, @idToken, @cSpam, @cHam)",
				CommandType.Text,
				Program.DbFactory.CreateParameter("@idAccount", AccountId),
				paramAddTokenId,
				paramAddSpamCount,
				paramAddHamCount
				))
			using (var cmdUpdateTokenCount = con.GetCommand(
				"UPDATE tblTokenCounts SET cSpam = @cSpam, cHam = @cHam WHERE idAccount = @idAccount AND idToken = @idToken",
				CommandType.Text,
				Program.DbFactory.CreateParameter("@idAccount", AccountId),
				paramUpdateTokenId,
				paramUpdateSpamCount,
				paramUpdateHamCount))
			using (var cmdAddMessage = con.GetCommand(
				"INSERT INTO tblMessages (idAccount, strId, strSubject, fSpam, fTrained, nScore) VALUES (@idAccount, @strId, @strSubject, @fSpam, @fTrained, @nScore)",
				CommandType.Text,
				Program.DbFactory.CreateParameter("@idAccount", AccountId),
				Program.DbFactory.CreateParameter("@strId", DbType.AnsiString),
				Program.DbFactory.CreateParameter("@strSubject", DbType.String),
				Program.DbFactory.CreateParameter("@fSpam", DbType.Boolean),
				Program.DbFactory.CreateParameter("@fTrained", DbType.Boolean),
				Program.DbFactory.CreateParameter("@nScore", DbType.Single)))
			using (var cmdUpdateMessage = con.GetCommand(
				"UPDATE tblMessages SET fSpam=@fSpam, fTrained=@fTrained, nScore=@nScore WHERE id=@id",
				CommandType.Text,
				Program.DbFactory.CreateParameter("@fSpam", DbType.Boolean),
				Program.DbFactory.CreateParameter("@fTrained", DbType.Boolean),
				Program.DbFactory.CreateParameter("@nScore", DbType.Single),
				Program.DbFactory.CreateParameter("@id", DbType.Int32)))
			using (var imapRead = GetImapClient(strFolder))
			{
				foreach (var (start, end) in rgMessageIds.ToRanges(100))
				{
					if (_token.IsCancellationRequested)
						return fChanged;

					imapRead.GetMessages(start, end, true, false, false, (MailMessage msg) =>
					{
						using (var transaction = con.BeginTransaction())
						{
							cmdFetchTokenCounts.Transaction = transaction;
							cmdUpdateMessage.Transaction = transaction;
							cmdAddMessage.Transaction = transaction;
							cmdUpdateTokenCount.Transaction = transaction;
							cmdAddTokenCount.Transaction = transaction;

							iMessage++;
							var rgMessageTokens = GetMessageTokens(transaction, msg).Select(p => p.token).ToArray();

							bool fNew = false;
							var mi = MessageInfo.FromMessage(con, AccountId, msg);
							if (mi == null)
							{
								fNew = true;
								mi = new MessageInfo
								{
									AccountId = AccountId,
									MessageId = MessageInfo.GetUniqueId(msg),
									Subject = msg.Subject,
								};
							}

							if (!fNew || fTraining)
							{
								if (mi.IsSpam != (fSpamFolder == true))
								{
									Trace.WriteLine(string.Format("{0}:{1}\t ({2}/{3}) OLD {4} : {5}", AccountId, strFolder, iMessage, cMessages, MessageInfo.GetSpamText(fSpamFolder == true), msg.Subject));

									foreach (var tr in rgMessageTokens)
									{
										int cSpam = 0, cHam = 0;
										bool fExists = false;

										paramFetchTokenId.Value = tr.Id;
										using (var reader = cmdFetchTokenCounts.ExecuteReader())
										{
											if (reader != null && reader.Read())
											{
												cSpam = reader.GetInt32("cSpam");
												cHam = reader.GetInt32("cHam");
												fExists = true;
											}
										}

										if (fExists && mi.IsTrained)
										{
											if (mi.IsSpam == false && cHam > 0)
												cHam--;
											if (mi.IsSpam == true && cSpam > 0)
												cSpam--;
										}

										if (fSpamFolder == true)
											cSpam++;
										else
											cHam++;

										if (fExists)
										{
											paramUpdateTokenId.Value = tr.Id;
											paramUpdateSpamCount.Value = cSpam;
											paramUpdateHamCount.Value = cHam;
											cmdUpdateTokenCount.ExecuteNonQuery();
										}
										else
										{
											paramAddTokenId.Value = tr.Id;
											paramAddSpamCount.Value = cSpam;
											paramAddHamCount.Value = cHam;
											cmdAddTokenCount.ExecuteNonQuery();
										}
									}

									if (!fNew)
									{
										if (mi.IsSpam == false)
											System.Threading.Interlocked.Decrement(ref _cHamTotal);
										if (mi.IsSpam == true)
											System.Threading.Interlocked.Decrement(ref _cSpamTotal);
									}

									if (fSpamFolder == true)
										System.Threading.Interlocked.Increment(ref _cSpamTotal);
									else
										System.Threading.Interlocked.Increment(ref _cHamTotal);

									SaveSpanCounts(con);

									mi.IsTrained = true;
									mi.IsSpam = fSpamFolder == true;
								}

								SetMessageFlags();
							}
							else
							{
								var spamScore = ClassifyTokenCounts(FetchTokenCounts(transaction, rgMessageTokens));
								mi.Score = (float) spamScore;

								bool? fSpam = null;
								if (spamScore < _hamCutoff)
									fSpam = false;
								else if (spamScore > _spamCutoff)
									fSpam = true;

								Trace.WriteLine(string.Format("{0}:{1}\t ({2}/{3}) NEW {4}({5:N2}) : {6}", AccountId, strFolder, iMessage, cMessages, MessageInfo.GetSpamText(fSpam), spamScore, msg.Subject));

								mi.IsSpam = fSpam;
								SetMessageFlags();

								if (fSpamFolder == null)
								{
									switch (fSpam)
									{
										case true:  // SPAM
											if (strFolder != SpamFolder)
											{
												imap.MoveMessage(msg, SpamFolder);
												msg = null;
												fChanged = true;
											}
											break;
										case null:  // UNSURE
											if (strFolder != UnsureFolder)
											{
												imap.MoveMessage(msg, UnsureFolder);
												msg = null;
												fChanged = true;
											}
											break;
									}
								}
							}

							if (fNew)
							{
								((IDbDataParameter) cmdAddMessage.Parameters["@strId"]).Value = mi.MessageId;
								((IDbDataParameter) cmdAddMessage.Parameters["@strSubject"]).Value = mi.Subject.Substring(0, Math.Min(200, mi.Subject.Length));
								((IDbDataParameter) cmdAddMessage.Parameters["@fSpam"]).Value = (object) mi.IsSpam ?? DBNull.Value;
								((IDbDataParameter) cmdAddMessage.Parameters["@fTrained"]).Value = mi.IsTrained;
								((IDbDataParameter) cmdAddMessage.Parameters["@nScore"]).Value = (object) mi.Score ?? DBNull.Value;
								cmdAddMessage.ExecuteNonQuery();
							}
							else
							{
								((IDbDataParameter) cmdUpdateMessage.Parameters["@id"]).Value = mi.Id;
								((IDbDataParameter) cmdUpdateMessage.Parameters["@fSpam"]).Value = (object) mi.IsSpam ?? DBNull.Value;
								((IDbDataParameter) cmdUpdateMessage.Parameters["@fTrained"]).Value = mi.IsTrained;
								((IDbDataParameter) cmdUpdateMessage.Parameters["@nScore"]).Value = (object) mi.Score ?? DBNull.Value;
								cmdUpdateMessage.ExecuteNonQuery();
							}

							void SetMessageFlags()
							{
								switch (mi.IsSpam)
								{
									case true:
										fChanged |= imap.AddFlags(new[] { "spam" }, msg);
										fChanged |= imap.RemoveFlags(new[] { "ham" }, msg);
										break;
									default:
									case false:
										fChanged |= imap.AddFlags(new[] { "ham" }, msg);
										fChanged |= imap.RemoveFlags(new[] { "spam" }, msg);
										break;
								}
							}

							transaction.Commit();
						}
					});
				}
			}

			if (fChanged)
			{
				imap.Expunge();
			}

			return fChanged;
		}

		void SaveSpanCounts(IDbConnection con)
		{
			using (var cmdUpdateSpamCounts = con.GetCommand(
				"UPDATE tblAccounts SET cSpam = @cSpam, cHam = @cHam WHERE Id = @idAccount",
				CommandType.Text,
				Program.DbFactory.CreateParameter("@idAccount", AccountId),
				Program.DbFactory.CreateParameter("@cSpam", _cSpamTotal),
				Program.DbFactory.CreateParameter("@cHam", _cHamTotal)))
			{
				cmdUpdateSpamCounts.ExecuteNonQuery();
			}
		}

		static IEnumerable<(TokenRecord token, int count)> GetMessageTokens(IDbTransaction transaction, MailMessage msg)
		{
			using (var cmdFindToken = transaction.GetCommand(
				"SELECT id from tblTokens WHERE value = @value",
				CommandType.Text,
				Program.DbFactory.CreateParameter("@value", DbType.String)))
			using (var cmdAddToken = transaction.GetCommand(
				"INSERT INTO tblTokens (value) VALUES (@value); SELECT CAST(" + _scopeIdentity + " as Int);",
				CommandType.Text,
				Program.DbFactory.CreateParameter("@value", DbType.String)))
			{
				Func<string, TokenRecord> FetchToken = strToken =>
				{
					return _mapTokens.GetOrAddSafe(strToken, str =>
					{
						((IDbDataParameter) cmdFindToken.Parameters[0]).Value = strToken;
						RowIdType idToken = 0;
						using (var reader = cmdFindToken.ExecuteReader())
						{
							if (reader.Read())
								idToken = reader.GetInt32("id");
						}

						if (idToken == 0)
						{
							((IDbDataParameter) cmdAddToken.Parameters[0]).Value = strToken;
							idToken = cmdAddToken.ExecuteScalar<RowIdType>();
						}

						return new TokenRecord
						{
							Id = idToken,
							Value = strToken,
						};
					});
				};

				return
					Tokenize(msg)
					.GroupBy(t => t)
					.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
					.Select(group =>
					{
						var strToken = group.Key;
						var count = group.Count();

						if (strToken.Length > _maxTokenLength)
							strToken = strToken.Substring(0, _maxTokenLength);

						TokenRecord token;
						try
						{
							token = FetchToken(strToken);
						}
						catch (DbException)
						{
							token = FetchToken(strToken);
						}

						return (token, count);
					})
					.GroupBy(tc => tc.Item1, (token, rgTCs) =>
						(
							Token: token,
							Count: rgTCs.Sum(tc => tc.Item2)
						),
						TokenRecord.Comparer
					)
					.OrderBy(tc => tc.Token.Id);
			}
		}

		IEnumerable<(int cSpam, int cHam)> FetchTokenCounts(IDbTransaction con, IEnumerable<TokenRecord> tokens)
		{
			IDbDataParameter paramTokenId = Program.DbFactory.CreateParameter("@idToken", DbType.Int32);

			using (var cmdFetchTokenCounts = con.GetCommand(
				"SELECT cSpam, cHam FROM tblTokenCounts WHERE idAccount = @idAccount AND idToken = @idToken",
				CommandType.Text,
				Program.DbFactory.CreateParameter("@idAccount", AccountId),
				paramTokenId))
			{
				foreach (var tr in tokens)
				{
					paramTokenId.Value = tr.Id;
					foreach (var reader in cmdFetchTokenCounts.ExecuteRows())
						yield return (reader.GetInt32("cSpam"), reader.GetInt32("cHam"));
				}
			}
		}

		double ClassifyTokenCounts(IEnumerable<(int cSpam, int cHam)> counts)
		{
			double cSpamTotal = _cSpamTotal;
			double cHamTotal = _cHamTotal;

			double H = 1.0;
			double S = 1.0;

			double Hexp = 0;
			double Sexp = 0;

			var cRecords = 0;

			foreach (var (cSpam, cHam) in counts)
			{
				//var cSpam = count.Item1;
				//var cHam = count.Item2;
				if (cSpam <= 0 && cHam <= 0)
					continue;

				cRecords++;

				var hamRatio = cHam / cHamTotal;
				var spamRatio = cSpam / cSpamTotal;

				var prob = spamRatio / (hamRatio + spamRatio);
				Debug.Assert(prob >= 0 && prob <= 1);

				var n = cHam + cSpam;

				prob = (_unknownWordStrength * _unknownWordProb + n * prob) / (_unknownWordStrength + n);
				Debug.Assert(prob >= 0 && prob <= 1);

				S *= 1.0 - prob;
				H *= prob;

				Debug.Assert(!double.IsNaN(S));
				Debug.Assert(!double.IsNaN(H));

				if (S < 1e-200)
				{
					int e;
					(S, e) = S.frexp();
					Sexp += e;
				}

				if (H < 1e-200)
				{
					int e;
					(H, e) = H.frexp();
					Hexp += e;
				}

				Debug.Assert(!double.IsNaN(S));
				Debug.Assert(!double.IsNaN(H));
			}

			if (cRecords == 0)
				return 0.5;

			Debug.Assert(!double.IsNaN(S));
			Debug.Assert(!double.IsNaN(H));

			var S2 = Math.Log(S) + Sexp * Ln2;
			var H2 = Math.Log(H) + Hexp * Ln2;

			Debug.Assert(!double.IsNaN(S2));
			Debug.Assert(!double.IsNaN(H2));

			var S3 = 1 - Utils.chi2Q(-2 * S2, 2 * cRecords);
			var H3 = 1 - Utils.chi2Q(-2 * H2, 2 * cRecords);

			Debug.Assert(!double.IsNaN(S3));
			Debug.Assert(!double.IsNaN(H3));

			// How to combine these into a single spam score?  We originally
			// used (S-H)/(S+H) scaled into [0., 1.], which equals S/(S+H).  A
			// systematic problem is that we could end up being near-certain
			// a thing was (for example) spam, even if S was small, provided
			// that H was much smaller.
			// Rob Hooft stared at these problems and invented the measure
			// we use now, the simpler S-H, scaled into [0., 1.].
			return (S3 - H3 + 1) / 2;
		}

		static readonly Regex _reSplitFilename = new Regex(@"[/\\:]", RegexOptions.Compiled);
		static readonly Regex _reSplitUrl = new Regex("[;?:@&=+,$.]", RegexOptions.Compiled);

		static readonly Regex _reTokenizeSubject = new Regex(@"[\w\x80-\xff$%]+", RegexOptions.Compiled);
		static readonly Regex _reTokenizePunctuation = new Regex(@"\W+", RegexOptions.Compiled);

		const int skip_max_word_size = 12;

		static IEnumerable<string> TokenizeWord(string word)
		{
			var cch = word.Length;
			if (cch >= 3)
			{
				if (cch < 40 && word.Contains('.') && word.Contains('@'))
				{
					word = word.Trim("<>[]\"'".ToArray());
					var rgParts = word.Split('@');
					if (rgParts.Length == 2)
					{
						var name = rgParts[0];
						if (name.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
							name = name.Substring("mailto:".Length);

						yield return "email name:" + name;
						yield return "email addr:" + rgParts[1];

						yield break;
					}
				}

				if (cch <= skip_max_word_size)
					yield return word;
			}
		}

		static readonly string[] _rgAddressHeaders = { "from", "to", "cc", "sender", "reply-to" };
		static readonly string[] _rgAddressCountHeaders = { "to", "cc" };

		static readonly string[] _rgSafeHeaders = {
			"abuse-reports-to", "date", "errors-to",
			"from", "importance", "in-reply-to",
			"message-id", "mime-version",
			"organization", "received",
			"reply-to", "return-path", "subject",
			"to", "user-agent", "x-abuse-info",
			"x-complaints-to", "x-face"
		};

		static readonly Regex _reFindEmails = new Regex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,6}", RegexOptions.Compiled);
		static readonly Regex _reMessageId = new Regex(@"\s*<[^@]+@([^>]+)>\s*", RegexOptions.Compiled);

		static readonly Regex _reFindEntity = new Regex(@"&#(\d+);", RegexOptions.Compiled);
		static readonly Regex _reQuotedPrintable = new Regex(@"=([0-9A-F][0-9A-F])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		static readonly Regex _reStripHTMLSpace = new Regex(@" &nbsp; | < (?: p | br ) > ", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
		static readonly Regex _reStripHTML = new Regex(@" < [^\s<>] [^>]{0,256} > ", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);

		static readonly Stripper _stripUUencode = new Stripper(
			new Regex(@" ^begin \s+ (\S+) \s+ (\S+) \s* $", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline),
			new Regex(@"^end\s*$", RegexOptions.Compiled | RegexOptions.Multiline),
			m =>
			{
				return TokenizeFilename(m.Groups[2].Value).Select(s => "uuencode:" + s)
					.Append("uuencode mode" + m.Groups[1].Value);
			}
		);

		static readonly Stripper _stripStyle = new Stripper(
			new Regex(@"< \s* style \s+ [^>]* >", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline),
			new Regex(@"</ \s* style [^>]* >", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline)
		);

		static readonly Stripper _stripComments = new Stripper(
			new Regex(@"<!-- | < \s* comment \s* [^>]* >", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline),
			new Regex(@"-->|</ \s* comment [^>]* >", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline)
		);

		static readonly Stripper _stripNoframes = new Stripper(
			new Regex(@"< \s* noframes \s+ [^>]* >", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline),
			new Regex(@"</ \s* noframes [^>]* >", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline)
		);

		static readonly Stripper[] _rgStrippers = {
			_stripUUencode,
			_stripStyle,
			_stripComments,
			_stripNoframes,
		};

		static IEnumerable<string> TokenizeFilename(string strFilename)
		{
			if (!string.IsNullOrEmpty(strFilename))
			{
				yield return "fname:" + strFilename;

				var rgFilenameParts = _reSplitFilename.Split(strFilename);
				foreach (var strFilenamePart in rgFilenameParts)
				{
					if (rgFilenameParts.Length > 1)
						yield return "fname comp:" + strFilenamePart;

					var rgFilenamePieces = _reSplitUrl.Split(strFilenamePart);
					if (rgFilenamePieces.Length > 1)
					{
						foreach (var strPiece in rgFilenamePieces)
							yield return "fname piece:" + strPiece;
					}
				}
			}
		}

		static readonly char[] _rgTrimChars = "\\\"`'!?.+-*/=><_ ,:;&|^(){}“”[]".ToArray();
		static readonly Regex _reSpace = new Regex(@"\s+|[\.,""!?:;]", RegexOptions.Compiled);

		static IEnumerable<string> TokenizeText(string text)
		{
			foreach (var w in _reSpace.Split(text))
			{
				var w2 = w.Trim(_rgTrimChars);
				foreach (var token in TokenizeWord(w2))
					yield return token;
			}
		}

		static readonly Regex _rePrice = new Regex(@"\$ ( [0-9,.]+ )  ( \s* ( / | per) \s*  [a-z]+ )? ", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);

		static readonly Regex _reColor = new Regex(@"\# ( [0-9a-f]{6} | [0-9a-f]{3} ) (?![0-9a-f]) ", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);

		static IEnumerable<string> Tokenize(MailMessage msg)
		{
			foreach (var part in msg.AllAttachments().Cast<ObjectWHeaders>().Append(msg))
			{
				var rgExtraTokens = new List<string>();

				var strContentType = part.Headers["Content-Type"].Value;
				var strMainType = strContentType.Split('/').FirstOrDefault();
				if (strMainType == "multipart")
					continue;

				yield return "content-type:" + strContentType;

				var type = part.Headers["Content-Type"]["type"];
				if (!string.IsNullOrEmpty(type))
					yield return "content-type/type:" + type;

				if (!string.IsNullOrEmpty(part.Headers["Content-Disposition"].Value))
					yield return "content-disposition:" + part.Headers["Content-Disposition"].Value;

				var strFilename = part.Headers["Content-Disposition"]["filename"];
				foreach (var strFilenamePart in TokenizeFilename(strFilename))
					yield return "filename:" + strFilenamePart;

				var strContentTransferEncoding = part.ContentTransferEncoding;
				if (!string.IsNullOrEmpty(strContentTransferEncoding))
					yield return "content-transfer-encoding:" + strContentTransferEncoding;

				if (strMainType != "text")
					continue;

				var strBody = part.Body.ToLowerInvariant();

				if (strContentTransferEncoding == "quoted-printable")
				{
					strBody = _reQuotedPrintable.Replace(strBody, m =>
					{
						var n = int.Parse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
						return ((char) n).ToString();
					});
				}

				HtmlNode body = null;
				try
				{
					var docTmp = new HtmlDocument();
					docTmp.LoadHtml(strBody);
					body = docTmp.DocumentNode.SelectSingleNode("/html/body");
				}
				catch { }

				if (body != null)
				{
					foreach (var node in body.QuerySelectorAll("script").ToList())
						node.Remove();

					foreach (var node in body.QuerySelectorAll("style").ToList())
						node.Remove();

					foreach (var node in body.QuerySelectorAll("font[size='1']").ToList())
						node.Remove();

					foreach (var node in body.QuerySelectorAll("font[size='0']").ToList())
						node.Remove();

					strBody = body.InnerText;

					strBody = _stripComments.Analyze(strBody).Item1;

					foreach (var eltLink in body.QuerySelectorAll("a"))
					{
						var hrefAttr = eltLink.Attributes["href"];
						if (hrefAttr != null)
						{
							var href = hrefAttr.Value;
							if (Uri.TryCreate(href, UriKind.Absolute, out Uri uri))
							{
								yield return "token:link-scheme-" + uri.Scheme;
								yield return "token:link:" + uri.Host;
							}
							else if (href.IndexOf("javascript:", StringComparison.OrdinalIgnoreCase) >= 0)
							{
								yield return "token:link-javascript";
							}
						}
					}
				}

				strBody = _reFindEntity.Replace(strBody, m =>
				{
					string strValue = m.Groups[1].Value;
					var value = int.Parse(strValue);
					var ch = (char) value;

					return ch.ToString();
				});

				strBody = Utils.ReplaceEntities(strBody);

				foreach (var stripper in _rgStrippers)
				{
					var res = stripper.Analyze(strBody);

					strBody = res.Item1;
					foreach (var token in res.Item2)
						yield return token;
				}

				strBody = _reStripHTMLSpace.Replace(strBody, " ");
				strBody = _reStripHTML.Replace(strBody, "");

				strBody = _reColor.Replace(strBody, m =>
				{
					rgExtraTokens.Add("token:color");
					return "";
				});

				strBody = _rePrice.Replace(strBody, m =>
				{
					if (m.Groups[2].Success)
						rgExtraTokens.Add("token:price/" + m.Groups[2].Value);
					else
						rgExtraTokens.Add("token:price");

					return "";
				});

				foreach (var token in TokenizeText(strBody))
					yield return token;

				foreach (var token in rgExtraTokens)
					yield return token;
			}

			var strSubject = msg.Subject;
			if (string.IsNullOrWhiteSpace(strSubject))
			{
				yield return "subject-empty:";
			}
			else
			{
				foreach (var strSubjectWord in _reTokenizeSubject.FindAll(strSubject))
				{
					foreach (var token in TokenizeWord(strSubjectWord))
					{
						if (!string.IsNullOrWhiteSpace(strSubjectWord))
							yield return "subject:" + token;
					}
				}
				foreach (var strSubjectPunctuation in _reTokenizePunctuation.FindAll(strSubject))
				{
					if (!string.IsNullOrWhiteSpace(strSubjectPunctuation))
						yield return "subject:" + strSubjectPunctuation;
				}
			}

			foreach (var strHeader in _rgAddressHeaders)
			{
				var strValue = msg.Headers[strHeader].Value;
				if (strValue == null)
				{
					yield return strHeader + ":none";
				}
				else
				{
					var cNonnames = 0;

					foreach (var strEmail in _reFindEmails.FindAll(strValue))
					{
						var rgParts = strEmail.Split('@');
						if (rgParts.Length == 2)
						{
							if (!string.IsNullOrEmpty(rgParts[0]))
								yield return string.Format("{0}:name:{1}", strHeader, rgParts[0]);
							else
								cNonnames++;

							if (!string.IsNullOrEmpty(rgParts[1]))
								yield return string.Format("{0}:addr:{1}", strHeader, rgParts[1]);
							else
								yield return string.Format("{0}:addr:none", strHeader);
						}
					}

					if (cNonnames > 0)
						yield return string.Format("{0}:no real name:2**{1}", strHeader, Math.Log(cNonnames) / Ln2);
				}
			}

			foreach (var strHeader in _rgAddressCountHeaders)
			{
				var strValue = msg.Headers[strHeader].Value;
				var cAddresses = strValue.Count(ch => ch == ',');
				if (cAddresses > 0)
					yield return string.Format("{0}:2**{1}", strHeader, Math.Log(cAddresses) / Ln2);
			}

			var strId = msg.MessageID;
			Match match = null;
			if (!string.IsNullOrWhiteSpace(strId))
				match = _reMessageId.Match(strId);

			if (match?.Success == true)
				yield return "message-id:@" + match.Groups[1].Value;
			else
				yield return "message-id:invalid";
		}
	}
}
