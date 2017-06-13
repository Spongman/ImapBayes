using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using System.Collections.ObjectModel;

namespace AE.Net.Mail.Imap {
	public class Namespaces {
		readonly Collection<Namespace> _servernamespace = new Collection<Namespace>();
		readonly Collection<Namespace> _usernamespace = new Collection<Namespace>();
		readonly Collection<Namespace> _sharednamespace = new Collection<Namespace>();

		public virtual Collection<Namespace> ServerNamespace => this._servernamespace;
		public virtual Collection<Namespace> UserNamespace => this._usernamespace;
		public virtual Collection<Namespace> SharedNamespace => this._sharednamespace;
	}

	public class Namespace {
		public Namespace(string prefix, string delimiter) {
			Prefix = prefix;
			Delimiter = delimiter;
		}

		public Namespace() { }
		public virtual string Prefix { get; internal set; }
		public virtual string Delimiter { get; internal set; }
	}
}