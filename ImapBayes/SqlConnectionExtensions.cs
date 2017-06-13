using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
//using System.Data.SqlClient;
using System.Diagnostics.Contracts;

namespace ImapBayes
{
	public static class SqlConnectionExtensions
	{
		public static IDbDataParameter CreateParameter(this DbProviderFactory @this, string parameterName, DbType type)
		{
			var param = @this.CreateParameter();
			param.ParameterName = parameterName;
			param.DbType = type;
			return param;
		}

		public static IDbDataParameter CreateParameter(this DbProviderFactory @this, string parameterName, object value)
		{
			var param = @this.CreateParameter();
			param.ParameterName = parameterName;
			param.Value = value;
			return param;
		}

		public static IDbCommand GetCommand (this IDbConnection @this, string strCmd, params IDbDataParameter [] rgParams)
		{
			Contract.Requires (rgParams != null);
			return GetCommand (@this, strCmd, CommandType.StoredProcedure, rgParams);
		}

		public static IDbCommand GetCommand (this IDbConnection @this, string strCmd, CommandType type, params IDbDataParameter [] rgParams)
		{
			Contract.Requires (@this != null);
			Contract.Requires (rgParams != null);
			Contract.Ensures (Contract.Result<IDbCommand> () != null);

			IDbCommand cmd = @this.CreateCommand ();
			Contract.Assume (cmd != null);
			cmd.CommandTimeout = 180;
			cmd.CommandText = strCmd;
			cmd.CommandType = type;
			foreach (var param in rgParams)
				cmd.Parameters.Add(param);

			return cmd;
		}

		public static IDataReader ExecuteReader (this IDbConnection @this, string strCmd, params IDbDataParameter [] rgParams)
		{
			using (IDbCommand cmd = @this.GetCommand (strCmd, rgParams))
				return cmd.ExecuteReader ();
		}

		public static IDataReader ExecuteReader(this IDbConnection @this, string strCmd, CommandType type, params IDbDataParameter[] rgParams)
		{
			using (IDbCommand cmd = @this.GetCommand(strCmd, type, rgParams))
				return cmd.ExecuteReader();
		}

		public static IEnumerable<IDataReader> ExecuteRows(this IDbConnection @this, string strCmd, params IDbDataParameter[] rgParams)
		{
			using (IDbCommand cmd = @this.GetCommand(strCmd, rgParams))
				return cmd.ExecuteRows();
		}

		public static IEnumerable<IDataReader> ExecuteRows(this IDbConnection @this, string strCmd, CommandType type, params IDbDataParameter[] rgParams)
		{
			using (IDbCommand cmd = @this.GetCommand(strCmd, type, rgParams))
				return cmd.ExecuteRows();
		}

		public static void ExecuteNonQuery (this IDbConnection @this, string strCmd, CommandType type, params IDbDataParameter [] rgParams)
		{
			using (IDbCommand cmd = @this.GetCommand (strCmd, type, rgParams))
				cmd.ExecuteNonQuery ();
		}

		public static T ExecuteScalar<T> (this IDbConnection @this, string strCmd, CommandType type, params IDbDataParameter [] rgParams)
		{
			using (IDbCommand cmd = @this.GetCommand (strCmd, type, rgParams))
				return (T) cmd.ExecuteScalar ();
		}

		public static T ExecuteReturn<T> (this IDbConnection @this, string strCmd, CommandType type, params IDbDataParameter [] rgParams)
		{
			using (IDbCommand cmd = @this.GetCommand (strCmd, type, rgParams))
				return cmd.ExecuteReturn<T> ();
		}

		public static IDataReader ExecuteReaderReturn (this IDbConnection @this, string strCmd, CommandType type, out IDbDataParameter paramReturn, params IDbDataParameter [] rgParams)
		{
			using (IDbCommand cmd = @this.GetCommand (strCmd, type, rgParams))
			{
				paramReturn = cmd.CreateParameter();
				paramReturn.Direction = ParameterDirection.ReturnValue;
				cmd.Parameters.Add (paramReturn);
				return cmd.ExecuteReader ();
			}
		}

		public static void ExecuteNonQuery (this IDbConnection @this, string strCmd, params IDbDataParameter [] rgParams)
		{
			@this.ExecuteNonQuery (strCmd, CommandType.StoredProcedure, rgParams);
		}

		public static T ExecuteScalar<T> (this IDbConnection @this, string strCmd, params IDbDataParameter [] rgParams)
		{
			return @this.ExecuteScalar<T> (strCmd, CommandType.StoredProcedure, rgParams);
		}

		public static T ExecuteReturn<T> (this IDbConnection @this, string strCmd, params IDbDataParameter [] rgParams)
		{
			return @this.ExecuteReturn<T> (strCmd, CommandType.StoredProcedure, rgParams);
		}

		public static IDataReader ExecuteReaderReturn(this IDbConnection @this, string strCmd, out IDbDataParameter paramReturn, params IDbDataParameter[] rgParams)
		{
			return @this.ExecuteReaderReturn (strCmd, CommandType.StoredProcedure, out paramReturn, rgParams);
		}

		public static IEnumerable<IDataReader> ExecuteRows(this IDbCommand @this)
		{
			using (var reader = @this.ExecuteReader())
			{
				while (reader.Read())
					yield return reader;
			}
		}

		public static T ExecuteReturn<T> (this IDbCommand @this)
		{
			Contract.Requires (@this != null);
			Contract.Requires (@this.Parameters != null);

			IDbDataParameter param = @this.CreateParameter ();
			param.Direction = ParameterDirection.ReturnValue;
			@this.Parameters.Add (param);
			@this.ExecuteNonQuery ();

			Contract.Assume (param.Value != null);
			return (T) param.Value;
		}

		public static T ExecuteScalar<T> (this IDbCommand @this)
		{
			object value = @this.ExecuteScalar ();
			return (T) value;
		}

		public static T? GetNullable<T>(this IDataReader @this, int i)
			where T : struct
		{
			if (@this.IsDBNull(i))
				return default(T?);

			return (T?) @this.GetValue(i);
		}

		public static T? GetNullable<T>(this IDataReader @this, string s)
			where T : struct
		{
			return @this.GetNullable<T>(@this.GetOrdinal(s));
		}

		public static bool GetBoolean(this IDataReader @this, string s) { return @this.GetBoolean(@this.GetOrdinal(s)); }
		public static byte GetByte(this IDataReader @this, string s) { return @this.GetByte(@this.GetOrdinal(s)); }
		public static char GetChar(this IDataReader @this, string s) { return @this.GetChar(@this.GetOrdinal(s)); }
		public static DateTime GetDateTime(this IDataReader @this, string s) { return @this.GetDateTime(@this.GetOrdinal(s)); }
		public static decimal GetDecimal(this IDataReader @this, string s) { return @this.GetDecimal(@this.GetOrdinal(s)); }
		public static double GetDouble(this IDataReader @this, string s) { return @this.GetDouble(@this.GetOrdinal(s)); }
		public static float GetFloat(this IDataReader @this, string s) { return @this.GetFloat(@this.GetOrdinal(s)); }
		public static Guid GetGuid(this IDataReader @this, string s) { return @this.GetGuid(@this.GetOrdinal(s)); }
		public static short GetInt16(this IDataReader @this, string s) { return @this.GetInt16(@this.GetOrdinal(s)); }
		public static int GetInt32(this IDataReader @this, string s) { return @this.GetInt32(@this.GetOrdinal(s)); }
		public static long GetInt64(this IDataReader @this, string s) { return @this.GetInt64(@this.GetOrdinal(s)); }
		public static string GetString(this IDataReader @this, string s) { return @this.GetString(@this.GetOrdinal(s)); }
		public static object GetValue(this IDataReader @this, string s) { return @this.GetValue(@this.GetOrdinal(s)); }
		public static bool IsDBNull(this IDataReader @this, string s) { return @this.IsDBNull(@this.GetOrdinal(s)); }
	}
}
