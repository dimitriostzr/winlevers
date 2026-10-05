using System.Text;

namespace WinLevers.Presentation.Reporting;

/// <summary>Writes rows the way every spreadsheet and CSV reader expects.</summary>
/// <remarks>
/// RFC 4180: a field holding a comma, a quote or a line break is quoted, a
/// quote inside it is doubled, and rows end in CRLF. Everything else is
/// written bare. One implementation, so the status report, the grid export
/// and the History exports cannot disagree about quoting.
/// </remarks>
public static class Csv
{
    /// <summary>Appends one row.</summary>
    public static void Row(StringBuilder sb, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            var field = fields[i];

            if (field.IndexOfAny([',', '"', '\r', '\n']) >= 0)
            {
                sb.Append('"').Append(field.Replace("\"", "\"\"")).Append('"');
            }
            else
            {
                sb.Append(field);
            }
        }

        sb.Append("\r\n");
    }
}
