#define VERBOSE

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace AE.Net.Mail
{
	public abstract class TextClient : IDisposable
	{

		static int __id = 0;

		int _id = __id++;

		protected TcpClient _connection;
		protected Stream _streamInput;
		protected Stream _streamOutput;
		readonly object _lockObject = new object();

		public virtual string Host { get; private set; }
		public virtual int Port { get; set; }
		public virtual bool Ssl { get; set; }
		public virtual bool IsConnected { get; private set; }
		public virtual bool IsAuthenticated { get; private set; }
		public virtual bool IsDisposed { get; private set; }
		public virtual System.Text.Encoding Encoding { get; set; }

		public event EventHandler<WarningEventArgs> Warning;

		protected virtual void RaiseWarning(MailMessage mailMessage, string message)
		{
			Warning?.Invoke(this, new WarningEventArgs { MailMessage = mailMessage, Message = message });
		}

		protected TextClient()
		{
			//Encoding = System.Text.Encoding.GetEncoding(1252);
			Encoding = System.Text.Encoding.UTF8;
		}

		internal abstract void OnLogin(string username, string password);
		internal abstract void OnLogout();
		internal abstract void CheckResultOK(string result);

		protected virtual void OnConnected(string result)
		{
			CheckResultOK(result);
		}

		protected virtual void OnDispose() { }

		public virtual void Login(string username, string password)
		{
			if (!IsConnected)
				throw new Exception("You must connect first!");

			IsAuthenticated = false;
			OnLogin(username, password);
			IsAuthenticated = true;
		}

		public virtual void Logout()
		{
			IsAuthenticated = false;
			OnLogout();
		}

		public virtual void Connect(string hostname, int port, bool ssl, bool skipSslValidation)
		{
			System.Net.Security.RemoteCertificateValidationCallback validateCertificate = null;
			if (skipSslValidation)
				validateCertificate = (sender, cert, chain, err) => true;

			Connect(hostname, port, ssl, validateCertificate);
		}

		public virtual void Connect(string hostname, int port, bool ssl, System.Net.Security.RemoteCertificateValidationCallback validateCertificate)
		{
			if (IsConnected)
				throw new Exception("Already connected!");

			try
			{
				Host = hostname;
				Port = port;
				Ssl = ssl;

				_connection = new TcpClient
				{
					ReceiveBufferSize = 64 * 1024
				};

				var task = _connection.ConnectAsync(hostname, port);
				task.Wait();

				_streamOutput = _connection.GetStream();
				if (ssl)
				{
					System.Net.Security.SslStream sslStream;
					if (validateCertificate != null)
						sslStream = new System.Net.Security.SslStream(_streamOutput, false, validateCertificate);
					else
						sslStream = new System.Net.Security.SslStream(_streamOutput, false);

					_streamOutput = sslStream;
					sslStream.AuthenticateAsClientAsync(hostname).Wait();
				}

				//if (_streamOutput.CanTimeout)
				//	_streamOutput.ReadTimeout = 10 * 1000;
				//_streamInput = new BufferedStream(_streamOutput, 64 * 1024);
				_streamInput = new BufferedInputStream(_streamOutput, 4 * 64 * 1024);

				OnConnected(GetResponse());

				IsConnected = true;
				Host = hostname;
			}
			catch (Exception)
			{
				IsConnected = false;
				Utilities.TryDispose(ref _streamOutput);
				Utilities.TryDispose(ref _streamInput);
				throw;
			}
		}

		protected virtual void CheckConnectionStatus()
		{
			if (IsDisposed)
				throw new ObjectDisposedException(this.GetType().Name);

			if (!IsConnected)
				throw new Exception("You must connect first!");

			if (!IsAuthenticated)
				throw new Exception("You must authenticate first!");
		}

		protected virtual void SendCommand(string command)
		{
#if VERBOSE
			Debug.WriteLine(string.Format("{0}\t > {1}", System.Threading.Thread.CurrentThread.ManagedThreadId, command));
#endif
			var bytes = System.Text.Encoding.UTF8.GetBytes(command + "\r\n");
			_streamOutput.Write(bytes, 0, bytes.Length);
		}

		protected virtual string SendCommandGetResponse(string command)
		{
			SendCommand(command);
			return GetResponse();
		}

		protected virtual string GetResponse()
		{
			string line = _streamInput.ReadLine(Encoding, null);
#if VERBOSE
			Debug.WriteLine(string.Format("{0}\t < {1}", System.Threading.Thread.CurrentThread.ManagedThreadId, line));
#endif
			return line;
		}

		/*
		protected async Task<string> GetResponseAsync()
		{
			string line = await _streamInput.ReadLineAsync(Encoding, null);
			Debug.WriteLine(string.Format("{0}\t < {1}", System.Threading.Thread.CurrentThread.ManagedThreadId, line));
			return line;
		}
		*/

		protected virtual void SendCommandCheckOK(string command)
		{
			CheckResultOK(SendCommandGetResponse(command));
		}

		public virtual void Disconnect()
		{
			if (IsAuthenticated)
				Logout();

			lock (_lockObject)
			{
				if (_connection != null)
				{
					if (_connection.Client != null && _connection.Client.Connected)
					{
						_connection.Client.Disconnect(false);
						_connection.Client.Dispose();
					}
					_connection.Close();
				}
				Utilities.TryDispose(ref _connection);
				_connection = null;

				if (_streamInput != null)
					_streamInput.Close();
				Utilities.TryDispose(ref _streamInput);

				if (_streamOutput != null)
					_streamOutput.Close();
				Utilities.TryDispose(ref _streamOutput);

				this.IsConnected = false;
			}
		}

		public virtual void Dispose()
		{
			if (IsDisposed)
				return;

			lock (_lockObject)
			{
				if (IsDisposed)
					return;

				IsDisposed = true;
				Disconnect();

				try
				{
					OnDispose();
				}
				catch (Exception) { }

				_streamInput = null;
				_streamOutput = null;
				_connection = null;
			}
//			GC.SuppressFinalize(this);
		}
	}
}
