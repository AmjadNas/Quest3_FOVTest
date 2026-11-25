using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace FOVTest
{
    /// <summary>Writes the log with the reference file's layout: indented header, one compact line per
    /// trial, compact summary, UTF-8 with BOM, LF line ends.</summary>
    public static class LogWriter
    {
        public static string ToJson(LogHeader header, IEnumerable<TrialLog> history, SummaryLog summary, long unixEnd)
        {
            var head = JsonConvert.SerializeObject(header, Formatting.Indented).Replace("\r\n", "\n");
            var sb = new StringBuilder();
            sb.Append(head, 0, head.LastIndexOf('\n')).Append(",\n\"History\": [\n");
            var lines = history.Select(TrialLine).ToList();
            if (lines.Count > 0) sb.Append(string.Join(",\n", lines)).Append('\n');
            sb.Append("],\n\"Summary\": ").Append(JsonConvert.SerializeObject(summary));
            sb.Append(",\n\"UnixTimeEnd\": ").Append(unixEnd.ToString(CultureInfo.InvariantCulture)).Append("\n}\n");
            return sb.ToString();
        }

        public static string TrialLine(TrialLog t) => JsonConvert.SerializeObject(t);

        /// <summary>Writes to a temporary file first, so a crash mid-write cannot destroy an earlier save.</summary>
        public static void Save(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }
}
