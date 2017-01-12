using System;
using System.IO;

namespace OJS.Common.Extensions
{
    public class IOHelpers
    {
        public static string GetTempPath(string prefix = "ojs")
        {
            var dirName = Path.Combine(Path.GetTempPath(), "ojs-files");
            if (Directory.Exists(dirName) == false)
            {
                Directory.CreateDirectory(dirName);
            }

            return Path.Combine(dirName, $"{prefix}-{Guid.NewGuid()}.tmp");
        }
    }
}
