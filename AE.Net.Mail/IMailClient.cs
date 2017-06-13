using System;

namespace AE.Net.Mail {
	public interface IMailClient : IDisposable {
		int GetMessageCount();
		MailMessage GetMessage(int index, bool headersonly = false);
		MailMessage GetMessage(long uid, bool headersonly = false);
		bool DeleteMessage(AE.Net.Mail.MailMessage msg);
		void Disconnect();

		event EventHandler<WarningEventArgs> Warning;
	}
}
