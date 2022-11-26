using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImapBayes
{
	public static class Utils
	{
		public static IEnumerable<(long start, long end)> ToRanges(this IEnumerable<long> @this, int max = int.MaxValue)
		{
			var rg = @this.ToList();
			if (rg.Count == 0)
				yield break;

			rg.Sort();

			long start = rg[0];

			for (int i = 1; i < rg.Count; i++)
			{
				var value = rg[i];
				var end = rg[i - 1];
				if (value != end + 1 || end - start > max)
				{
					yield return (start, end);
					start = value;
				}
			}

			yield return (start, rg[rg.Count - 1]);
		}

		public static IEnumerable<string> FindAll(this Regex @this, string str)
		{
			return @this.Matches(str).Cast<Match>().Select(m => m.Value);
		}

		public static string Join(this IEnumerable<string> @this, string strSep = "")
		{
			StringBuilder sb = null;
			foreach (var str in @this)
			{
				if (sb == null)
					sb = new StringBuilder();
				else
					sb.Append(strSep);

				sb.Append(str);
			}

			if (sb == null)
				return "";
			return sb.ToString();
		}

		public static IEnumerable<TSource> Except<TSource>(this IEnumerable<TSource> @this, params TSource[] second)
		{
			return @this.Except((IEnumerable<TSource>)second);
		}

		public static IEnumerable<TSource> Append<TSource>(this IEnumerable<TSource> first, params TSource[] second)
		{
			foreach (var item in first)
				yield return item;

			foreach (var item in second)
				yield return item;
		}

		public static (long mantissa, int exponent) frexp(this double d)
		{
			// Translate the double into sign, exponent and mantissa.
			long bits = BitConverter.DoubleToInt64Bits(d);
			// Note that the shift is sign-extended, hence the test against -1 not 1
			bool negative = bits < 0;
			int exponent = (int)((bits >> 52) & 0x7ffL);
			long mantissa = bits & 0xfffffffffffffL;

			if (exponent == 0)
			{
				// Subnormal numbers; exponent is effectively one higher,
				// but there's no extra normalisation bit in the mantissa
				exponent++;
			}
			else
			{
				// Normal numbers; leave exponent as it is but add extra
				// bit to the front of the mantissa
				mantissa |= 1L << 52;
			}

			// Bias the exponent. It's actually biased by 1023, but we're
			// treating the mantissa as m.0 rather than 0.m, so we need
			// to subtract another 52 from it.
			exponent -= 1075;

			if (mantissa != 0)
			{
				/* Normalize */
				while ((mantissa & 1) == 0)
				{ /*  i.e., Mantissa is even */
					mantissa >>= 1;
					exponent++;
				}
			}

			if (negative)
				mantissa = -mantissa;

			return (mantissa, exponent);
		}

		public static double chi2Q(double x2, int v)
		{
			Debug.Assert((v & 1) == 0);
			// XXX If x2 is very large, exp(-m) will underflow to 0.
			var m = x2 / 2.0;
			var sum = Math.Exp(-m);
			Debug.Assert(!double.IsNaN(sum));
			var term = sum;
			for (var i = 1; i < v / 2; i++)
			{
				term *= m / i;
				sum += term;
			}
			Debug.Assert(!double.IsNaN(sum));
			// With small x2 and large v, accumulated roundoff error, plus error in
			// the platform exp(), can cause this to spill a few ULP above 1.0.  For
			// example, chi2Q(100, 300) on my box has sum == 1.0 + 2.0**-52 at this
			// point.  Returning a value even a teensy bit over 1.0 is no good.
			return Math.Min(sum, 1.0);
		}

		public static V GetOrAddSafe<T, V>(this ConcurrentDictionary<T, Lazy<V>> dictionary, T key, Func<T, V> valueFactory)
		{
			Lazy<V> lazy = dictionary.GetOrAdd(key, new Lazy<V>(() => valueFactory(key)));
			return lazy.Value;
		}

		public static V AddOrUpdateSafe<T, V>(this ConcurrentDictionary<T, Lazy<V>> dictionary, T key, Func<T, V> addValueFactory, Func<T, V, V> updateValueFactory)
		{
			Lazy<V> lazy = dictionary.AddOrUpdate(key,
				new Lazy<V>(() => addValueFactory(key)),
				(k, oldValue) => new Lazy<V>(() => updateValueFactory(k, oldValue.Value)));
			return lazy.Value;
		}

		/*
		public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<Tuple<TKey, TValue>> @this, IEqualityComparer<TKey> comparer = null)
		{
			var dict = new Dictionary<TKey, TValue>(comparer);
			foreach (var item in @this)
				dict.Add(item.Item1, item.Item2);

			return dict;
		}
		*/

		public static string ReplaceEntities(string str)
		{
			return _reEntities.Replace(str, match =>
			{
				if (!_mapEntities.TryGetValue(match.Groups[1].Value, out int code))
					return match.Value;

				return "" + (char)code;
			});
		}

		static readonly Dictionary<string, int> _mapEntities = new Dictionary<string, int>
		{
			["quot"] = 0x0022,
			["amp"] = 0x0026,
			["apos"] = 0x0027,
			["lt"] = 0x003C,
			["gt"] = 0x003E,
			["nbsp"] = 0x00A0,
			["iexcl"] = 0x00A1,
			["cent"] = 0x00A2,
			["pound"] = 0x00A3,
			["curren"] = 0x00A4,
			["yen"] = 0x00A5,
			["brvbar"] = 0x00A6,
			["sect"] = 0x00A7,
			["uml"] = 0x00A8,
			["copy"] = 0x00A9,
			["ordf"] = 0x00AA,
			["laquo"] = 0x00AB,
			["not"] = 0x00AC,
			["shy"] = 0x00AD,
			["reg"] = 0x00AE,
			["macr"] = 0x00AF,
			["deg"] = 0x00B0,
			["plusmn"] = 0x00B1,
			["sup2"] = 0x00B2,
			["sup3"] = 0x00B3,
			["acute"] = 0x00B4,
			["micro"] = 0x00B5,
			["para"] = 0x00B6,
			["middot"] = 0x00B7,
			["cedil"] = 0x00B8,
			["sup1"] = 0x00B9,
			["ordm"] = 0x00BA,
			["raquo"] = 0x00BB,
			["frac14"] = 0x00BC,
			["frac12"] = 0x00BD,
			["frac34"] = 0x00BE,
			["iquest"] = 0x00BF,
			["Agrave"] = 0x00C0,
			["Aacute"] = 0x00C1,
			["Acirc"] = 0x00C2,
			["Atilde"] = 0x00C3,
			["Auml"] = 0x00C4,
			["Aring"] = 0x00C5,
			["AElig"] = 0x00C6,
			["Ccedil"] = 0x00C7,
			["Egrave"] = 0x00C8,
			["Eacute"] = 0x00C9,
			["Ecirc"] = 0x00CA,
			["Euml"] = 0x00CB,
			["Igrave"] = 0x00CC,
			["Iacute"] = 0x00CD,
			["Icirc"] = 0x00CE,
			["Iuml"] = 0x00CF,
			["ETH"] = 0x00D0,
			["Ntilde"] = 0x00D1,
			["Ograve"] = 0x00D2,
			["Oacute"] = 0x00D3,
			["Ocirc"] = 0x00D4,
			["Otilde"] = 0x00D5,
			["Ouml"] = 0x00D6,
			["times"] = 0x00D7,
			["Oslash"] = 0x00D8,
			["Ugrave"] = 0x00D9,
			["Uacute"] = 0x00DA,
			["Ucirc"] = 0x00DB,
			["Uuml"] = 0x00DC,
			["Yacute"] = 0x00DD,
			["THORN"] = 0x00DE,
			["szlig"] = 0x00DF,
			["agrave"] = 0x00E0,
			["aacute"] = 0x00E1,
			["acirc"] = 0x00E2,
			["atilde"] = 0x00E3,
			["auml"] = 0x00E4,
			["aring"] = 0x00E5,
			["aelig"] = 0x00E6,
			["ccedil"] = 0x00E7,
			["egrave"] = 0x00E8,
			["eacute"] = 0x00E9,
			["ecirc"] = 0x00EA,
			["euml"] = 0x00EB,
			["igrave"] = 0x00EC,
			["iacute"] = 0x00ED,
			["icirc"] = 0x00EE,
			["iuml"] = 0x00EF,
			["eth"] = 0x00F0,
			["ntilde"] = 0x00F1,
			["ograve"] = 0x00F2,
			["oacute"] = 0x00F3,
			["ocirc"] = 0x00F4,
			["otilde"] = 0x00F5,
			["ouml"] = 0x00F6,
			["divide"] = 0x00F7,
			["oslash"] = 0x00F8,
			["ugrave"] = 0x00F9,
			["uacute"] = 0x00FA,
			["ucirc"] = 0x00FB,
			["uuml"] = 0x00FC,
			["yacute"] = 0x00FD,
			["thorn"] = 0x00FE,
			["yuml"] = 0x00FF,
			["OElig"] = 0x0152,
			["oelig"] = 0x0153,
			["Scaron"] = 0x0160,
			["scaron"] = 0x0161,
			["Yuml"] = 0x0178,
			["fnof"] = 0x0192,
			["circ"] = 0x02C6,
			["tilde"] = 0x02DC,
			["Alpha"] = 0x0391,
			["Beta"] = 0x0392,
			["Gamma"] = 0x0393,
			["Delta"] = 0x0394,
			["Epsilon"] = 0x0395,
			["Zeta"] = 0x0396,
			["Eta"] = 0x0397,
			["Theta"] = 0x0398,
			["Iota"] = 0x0399,
			["Kappa"] = 0x039A,
			["Lambda"] = 0x039B,
			["Mu"] = 0x039C,
			["Nu"] = 0x039D,
			["Xi"] = 0x039E,
			["Omicron"] = 0x039F,
			["Pi"] = 0x03A0,
			["Rho"] = 0x03A1,
			["Sigma"] = 0x03A3,
			["Tau"] = 0x03A4,
			["Upsilon"] = 0x03A5,
			["Phi"] = 0x03A6,
			["Chi"] = 0x03A7,
			["Psi"] = 0x03A8,
			["Omega"] = 0x03A9,
			["alpha"] = 0x03B1,
			["beta"] = 0x03B2,
			["gamma"] = 0x03B3,
			["delta"] = 0x03B4,
			["epsilon"] = 0x03B5,
			["zeta"] = 0x03B6,
			["eta"] = 0x03B7,
			["theta"] = 0x03B8,
			["iota"] = 0x03B9,
			["kappa"] = 0x03BA,
			["lambda"] = 0x03BB,
			["mu"] = 0x03BC,
			["nu"] = 0x03BD,
			["xi"] = 0x03BE,
			["omicron"] = 0x03BF,
			["pi"] = 0x03C0,
			["rho"] = 0x03C1,
			["sigmaf"] = 0x03C2,
			["sigma"] = 0x03C3,
			["tau"] = 0x03C4,
			["upsilon"] = 0x03C5,
			["phi"] = 0x03C6,
			["chi"] = 0x03C7,
			["psi"] = 0x03C8,
			["omega"] = 0x03C9,
			["thetasym"] = 0x03D1,
			["upsih"] = 0x03D2,
			["piv"] = 0x03D6,
			["ensp"] = 0x2002,
			["emsp"] = 0x2003,
			["thinsp"] = 0x2009,
			["zwnj"] = 0x200C,
			["zwj"] = 0x200D,
			["lrm"] = 0x200E,
			["rlm"] = 0x200F,
			["ndash"] = 0x2013,
			["mdash"] = 0x2014,
			["lsquo"] = 0x2018,
			["rsquo"] = 0x2019,
			["sbquo"] = 0x201A,
			["ldquo"] = 0x201C,
			["rdquo"] = 0x201D,
			["bdquo"] = 0x201E,
			["dagger"] = 0x2020,
			["Dagger"] = 0x2021,
			["bull"] = 0x2022,
			["hellip"] = 0x2026,
			["permil"] = 0x2030,
			["prime"] = 0x2032,
			["Prime"] = 0x2033,
			["lsaquo"] = 0x2039,
			["rsaquo"] = 0x203A,
			["oline"] = 0x203E,
			["frasl"] = 0x2044,
			["euro"] = 0x20AC,
			["image"] = 0x2111,
			["weierp"] = 0x2118,
			["real"] = 0x211C,
			["trade"] = 0x2122,
			["alefsym"] = 0x2135,
			["larr"] = 0x2190,
			["uarr"] = 0x2191,
			["rarr"] = 0x2192,
			["darr"] = 0x2193,
			["harr"] = 0x2194,
			["crarr"] = 0x21B5,
			["lArr"] = 0x21D0,
			["uArr"] = 0x21D1,
			["rArr"] = 0x21D2,
			["dArr"] = 0x21D3,
			["hArr"] = 0x21D4,
			["forall"] = 0x2200,
			["part"] = 0x2202,
			["exist"] = 0x2203,
			["empty"] = 0x2205,
			["nabla"] = 0x2207,
			["isin"] = 0x2208,
			["notin"] = 0x2209,
			["ni"] = 0x220B,
			["prod"] = 0x220F,
			["sum"] = 0x2211,
			["minus"] = 0x2212,
			["lowast"] = 0x2217,
			["radic"] = 0x221A,
			["prop"] = 0x221D,
			["infin"] = 0x221E,
			["ang"] = 0x2220,
			["and"] = 0x2227,
			["or"] = 0x2228,
			["cap"] = 0x2229,
			["cup"] = 0x222A,
			["int"] = 0x222B,
			["there4"] = 0x2234,
			["sim"] = 0x223C,
			["cong"] = 0x2245,
			["asymp"] = 0x2248,
			["ne"] = 0x2260,
			["equiv"] = 0x2261,
			["le"] = 0x2264,
			["ge"] = 0x2265,
			["sub"] = 0x2282,
			["sup"] = 0x2283,
			["nsub"] = 0x2284,
			["sube"] = 0x2286,
			["supe"] = 0x2287,
			["oplus"] = 0x2295,
			["otimes"] = 0x2297,
			["perp"] = 0x22A5,
			["sdot"] = 0x22C5,
			["lceil"] = 0x2308,
			["rceil"] = 0x2309,
			["lfloor"] = 0x230A,
			["rfloor"] = 0x230B,
			["lang"] = 0x2329,
			["rang"] = 0x232A,
			["loz"] = 0x25CA,
			["spades"] = 0x2660,
			["clubs"] = 0x2663,
			["hearts"] = 0x2665,
			["diams"] = 0x2666
		};

		static readonly Regex _reEntities = new Regex(" & ( " + _mapEntities.Keys.Join("|") + " ) ;", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
	}

	public class ConsoleTraceListener : TraceListener
	{
		bool _fEOL = true;

		void WritePrefix()
		{
			if (_fEOL)
			{
				//Console.Write($"{DateTime.UtcNow.ToString("u")} ({System.Threading.Thread.CurrentThread.ManagedThreadId}) : ");
				_fEOL = false;
			}
		}

		public override void Write(string message)
		{
			WritePrefix();
			Console.Write(message);

			_fEOL |= message.EndsWith("\n");
		}

		public override void WriteLine(string message)
		{
			WritePrefix();
			Console.WriteLine(message);
			_fEOL = true;
		}
	}
}