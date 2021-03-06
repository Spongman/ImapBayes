﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AE.Net.Mail
{
	public struct HeaderValue
	{
		readonly string _rawValue;
		readonly SafeDictionary<string, string> _values;

		public HeaderValue(string value)
			: this()
		{
			_values = new SafeDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			_rawValue = value ?? (value = "");
			_values[""] = RawValue;

			var semicolon = value.IndexOf(';');
			if (semicolon > 0)
			{
				_values[""] = value.Substring(0, semicolon).Trim();
				value = value.Substring(semicolon).Trim();
				ParseValues(_values, value);
			}
		}

		public string Value => this[""] ?? "";
		public string RawValue => _rawValue ?? "";
		public string this[string name]
		{
			get => _values.Get(name, "");
			internal set => _values.Set(name, value);
		}

		public static void ParseValues(IDictionary<string, string> result, string header)
		{
			while (header.Length > 0)
			{
				var eq = header.IndexOf('=');
				if (eq < 0)
					eq = header.Length;

				var name = header.Substring(0, eq).Trim().Trim(new[] { ';', ',' }).Trim();

				var value = header = header.Substring(Math.Min(header.Length, eq + 1)).Trim();

				if (value.StartsWith("\""))
					ProcessValue(1, ref header, ref value, '"');
				else if (value.StartsWith("'"))
					ProcessValue(1, ref header, ref value, '\'');
				else
					ProcessValue(0, ref header, ref value, ' ', ',', ';');

				result.Set(name, value);
			}
		}

		static void ProcessValue(int skip, ref string header, ref string value, params char[] lookFor)
		{
			var quote = value.IndexOfAny(lookFor, skip);
			if (quote < 0)
				quote = value.Length;

			header = header.Substring(Math.Min(quote + 1, header.Length));
			value = value.Substring(skip, quote - skip);
		}

		public override string ToString()
		{
			var props = _values.Where(x => !string.IsNullOrEmpty(x.Key)).Select(x => x.Key + "=" + x.Value);
			return Value + (props.Any() ? ("; " + string.Join(", ", props)) : null);
		}
	}
}
