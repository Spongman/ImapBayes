using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AE.Net.Mail
{
	public static class ViewCollectionExtensions
	{
		/// <summary>
		/// Find views matching a specific content-type.
		/// </summary>
		/// <param name="this"></param>
		/// <param name="contentType">The content-type to search for; such as "text/html"</param>
		/// <returns></returns>
		public static IEnumerable<Attachment> OfType(this ICollection<Attachment> @this, string contentType)
		{
			contentType = (contentType ?? "").ToLower();
			return @this.OfType(x => x.Is(contentType));
		}

		/// <summary>
		/// Find views where the content-type matches a condition
		/// </summary>
		/// <param name="this"></param>
		/// <param name="predicate"></param>
		/// <returns></returns>
		public static IEnumerable<Attachment> OfType(this ICollection<Attachment> @this, Func<string, bool> predicate)
		{
			return @this.Where(x => predicate((x.ContentType ?? "").Trim()));
		}

		public static Attachment GetHtmlView(this ICollection<Attachment> @this)
		{
			return @this.OfType("text/html").FirstOrDefault() ?? @this.OfType(ct => ct.Contains("html")).FirstOrDefault();
		}

		public static Attachment GetTextView(this ICollection<Attachment> @this)
		{
			return @this.OfType("text/plain").FirstOrDefault() ?? @this.OfType(ct => ct.StartsWith("text/")).FirstOrDefault();
		}
	}
}
