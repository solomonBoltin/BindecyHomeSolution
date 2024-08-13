using System.Diagnostics;
using Xunit;

PermissionManagerTester.TestProgram();


public class PermissionManager
{
    private Dictionary<int, (string path, bool read, bool write)> handleToFile;
    private Dictionary<string, (int readCount, int writeCount)> fileReferenceCounts;
    private int nextHandle;

    public PermissionManager()
    {
        handleToFile = new Dictionary<int, (string path, bool read, bool write)>();
        fileReferenceCounts = new Dictionary<string, (int readCount, int writeCount)>();
        nextHandle = 1;
    }

    public int Register(string path, bool read, bool write)
    {
        if (!fileReferenceCounts.ContainsKey(path))
        {
            fileReferenceCounts[path] = (0, 0);
        }

        var (readCount, writeCount) = fileReferenceCounts[path];
        if (read) readCount++;
        if (write) writeCount++;

        fileReferenceCounts[path] = (readCount, writeCount);
        handleToFile[nextHandle] = (path, read, write);

        UpdatePermissions(path, readCount > 0, writeCount > 0);

        return nextHandle++;
    }

    public void Unregister(int handle)
    {
        if (!handleToFile.ContainsKey(handle))
        {
            return;
        }

        var (path, read, write) = handleToFile[handle];
        handleToFile.Remove(handle);

        var (readCount, writeCount) = fileReferenceCounts[path];
        if (read) readCount--;
        if (write) writeCount--;

        if (readCount == 0 && writeCount == 0)
        {
            fileReferenceCounts.Remove(path);
        }
        else
        {
            fileReferenceCounts[path] = (readCount, writeCount);
        }

        UpdatePermissions(path, readCount > 0, writeCount > 0);
    }

    private void UpdatePermissions(string path, bool read, bool write)
    {
        string command = "chmod ";
        if (read) command += "o+r,";
        if (write) command += "o+w,";
        if (!read) command += "o-r,";
        if (!write) command += "o-w,";

        command = command.Substring(0, command.Length - 1);
        command += " " + path;

        ExecuteCommand(command);
    }

    public string filePermissions(string path)
    {
        string res = ExecuteCommand($"ls -l {path}");
        res = res.Substring(0, 10);
        return res;
    }

    public string ExecuteCommand(string command)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        return process.StandardOutput.ReadToEnd();
    }
}


class PermissionManagerTester()
{
    public static void TestProgram()
    {
        // Call, Return value,  secret.txt permissions
        // register(“secret.txt”, true, false) 1 -rw-r--r--

        // register(“secret.txt”, false, true) 2 -rw-r--rw-
        // unregsiter(1) -rw-r---w-
        // unregsiter(2) -rw-r-----

        // register(“secret.txt”, true, true) 3 -rw-r--rw-
        // register(“secret.txt”, true, false) 4 -rw-r--rw-
        // unregister(3) -rw-r--r--

        // unregister(3) -rw-r--r--
        // unregister(4) -rw-r-----
        // register(“xxxxxx”, true, true) 5 -rw-r-----

        Console.WriteLine("Testing PermissionManager...");

        var manager = new PermissionManager();
        string path = "secret.txt";

        // set initial permissions to -rw-r-----
        manager.ExecuteCommand($"chmod 640 {path}");
        Console.WriteLine($"Initial permissions: {manager.filePermissions(path)}\n\n");


        int handle = manager.Register(path, true, false);
        Console.WriteLine($"register(\"{path}\", true, false) | {handle} | {manager.filePermissions(path)}");
        Console.WriteLine($"Expected: -rw-r--r-- | Actual: {manager.filePermissions(path)}\n\n");
        Assert.Equal("-rw-r--r--", manager.filePermissions(path));

        int handle2 = manager.Register(path, false, true);
        Console.WriteLine($"register(\"{path}\", false, true) | {handle2} | {manager.filePermissions(path)}");
        Console.WriteLine($"Expected: -rw-r--rw- | Actual: {manager.filePermissions(path)}\n\n");
        Assert.Equal("-rw-r--rw-", manager.filePermissions(path));

        manager.Unregister(handle);
        Console.WriteLine($"unregister({handle}) |  | {manager.filePermissions(path)}");
        Console.WriteLine($"Expected: -rw-r---w- | Actual: {manager.filePermissions(path)}\n\n");
        Assert.Equal("-rw-r---w-", manager.filePermissions(path));

        manager.Unregister(handle2);
        Console.WriteLine($"unregister({handle2}) |  | {manager.filePermissions(path)}");
        Console.WriteLine($"Expected: -rw-r----- | Actual: {manager.filePermissions(path)}\n\n");
        Assert.Equal("-rw-r-----", manager.filePermissions(path));

        int handle3 = manager.Register(path, true, true);
        Console.WriteLine($"register(\"{path}\", true, true) | {handle3} | {manager.filePermissions(path)}");
        Console.WriteLine($"Expected: -rw-r--rw- | Actual: {manager.filePermissions(path)}\n\n");
        Assert.Equal("-rw-r--rw-", manager.filePermissions(path));

        int handle4 = manager.Register(path, true, false);
        Console.WriteLine($"register(\"{path}\", true, false) | {handle4} | {manager.filePermissions(path)}");
        Console.WriteLine($"Expected: -rw-r--rw- | Actual: {manager.filePermissions(path)}\n\n");
        Assert.Equal("-rw-r--rw-", manager.filePermissions(path));

        manager.Unregister(handle3);
        Console.WriteLine($"unregister({handle3}) |  | {manager.filePermissions(path)}");
        Console.WriteLine($"Expected: -rw-r--r-- | Actual: {manager.filePermissions(path)}\n\n");
        Assert.Equal("-rw-r--r--", manager.filePermissions(path));

        manager.Unregister(handle3);
        Console.WriteLine($"unregister({handle3}) |  | {manager.filePermissions(path)}");
        Console.WriteLine($"Expected: -rw-r--r-- | Actual: {manager.filePermissions(path)}\n\n");
        Assert.Equal("-rw-r--r--", manager.filePermissions(path));

        manager.Unregister(handle4);
        Console.WriteLine($"unregister({handle4}) |  | {manager.filePermissions(path)}");
        Console.WriteLine($"Expected: -rw-r----- | Actual: {manager.filePermissions(path)}\n\n");
        Assert.Equal("-rw-r-----", manager.filePermissions(path));

        int handle5 = manager.Register("xxxxxx", true, true);
        Console.WriteLine($"register(\"xxxxxx\", true, true) | {handle5} | {manager.filePermissions(path)}");
        Console.WriteLine($"Expected: -rw-r----- | Actual: {manager.filePermissions(path)}\n\n");
        Assert.Equal("-rw-r-----", manager.filePermissions(path));
    }
}
