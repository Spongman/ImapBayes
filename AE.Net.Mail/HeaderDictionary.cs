using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace AE.Net.Mail
{
	public class HeaderDictionary : SafeDictionary<string, HeaderValue>
	{
		public HeaderDictionary() : base(StringComparer.OrdinalIgnoreCase) { }

		public virtual string GetBoundary()
		{
			return this["Content-Type"]["boundary"];
		}

		static readonly Regex[] rxDates = new[]{
			@"\d{1,2}\s+[a-z]{3}\s+\d{2,4}\s+\d{1,2}\:\d{2}\:\d{1,2}\s+[\+\-\d\:]*",
			@"\d{4}\-\d{1,2}-\d{1,2}\s+\d{1,2}\:\d{2}(?:\:\d{2})?(?:\s+[\+\-\d:]+)?",
		}.Select(x => new Regex(x, RegexOptions.Compiled | RegexOptions.IgnoreCase)).ToArray();

		public virtual DateTime GetDate()
		{
			var value = this["Date"].RawValue.ToNullDate();
			if (value == null)
			{
				foreach (var rx in rxDates)
				{
					var match = rx.Matches(this["Received"].RawValue ?? "")
					  .Cast<Match>().LastOrDefault();
					if (match != null)
					{
						value = match.Value.ToNullDate();
						if (value != null)
							break;
					}
				}
			}

			//written this way so a break can be set on the null condition
			if (value == null)
				return DateTime.MinValue;

			return value.Value;
		}

		public virtual T GetEnum<T>(string name) where T : struct, IConvertible
		{
			var value = this[name].RawValue;
			if (string.IsNullOrEmpty(value))
				return default(T);

			var values = System.Enum.GetValues(typeof(T)).Cast<T>();
			return values.FirstOrDefault(x => x.ToString().Equals(value, StringComparison.OrdinalIgnoreCase));
		}

		public virtual IEnumerable<MailAddress> GetMailAddresses(string header)
		{
			const int notFound = -1;

			var headerValue = this[header].RawValue.Trim();

			var mailAddressStartIndex = 0;

			while (mailAddressStartIndex < headerValue.Length)
			{
				while (mailAddressStartIndex < headerValue.Length
					&& char.IsWhiteSpace(headerValue[mailAddressStartIndex]))
				{
					mailAddressStartIndex++;
				}

				if (mailAddressStartIndex >= headerValue.Length)
					break;

				var mailAddressEndIndex = mailAddressStartIndex;

				var chStart = headerValue[mailAddressStartIndex];
				if (chStart == '\'' || chStart == '\"')
				{
					mailAddressEndIndex = headerValue.IndexOf(chStart, mailAddressEndIndex + 1);
					if (mailAddressEndIndex == -1)
						break;
				}

				mailAddressEndIndex = headerValue.IndexOf(',', mailAddressEndIndex);
				if (mailAddressEndIndex == notFound)
					mailAddressEndIndex = headerValue.Length;

				var possibleMailAddress = headerValue.Substring(mailAddressStartIndex, mailAddressEndIndex - mailAddressStartIndex);

				var mailAddress = possibleMailAddress.Trim().ToEmailAddress();
				if (mailAddress != null)
					yield return mailAddress;

				mailAddressStartIndex = mailAddressEndIndex + 1;
			}
		}

		static string Decode(string line)
		{
			line = line.Trim();

			if (!line.StartsWith("=?") || !line.EndsWith("?="))
				return line;

			var rgParts = line.Split('?');
			if (rgParts.Length != 5)
				return line;

			var charsetName = rgParts[1].Trim().ToLower();
			Encoding decoder = null;
			switch (charsetName)
			{
				case "us-ascii":
				case "windows-1252":
				case "iso-8859-1":
					decoder = Encoding.ASCII;
					break;
				case "utf-8":
					decoder = Encoding.UTF8;
					break;
				case "utf-7":
					decoder = Encoding.UTF7;
					break;
				default:
					return line;
			}

			var encoded = rgParts[3];

			switch (rgParts[2].ToLower())
			{
				case "b":
					return decoder.GetString(Convert.FromBase64String(encoded));
				case "q":
					return Utilities.DecodeQuotedPrintable(encoded, decoder);
				default:
					return line;
			}
		}

		public static HeaderDictionary Parse(string headers, Encoding encoding)
		{
			headers = Utilities.DecodeWords(headers, encoding);
			var temp = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var lines = headers.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			int i;
			string key = null, value;
			foreach (var line in lines)
			{
				if (key != null && (line[0] == '\t' || line[0] == ' '))
				{
					temp[key] += Decode(line);
				}
				else
				{
					i = line.IndexOf(':');
					if (i > -1)
					{
						key = line.Substring(0, i).Trim();
						value = Decode(line.Substring(i + 1));

						temp.Set(key, value);
					}
				}
			}

			var result = new HeaderDictionary();
			foreach (var item in temp)
				result.Add(item.Key, new HeaderValue(item.Value));

			return result;
		}
	}
}
