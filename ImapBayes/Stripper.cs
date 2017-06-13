using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImapBayes
{
	public class Stripper
	{
		readonly Regex _reStart;
		readonly Regex _reEnd;
		readonly Func<Match, IEnumerable<string>> _fnTokenize;

		public Stripper(Regex reStart, Regex reEnd, Func<Match, IEnumerable<string>> fnTokenize = null)
		{
			_reStart = reStart;
			_reEnd = reEnd;
			_fnTokenize = fnTokenize;
		}

		public Tuple<string, IEnumerable<string>> Analyze(string text)
		{
			int i = 0;

			var retained = new List<string>();
			var tokens = new List<string>();
			while (true)
			{
				var m = _reStart.Match(text, i);
				if (m == null || !m.Success)
				{
					retained.Add(text.Substring(i));
					break;
				}

				int start = m.Index;
				int end = m.Index + m.Length;

				retained.Add(text.Substring(i, start - i));

				if (_fnTokenize != null)
					tokens.AddRange(_fnTokenize(m));

				m = _reEnd.Match(text, end);
				if (m == null || !m.Success)
				{
					// No matching end - act as if the open
					// tag did not exist.
					retained.Add(text.Substring(end));
					break;
				}
				i = m.Index + m.Length;
			}

			return Tuple.Create<string, IEnumerable<string>>(retained.Join(""), tokens);
		}
	}
}
