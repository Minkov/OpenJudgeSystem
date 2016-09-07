namespace OJS.Common.Extensions
{
    using System;
    using System.IO;

    // TODO: Unit test
    public static class FileHelpers
    {
        public static string SaveStringToTempFile(string stringToWrite)
        {
            string tempFilePath = string.Empty;

            tempFilePath = $"{Path.GetTempPath()}File-{Guid.NewGuid()}.txt";

            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            File.WriteAllText(tempFilePath, stringToWrite);
            return tempFilePath;
        }

        public static string SaveByteArrayToTempFile(byte[] dataToWrite)
        {
            var tempFilePath = Path.GetTempFileName();
            File.WriteAllBytes(tempFilePath, dataToWrite);
            return tempFilePath;
        }

        public static string GetTempPath()
        {
            return Path.GetTempPath() + "File-" + Guid.NewGuid() + ".tmp";
        }
    }
}
