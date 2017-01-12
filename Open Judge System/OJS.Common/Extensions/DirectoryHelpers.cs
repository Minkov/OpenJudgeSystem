using System;

namespace OJS.Common.Extensions
{
    using System.IO;

    public static class DirectoryHelpers
    {
        public static string CreateTempDirectory()
        {
            var dirName = IOHelpers.GetTempPath("dir");
            var path = Path.Combine(Path.GetTempPath(), dirName);

            while (Directory.Exists(path))
            {
                dirName = Path.GetRandomFileName();
                path = Path.Combine(Path.GetTempPath(), dirName);
            }

            return path;
        }

        public static void SafeDeleteDirectory(string path, bool recursive = false)
        {
            if (Directory.Exists(path))
            {
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                Directory.EnumerateFileSystemEntries(path, "*", searchOption)
                    .ForEach(x => File.SetAttributes(x, FileAttributes.Normal));

                Directory.Delete(path, recursive);
            }
        }
    }
}
