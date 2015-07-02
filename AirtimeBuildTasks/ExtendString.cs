using System;
using System.Collections.Generic;
using System.IO;
using System.Text;


namespace AirtimeBuildTasks
{

	public static class ExtendString
	{

		public static string NamedFormat(this string format, IDictionary<string, object> source, Func<string, string> missingItemFunc = null)
		{
			if (format == null) {
				throw new ArgumentNullException(nameof(format));
			}

			if (missingItemFunc == null) {
				missingItemFunc = expression => "{" + expression + "}";
			}

			var result = new StringBuilder(format.Length * 2);

			using (var reader = new StringReader(format)) {
				long position = 0;
				var expression = new StringBuilder();

				var state = State.OutsideExpression;
				do {
					int c;
					switch (state) {
						case State.OutsideExpression:
							c = reader.Read();
							position++;
							switch (c) {
								case -1:
									state = State.End;
									break;
								case '{':
									state = State.OnOpenBracket;
									break;
								case '}':
									state = State.OnCloseBracket;
									break;
								default:
									result.Append((char)c);
									break;
							}
							break;
						case State.OnOpenBracket:
							c = reader.Read();
							position++;
							switch (c) {
								case -1:
									throw new FormatException($"One of the identified items was in an invalid format at character position {position}");
								case '{':
									result.Append('{');
									state = State.OutsideExpression;
									break;
								case '}':
									result.Append(OutExpression(source, "", missingItemFunc));
									expression.Length = 0;
									state = State.OutsideExpression;
									break;
								default:
									expression.Append((char)c);
									state = State.InsideExpression;
									break;
							}
							break;
						case State.InsideExpression:
							c = reader.Read();
							position++;
							switch (c) {
								case -1:
									throw new FormatException($"One of the identified items was in an invalid format at character position {position}");
								case '}':
									result.Append(OutExpression(source, expression.ToString(), missingItemFunc));
									expression.Length = 0;
									state = State.OutsideExpression;
									break;
								default:
									expression.Append((char)c);
									break;
							}
							break;
						case State.OnCloseBracket:
							c = reader.Read();
							position++;
							switch (c) {
								case '}':
									result.Append('}');
									state = State.OutsideExpression;
									break;
								default:
									throw new FormatException($"One of the identified items was in an invalid format at character position {position}");
							}
							break;
						default:
							throw new InvalidOperationException($"Invalid state at character position {position}");
					}
				} while (state != State.End);
			}

			return result.ToString();
		}


		private static string OutExpression(IDictionary<string, object> source, string expression, Func<string, string> missingItemFunc)
		{
			if (String.IsNullOrEmpty(expression)) {
				return missingItemFunc(expression);
			}

			string format = "";
			int colonIndex = expression.IndexOf(':');
			if (colonIndex > 0) {
				format = expression.Substring(colonIndex + 1);
				expression = expression.Substring(0, colonIndex);
			}

			try {
				return String.IsNullOrEmpty(format)
					? (source[expression] ?? "").ToString()
					: String.Format("{0:" + format + "}", source[expression]);

			} catch (KeyNotFoundException) {
				return missingItemFunc(expression);
			}
		}


		private enum State
		{
			OutsideExpression,
			OnOpenBracket,
			InsideExpression,
			OnCloseBracket,
			End
		}

	}

}
