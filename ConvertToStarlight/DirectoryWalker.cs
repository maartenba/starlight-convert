namespace ConvertToStarlight;

public static class DirectoryWalker
{
    public static void Walk(
        string sourceDirectory,
        bool recursive,
        Action<string, string> processFile,
        Action<string, string> processDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
            return;

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            processFile(sourceDirectory, Path.GetFileName(file));
        }
        
        foreach (string directory in Directory.GetDirectories(sourceDirectory))
        {
            processDirectory(sourceDirectory, Path.GetFileName(directory));

            if (recursive)
            {
                Walk(directory, recursive, processFile, processDirectory);
            }
        }
    }
}