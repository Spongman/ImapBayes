
using System.Diagnostics;
namespace AE.Net.Mail.Imap
{
	[DebuggerDisplay("{Name}")]
	public class Mailbox
	{
		public Mailbox() : this("") { }
		public Mailbox(string name)
		{
			Name = ModifiedUtf7Encoding.Decode(name);
			Flags = new string[0];
		}

		internal Mailbox(string name, string flags, string delimiter)
			: this(name)
		{
			this.SetFlags(flags);
			this.Delimiter = delimiter;
		}

		public virtual long Uid { get; internal set; }

		public virtual string Name { get; internal set; }
		public virtual int NumNewMsg { get; internal set; }
		public virtual int NumMsg { get; internal set; }
		public virtual int NumUnSeen { get; internal set; }
		public virtual int UIDValidity { get; internal set; }
		public virtual string[] Flags { get; internal set; }
		public virtual bool IsWritable { get; internal set; }

		public string Delimiter { get; internal set; }

		internal void SetFlags(string flags)
		{
			Flags = flags.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
		}

		public static string GetParentName (string name, string delimiter)
		{
			int ich = name.LastIndexOf(delimiter);
			if (ich < 0)
				return null;

			return name.Substring(0, ich);
		}
	}
}

