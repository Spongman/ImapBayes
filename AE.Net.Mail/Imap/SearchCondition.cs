using System;
using System.Linq;
using System.Collections.Generic;

namespace AE.Net.Mail
{
	public abstract class SearchCondition
	{
		public static readonly SearchCondition All = new SimpleSearchCondition(Fields.All);
		public static SearchCondition Text(string text) => new ValueSearchCondition(Fields.Text, text);
		public static SearchCondition BCC(string text) => new ValueSearchCondition(Fields.BCC, text);
		public static SearchCondition Body(string text) => new ValueSearchCondition(Fields.Body, text);
		public static SearchCondition Cc(string text) => new ValueSearchCondition(Fields.Cc, text);
		public static SearchCondition From(string text) => new ValueSearchCondition(Fields.From, text);
		public static SearchCondition Header(string name, string text) => new ValueSearchCondition(Fields.Header, name + " " + text.QuoteString());
		public static SearchCondition Keyword(string text) => new ValueSearchCondition(Fields.Keyword, text);
		public static SearchCondition Larger(long size) => new ValueSearchCondition(Fields.Larger, size);
		public static SearchCondition Smaller(long size) => new ValueSearchCondition(Fields.Smaller, size);
		public static SearchCondition SentBefore(DateTime date) => new ValueSearchCondition(Fields.SentBefore, date);
		public static SearchCondition SentOn(DateTime date) => new ValueSearchCondition(Fields.SentOn, date);
		public static SearchCondition SentSince(DateTime date) => new ValueSearchCondition(Fields.SentSince, date);
		public static SearchCondition Subject(string text) => new ValueSearchCondition(Fields.Subject, text);

		// internal date
		public static SearchCondition Before(DateTime date) => new ValueSearchCondition(Fields.Before, date);
		public static SearchCondition On(DateTime date) => new ValueSearchCondition(Fields.On, date);
		public static SearchCondition Since(DateTime date) => new ValueSearchCondition(Fields.Since, date);

		public static SearchCondition To(string text) => new ValueSearchCondition(Fields.To, text);
		public static SearchCondition UID(string ids) => new ValueSearchCondition(Fields.UID, ids);
		public static SearchCondition Unkeyword(string text) => new ValueSearchCondition(Fields.Unkeyword, text);
		public static readonly SearchCondition Answered = new SimpleSearchCondition(Fields.Answered);
		public static readonly SearchCondition Deleted = new SimpleSearchCondition(Fields.Deleted);
		public static readonly SearchCondition Draft = new SimpleSearchCondition(Fields.Draft);
		public static readonly SearchCondition Flagged = new SimpleSearchCondition(Fields.Flagged);
		public static readonly SearchCondition New = new SimpleSearchCondition(Fields.New);
		public static readonly SearchCondition Old = new SimpleSearchCondition(Fields.Old);
		public static readonly SearchCondition Recent = new SimpleSearchCondition(Fields.Recent);
		public static readonly SearchCondition Seen = new SimpleSearchCondition(Fields.Seen);
		public static readonly SearchCondition Unanswered = new SimpleSearchCondition(Fields.Unanswered);
		public static readonly SearchCondition Undeleted = new SimpleSearchCondition(Fields.Undeleted);
		public static readonly SearchCondition Undraft = new SimpleSearchCondition(Fields.Undraft);
		public static readonly SearchCondition Unflagged = new SimpleSearchCondition(Fields.Unflagged);
		public static readonly SearchCondition Unseen = new SimpleSearchCondition(Fields.Unseen);

		public enum Fields
		{
			BCC, Before, Body, Cc, From, Header, Keyword,
			Larger, On, SentBefore, SentOn, SentSince, Since, Smaller, Subject,
			Text, To, UID, Unkeyword, All, Answered, Deleted, Draft, Flagged,
			New, Old, Recent, Seen, Unanswered, Undeleted, Undraft, Unflagged, Unseen,
		}

		internal virtual SearchCondition And(params SearchCondition[] others) => new AndSearchCondition(new[] { this }.Concat(others).ToArray());

		internal virtual SearchCondition Not() => new NotSearchCondition(this);

		internal virtual SearchCondition Or(SearchCondition other) => new OrSearchCondition(this, other);

		public static bool operator true(SearchCondition cond) => false;

		public static bool operator false(SearchCondition cond) => false;

		public static SearchCondition operator &(SearchCondition x, SearchCondition y) => x.And(y);

		public static SearchCondition operator |(SearchCondition x, SearchCondition y) => x.Or(y);

		public static SearchCondition operator !(SearchCondition x) => x.Not();
	}

	public class SimpleSearchCondition : SearchCondition
	{
		public virtual Fields Field { get; set; }

		internal SimpleSearchCondition(Fields field)
		{
			this.Field = field;
		}

		public override string ToString() => Field.ToString().ToUpper();
	}

	public class ValueSearchCondition : SimpleSearchCondition
	{
		public virtual object Value { get; set; }

		internal ValueSearchCondition(Fields field, object Value)
			: base(field)
		{
			this.Value = Value;
		}

		public override string ToString()
		{
			var builder = new System.Text.StringBuilder(base.ToString());

			var value = Value;
			if (value != null)
			{
				if (value is DateTime)
				{
					value = ((DateTime) value).GetRFC2060Date().QuoteString();
				}
				else
				{
					switch (Field)
					{
						case Fields.BCC:
						case Fields.Body:
						case Fields.From:
						case Fields.Subject:
						case Fields.Text:
						case Fields.To:
							value = Convert.ToString(value).QuoteString();
							break;
					}
				}

				builder.Append(" ");
				builder.Append(value);
			}

			return builder.ToString();
		}
	}

	public class NotSearchCondition : SearchCondition
	{
		readonly SearchCondition _other;

		public NotSearchCondition(SearchCondition other)
		{
			_other = other;
		}

		public override string ToString()
		{
			var other = _other.ToString();
			if (_other is AndSearchCondition)
				other = "(" + other + ")";

			return "NOT " + _other;
		}

		internal override SearchCondition Not() => _other;
	}

	public class AndSearchCondition : SearchCondition
	{
		readonly SearchCondition[] _others;
		public AndSearchCondition(params SearchCondition[] others)
		{
			_others = others;
		}

		public override string ToString() => string.Join(" ", (object[]) _others);

		internal override SearchCondition And(params SearchCondition[] others) => new AndSearchCondition(_others.Concat(others).ToArray());
	}

	public class OrSearchCondition : SearchCondition
	{
		readonly SearchCondition _left;
		readonly SearchCondition _right;

		public OrSearchCondition(SearchCondition left, SearchCondition right)
		{
			_left = left;
			_right = right;
		}

		public override string ToString()
		{
			var left = _left.ToString();
			if (_left is AndSearchCondition)
				left = "(" + left + ")";

			var right = _right.ToString();
			if (_right is AndSearchCondition)
				right = "(" + right + ")";

			return "OR " + left + " " + right;
		}
	}
}
