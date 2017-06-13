using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AE.Net.Mail
{
	public class BufferedInputStream : Stream
	{
		public Stream _stream;
		readonly byte[] _rgb;
		int _ib;
		int _cb;

		public BufferedInputStream(Stream stream, int size = 16 * 1024)
		{
			_stream = stream;
			_rgb = new byte[size];
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int cb = 0;
			if (_cb > 0)
			{
				cb = Math.Min(count, _cb);
				Buffer.BlockCopy(_rgb, _ib, buffer, offset, cb);
				_ib += cb;
				_cb -= cb;
				count -= cb;

				if (count == 0)
					return cb;

				offset += cb;
			}

			return cb + _stream.Read(buffer, offset, count);
		}

		public override int ReadByte()
		{
			if (_cb == 0)
			{
				_cb = _stream.Read(_rgb, 0, _rgb.Length);
				if (_cb == 0)
					return -1;

				_ib = 0;
			}

			_cb--;
			return _rgb[_ib++];
		}

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override void Flush()
		{
			throw new InvalidOperationException();
		}

		public override long Length => throw new InvalidOperationException();
		public override long Position
		{
			get => throw new InvalidOperationException();
			set => throw new InvalidOperationException();
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new InvalidOperationException();

		public override void SetLength(long value) => throw new InvalidOperationException();

		public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException();
	}

	internal static class Utilities
	{
		static readonly CultureInfo _enUsCulture = new CultureInfo("en-US");// CultureInfo.GetCultureInfo("en-US");

#if MAX_LENGTH
		internal static void CopyStream(Stream a, Stream b, int? maxLength, int bufferSize = 8192)
		{
			var buffer = new byte[bufferSize];
			while (maxLength == null || maxLength > 0)
			{
				int read = maxLength == null ? bufferSize : Math.Min(bufferSize, maxLength.Value);
				read = a.Read(buffer, 0, read);
				if (read == 0) return;
				if (maxLength != null)
					maxLength -= read;
				b.Write(buffer, 0, read);
			}
		}
#endif

		public static NameValueCollection ParseImapHeader(string data)
		{
			var values = new NameValueCollection();
			if (data != null)
			{
				string name = null;
				int nump = 0;
				bool fQuote = false;
				var temp = new StringBuilder();

				foreach (var c in data)
				{
					if (fQuote)
					{
						temp.Append(c);
						fQuote &= c != '"';
					}
					else
					{
						switch (c)
						{
							case ' ':
								if (name == null)
								{
									name = temp.ToString();
									temp.Clear();
								}
								else if (nump == 0)
								{
									values[name] = temp.ToString().Trim('"');
									name = null;
									temp.Clear();
								}
								else
								{
									temp.Append(c);
								}

								break;
							case '(':
								if (nump > 0)
									temp.Append(c);

								nump++;
								break;
							case ')':
								nump--;
								if (nump > 0)
									temp.Append(c);

								break;
							default:
								temp.Append(c);
								fQuote |= c == '"';

								break;
						}
					}
				}
				if (name != null)
				{
					values[name] = temp.ToString();
				}
			}

			return values;
		}

		internal static string ReadLine(this Stream stream, Encoding encoding, char? termChar)
		{
			if (stream.CanTimeout)
			{
				stream.ReadTimeout = 10000;
			}

			using (var mem = new MemoryStream())
			{
				bool fRead = false;

				while (true)
				{
					int ch = stream.ReadByte();
					if (ch == -1)
					{
						break;
					}
					//throw new EndOfStreamException();

					fRead = true;

					if (ch == 10 && mem.Length == 0)
					{
						continue;
					}
					if (ch == 13)
					{
						break;
					}

					mem.WriteByte((byte) ch);
				}

				if (!fRead)
				{
					return null;
				}

				var strLine = encoding.GetString(mem.ToArray());
				//Debug.WriteLine ("ReadLine: " + strLine);
				return strLine;
			}
		}

#if MAX_LENGTH

		internal static byte[] Read(this Stream stream, int len)
		{
			var data = new byte[len];
			int read, pos = 0;
			while (pos < len && (read = stream.Read(data, pos, len - pos)) > 0)
			{
				pos += read;
			}
			return data;
		}

		internal static string ReadLine(this Stream stream, ref int? maxLength, Encoding encoding, char? termChar)
		{
			if (stream.CanTimeout)
				stream.ReadTimeout = 10000;

			var maxLengthSpecified = maxLength > 0;
			using (var mem = new MemoryStream())
			{
				var read = false;
				byte b = 0;
				while (true)
				{
					byte b0 = b;
					int i = stream.ReadByte();
					if (i == -1)
						throw new EndOfStreamException();
						//break;
					read = true;

					b = (byte) i;
					if (maxLengthSpecified)
					{
						maxLength--;

						if (mem.Length == 1 && b == termChar && b0 == termChar)
						{
							maxLength++;
							continue;
						}
					}

					if (b == 10 && mem.Length == 0)
						continue;
					else if (b == 13)
						break;

					mem.WriteByte(b);
					if (maxLengthSpecified && maxLength == 0)
						break;
				}

				if (mem.Length == 0 && !read)
					return null;

				var strLine = encoding.GetString(mem.ToArray());
				//Debug.WriteLine ("ReadLine: " + strLine);
				return strLine;
			}
		}

		/*
		internal static async Task<string> ReadLineAsync(this Stream stream, ref int? maxLength, Encoding encoding, char? termChar)
		{
			if (stream.CanTimeout)
				stream.ReadTimeout = 10000;

			var buffer = new byte[1];

			var maxLengthSpecified = maxLength > 0;
			using (var mem = new MemoryStream())
			{
				var read = false;
				byte b = 0;
				while (true)
				{
					byte b0 = b;
					int c = await stream.ReadAsync(buffer, 0, 1);
					if (c == 0)
						break;
					read = true;

					b = buffer[0];
					if (maxLengthSpecified)
					{
						maxLength--;

						if (mem.Length == 1 && b == termChar && b0 == termChar)
						{
							maxLength++;
							continue;
						}
					}

					if (b == 10 && mem.Length == 0)
						continue;
					else if (b == 13)
						break;

					mem.WriteByte(b);
					if (maxLengthSpecified && maxLength == 0)
						break;
				}

				if (mem.Length == 0 && !read)
					return null;

				var strLine = encoding.GetString(mem.ToArray());
				//Debug.WriteLine (strLine);
				return strLine;
			}
		}
		*/

		internal static string ReadToEnd(this Stream stream, int? maxLength, Encoding encoding)
		{
			if (stream.CanTimeout)
				stream.ReadTimeout = 10000;

			int read = 1;
			byte[] buffer = new byte[8192];
			using (var mem = new MemoryStream())
			{
				do
				{
					var length = maxLength == null ? buffer.Length : Math.Min(maxLength.Value - (int) mem.Length, buffer.Length);
					read = stream.Read(buffer, 0, length);
					mem.Write(buffer, 0, read);
					if (maxLength > 0 && mem.Length == maxLength) break;
				} while (read > 0);

				return encoding.GetString(mem.ToArray());
			}
		}
#endif

		internal static byte[] ReadToEnd(this Stream stream)
		{
			using (var mem = new MemoryStream())
			{
				stream.CopyTo(mem);
				return mem.ToArray();
			}
		}

		internal static string ReadToEnd(this Stream stream, Encoding encoding) => encoding.GetString(stream.ReadToEnd());

		internal static void TryDispose<T>(ref T obj) where T : class, IDisposable
		{
			try
			{
				obj?.Dispose();
			}
			catch (Exception) { }
			obj = null;
		}

		internal static string NotEmpty(this string input, params string[] others)
		{
			if (!string.IsNullOrEmpty(input))
				return input;

			foreach (var item in others)
			{
				if (!string.IsNullOrEmpty(item))
					return item;
			}
			return "";
		}

		internal static int ToInt(this string input) => int.TryParse(input, out int result) ? result : 0;

		internal static DateTime? ToNullDate(this string input)
		{
			input = NormalizeDate(input);
			if (DateTime.TryParse(input, _enUsCulture, DateTimeStyles.None, out DateTime result))
				return result;
			return null;
		}

		static readonly Regex rxTimeZoneName = new Regex(@"\s+\([a-z]+\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase); //Mon, 28 Feb 2005 19:26:34 -0500 (EST)
		static readonly Regex rxTimeZoneColon = new Regex(@"\s+(\+|\-)(\d{1,2})\D(\d{2})$", RegexOptions.Compiled | RegexOptions.IgnoreCase); //Mon, 28 Feb 2005 19:26:34 -0500 (EST)
		static readonly Regex rxTimeZoneMinutes = new Regex(@"([\+\-]?\d{1,2})(\d{2})$", RegexOptions.Compiled); //search can be strict because the format has already been normalized
		static readonly Regex rxNegativeHours = new Regex(@"(?<=\s)\-(?=\d{1,2}\:)", RegexOptions.Compiled);

		public static string NormalizeDate(string value)
		{
			value = rxTimeZoneName.Replace(value, "");
			value = rxTimeZoneColon.Replace(value, match => " " + match.Groups[1].Value + match.Groups[2].Value.PadLeft(2, '0') + match.Groups[3].Value);
			value = rxNegativeHours.Replace(value, "");
			var minutes = rxTimeZoneMinutes.Match(value);
			if (minutes.Groups[2].Value.ToInt() > 60)
			{ //even if there's no match, the value = 0
				return value.Substring(0, minutes.Index) + minutes.Groups[1].Value + "00";
			}
			return value;
		}

		internal static string GetRFC2060Date(this DateTime date)
		{
			//return date.ToString("dd-MMM-yyyy hh:mm:ss zz00", _enUsCulture);
			return date.ToString("dd-MMM-yyyy", _enUsCulture);
		}

		internal static string QuoteString(this string value)
		{
			return "\"" + value
				.Replace("&", "&-")
				.Replace("\\", "\\\\")
				.Replace("\r", "\\r")
				.Replace("\n", "\\n")
				.Replace("\"", "\\\"") + "\"";
		}

		internal static bool StartsWithWhiteSpace(this string line)
		{
			if (string.IsNullOrEmpty(line))
				return false;

			var chr = line[0];
			return chr == ' ' || chr == '\t' || chr == '\n' || chr == '\r';
		}

		public static string DecodeQuotedPrintable(string value, Encoding encoding = null)
		{
			encoding = encoding ?? Encoding.UTF8;

			if (value.IndexOf('_') > -1 && value.IndexOf(' ') == -1)
				value = value.Replace('_', ' ');

			var data = Encoding.ASCII.GetBytes(value);
			var eq = Convert.ToByte('=');
			var n = 0;

			for (int i = 0; i < data.Length; i++)
			{
				var b = data[i];

				if ((b == eq) && ((i + 2) < data.Length))
				{
					byte b1 = data[i + 1], b2 = data[i + 2];
					if (b1 == 10 || b1 == 13)
					{
						i++;
						if (b2 == 10 || b2 == 13)
							i++;
						continue;
					}

					if (byte.TryParse(value.Substring(i + 1, 2), NumberStyles.HexNumber, null, out b))
					{
						data[n] = (byte) b;
						n++;
						i += 2;
					}
					else
					{
						data[i] = eq;
						n++;
					}
				}
				else
				{
					data[n] = b;
					n++;
				}
			}

			value = encoding.GetString(data, 0, n);
			return value;
		}

		internal static string DecodeBase64(string data, Encoding encoding = null)
		{
			if (!IsValidBase64String(ref data))
				return data;
			var bytes = Convert.FromBase64String(data);
			return (encoding ?? Encoding.UTF8).GetString(bytes);
		}

		#region OpenPOP.NET
		internal static string DecodeWords(string encodedWords, Encoding @default = null)
		{
			if (string.IsNullOrEmpty(encodedWords))
				return "";

			string decodedWords = encodedWords;

			// Notice that RFC2231 redefines the BNF to
			// encoded-word := "=?" charset ["*" language] "?" encoded-text "?="
			// but no usage of this BNF have been spotted yet. It is here to
			// ease debugging if such a case is discovered.

			// This is the regex that should fit the BNF
			// RFC Says that NO WHITESPACE is allowed in this encoding, but there are examples
			// where whitespace is there, and therefore this regex allows for such.
			const string strRegEx = @"\=\?(?<Charset>\S+?)\?(?<Encoding>\w)\?(?<Content>.+?)\?\=";
			// \w	Matches any word character including underscore. Equivalent to "[A-Za-z0-9_]".
			// \S	Matches any nonwhite space character. Equivalent to "[^ \f\n\r\t\v]".
			// +?   non-gready equivalent to +
			// (?<NAME>REGEX) is a named group with name NAME and regular expression REGEX

			var matches = Regex.Matches(encodedWords, strRegEx);
			foreach (Match match in matches)
			{
				// If this match was not a success, we should not use it
				if (!match.Success)
					continue;

				string fullMatchValue = match.Value;

				string encodedText = match.Groups["Content"].Value;
				string encoding = match.Groups["Encoding"].Value;
				string charset = match.Groups["Charset"].Value;

				// Get the encoding which corrosponds to the character set
				Encoding charsetEncoding = ParseCharsetToEncoding(charset, @default);

				// Store decoded text here when done
				string decodedText;

				// Encoding may also be written in lowercase
				switch (encoding.ToUpperInvariant())
				{
					// RFC:
					// The "B" encoding is identical to the "BASE64" 
					// encoding defined by RFC 2045.
					// http://tools.ietf.org/html/rfc2045#section-6.8
					case "B":
						decodedText = DecodeBase64(encodedText, charsetEncoding);
						break;

					// RFC:
					// The "Q" encoding is similar to the "Quoted-Printable" content-
					// transfer-encoding defined in RFC 2045.
					// There are more details to this. Please check
					// http://tools.ietf.org/html/rfc2047#section-4.2
					// 
					case "Q":
						decodedText = DecodeQuotedPrintable(encodedText, charsetEncoding);
						break;

					default:
						throw new ArgumentException("The encoding " + encoding + " was not recognized");
				}

				// Repalce our encoded value with our decoded value
				decodedWords = decodedWords.Replace(fullMatchValue, decodedText);
			}

			return decodedWords;
		}

		//http://www.opensourcejavaphp.net/csharp/openpopdotnet/HeaderFieldParser.cs.html
		/// <param name="characterSet"></param>
		/// <param name="default"></param>
		/// Parse a character set into an encoding.
		/// </summary>
		/// <param name="characterSet">The character set to parse</param>
		/// <param name="@default">The character set to default to if it can't be parsed</param>
		/// <returns>An encoding which corresponds to the character set</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="characterSet"/> is <see langword="null"/></exception>
		public static Encoding ParseCharsetToEncoding(string characterSet, Encoding @default)
		{
			if (!string.IsNullOrEmpty(characterSet))
			{
				string charSetUpper = characterSet.ToUpperInvariant();
				if (charSetUpper.Contains("WINDOWS") || charSetUpper.Contains("CP"))
				{
					// It seems the character set contains an codepage value, which we should use to parse the encoding
					charSetUpper = charSetUpper.Replace("CP", ""); // Remove cp
					charSetUpper = charSetUpper.Replace("WINDOWS", ""); // Remove windows
					charSetUpper = charSetUpper.Replace("-", ""); // Remove - which could be used as cp-1554

					// Now we hope the only thing left in the characterSet is numbers.
					int codepageNumber = int.Parse(charSetUpper, System.Globalization.CultureInfo.InvariantCulture);

					var encoding = Encoding.GetEncoding(codepageNumber);
					if (encoding != null)
						return encoding;
					/*
					return Encoding.GetEncodings().Where(x => x.CodePage == codepageNumber)
						.Select(x => x.GetEncoding()).FirstOrDefault() ?? @default ?? Encoding.UTF8;
					*/
				}

				{
					var encoding = Encoding.GetEncoding(characterSet);
					if (encoding != null)
						return encoding;
				}
			}

			return @default ?? Encoding.UTF8;

			/*
			// It seems there is no codepage value in the characterSet. It must be a named encoding
			return Encoding.GetEncodings().Where(x => x.Name.Is(characterSet))
				.Select(x => x.GetEncoding()).FirstOrDefault() ?? @default ?? Encoding.UTF8;
			*/
		}
		#endregion

		#region IsValidBase64
		//stolen from http://stackoverflow.com/questions/3355407/validate-string-is-base64-format-using-regex
		const char Base64Padding = '=';

		static readonly HashSet<char> Base64Characters = new HashSet<char> {
			'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
			'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f',
			'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v',
			'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '+', '/'
		};

		internal static bool IsValidBase64String(ref string param, bool strictPadding = false)
		{
			if (param == null)
			{
				// null string is not Base64 
				return false;
			}

			// replace optional CR and LF characters
			param = param.Replace(" ", "").Replace("\r", "").Replace("\n", "");

			var lengthWPadding = param.Length;
			var missingPaddingLength = lengthWPadding % 4;
			if (missingPaddingLength != 0)
			{
				// Base64 string length should be multiple of 4
				if (strictPadding)
				{
					return false;
				}
				//add the minimum necessary padding
				if (missingPaddingLength > 2)
					missingPaddingLength %= 2;

				param += new string(Base64Padding, missingPaddingLength);
				lengthWPadding += missingPaddingLength;
				System.Diagnostics.Debug.Assert(lengthWPadding % 4 == 0);
			}

			if (lengthWPadding == 0)
			{
				// Base64 string should not be empty
				return false;
			}

			// replace pad chacters
			var paramWOPadding = param.TrimEnd(Base64Padding);
			var lengthWOPadding = paramWOPadding.Length;

			if ((lengthWPadding - lengthWOPadding) > 2)
			{
				// there should be no more than 2 pad characters
				return false;
			}

			foreach (char c in paramWOPadding)
			{
				if (!Base64Characters.Contains(c))
				{
					// string contains non-Base64 character
					return false;
				}
			}

			// nothing invalid found
			return true;
		}
		#endregion

		internal static VT Get<KT, VT>(this IDictionary<KT, VT> dictionary, KT key, VT defaultValue = default(VT))
		{
			if (dictionary == null)
				return defaultValue;

			if (dictionary.TryGetValue(key, out VT value))
				return value;

			return defaultValue;
		}

		internal static void Set<KT, VT>(this IDictionary<KT, VT> dictionary, KT key, VT value)
		{
			if (!dictionary.ContainsKey(key))
			{
				lock (dictionary)
				{
					if (!dictionary.ContainsKey(key))
					{
						dictionary.Add(key, value);
						return;
					}
				}
			}

			dictionary[key] = value;
		}

		public static void AddRange<T>(this ICollection<T> @this, IEnumerable<T> items)
		{
			foreach (var item in items)
				@this.Add(item);
		}

		public static bool AddRange<T>(this HashSet<T> @this, IEnumerable<T> items)
		{
			bool fChanged = false;

			foreach (var item in items)
				fChanged |= @this.Add(item);

			return fChanged;
		}

		public static bool RemoveRange<T>(this ICollection<T> @this, IEnumerable<T> items)
		{
			bool fChanged = false;

			foreach (var item in items)
				fChanged |= @this.Remove(item);

			return fChanged;
		}

		internal static void Fire<T>(this EventHandler<T> events, object sender, T args) where T : EventArgs
		{
			if (events == null)
				return;

			events(sender, args);
		}

		static readonly Regex _reEmail = new Regex(@"\s* "" \s* ([^""]*) \s* "" \s* \<  \s* ([^>]*) \s* \> \s*", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

		internal static MailAddress ToEmailAddress(this string input)
		{
			var match = _reEmail.Match(input);
			if (match?.Success == true)
			{
				try
				{
					var name = match.Groups[1].Value;
					var email = match.Groups[2].Value;

					return new MailAddress(email, name);
				}
				catch { }
			}

			try
			{
				return new MailAddress(input);
			}
			catch (Exception)
			{
				return null;
			}
		}

		internal static bool Is(this string input, string other)
		{
			return string.Equals(input, other, StringComparison.OrdinalIgnoreCase);
		}

		/*
		private static Dictionary<string, string> _TimeZoneAbbreviations = @"
ACDT +10:30
ACST +09:30
ACT +08
ADT -03
AEDT +11
AEST +10
AFT +04:30
AKDT -08
AKST -09
AMST +05
AMT +04
ART -03
AWDT +09
AWST +08
AZOST -01
AZT +04
BDT +08
BIOT +06
BIT -12
BOT -04
BRT -03
BTT +06
CAT +02
CCT +06:30
CDT -05
CEDT +02
CEST +02
CET +01
CHADT +13:45
CHAST +12:45
CIST -08
CKT -10
CLST -03
CLT -04
COST -04
COT -05
CST -06
CT +08
CVT -01
CXT +07
CHST +10
DFT +01
EAST -06
EAT +03
EDT -04
EEDT +03
EEST +03
EET +02
EST -05
FJT +12
FKST -03
FKT -04
GALT -06
GET +04
GFT -03
GILT +12
GIT -09
GMT 
GYT -04
HADT -09
HAST -10
HKT +08
HMT +05
HST -10
ICT +07
IDT +03
IRKT +08
IRST +03:30
JST +09
KRAT +07
KST +09
LHST +10:30
LINT +14
MAGT +11
MDT -06
MIT -09:30
MSD +04
MSK +03
MST -07
MUT +04
MYT +08
NDT -02:30
NFT +11:30
NPT +05:45
NST -03:30
NT -03:30
NZDT +13
NZST +12
OMST +06
PDT -07
PETT +12
PHOT +13
PKT +05
PST -08
RET +04
SAMT +04
SAST +02
SBT +11
SCT +04
SGT +08
SLT +05:30
TAHT -10
THA +07
UYST -02
UYT -03
VET -04:30
VLAT +10
WAT +01
WEDT +01
WEST +01
WET 
WST +08
YAKT +09
YEKT +05"
				.Trim().Split('\n').Select(line => line.Trim().Split(' ').Select(col => col.Trim()).Take(2).ToArray())
				.Where(x => x.Length == 2).ToDictionary(x => x[0], x => x[1]);

		internal static System.DateTime? ToNullDate(this string input, string format = null, DateTimeKind kind = DateTimeKind.Unspecified) {
				if (string.IsNullOrEmpty(input)) return null;
				if (input.Contains("T")) {
						foreach (var x in _TimeZoneAbbreviations) {
								input = input.Replace(x.Key, x.Value);
						}
				}

				System.DateTime num;
				if ((format != null && DateTime.TryParseExact(input, format, null, System.Globalization.DateTimeStyles.None, out num))
						|| (System.DateTime.TryParse(input, out  num))) {
						return DateTime.SpecifyKind(num, kind == DateTimeKind.Unspecified && input.Contains('Z') ? DateTimeKind.Utc : kind);
				} else {
						return null;
				}
		}
		 */
	}
}
