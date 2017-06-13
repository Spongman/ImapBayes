using System;
using System.Collections;

namespace AE.Net.Mail.Imap {
	public class Quota {
		readonly string ressource;
		readonly string usage;
		public Quota(string ressourceName, string usage, int used, int max) {
			this.ressource = ressourceName;
			this.usage = usage;
			this.Used = used;
			this.Max = max;
		}

		public virtual int Used { get; }
		public virtual int Max { get; }
	}
}