namespace OJS.Common.Extensions
{
    using System.IO;

    // TODO: Unit test
    public static class FileHelpers
    {
        public static string SaveStringToTempFile(string stringToWrite)
        {
            var tempFilePath = IOHelpers.GetTempPath("file");

            File.WriteAllText(tempFilePath, stringToWrite);
            return tempFilePath;
        }

        public static string SaveByteArrayToTempFile(byte[] dataToWrite)
        {
            var tempFilePath = IOHelpers.GetTempPath();
            File.WriteAllBytes(tempFilePath, dataToWrite);
            return tempFilePath;
        }
    }
}
